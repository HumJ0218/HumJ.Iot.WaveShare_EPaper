using System.Device.Gpio;
using System.Device.Spi;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HumJ.Iot.WaveShare_EPaper
{
    public class Epd7in3f : IDisposable
    {
        public const int WIDTH = 800;
        public const int HEIGHT = 480;

        public static SpiConnectionSettings SpiConnectionSettings { get; } = new SpiConnectionSettings(0, 0)
        {
            Mode = SpiMode.Mode0,
            ClockFrequency = 4000000,
            DataFlow = DataFlow.MsbFirst,
            ChipSelectLineActiveState = 0,
        };

        public IDictionary<int, Epd7in3fColor> ColorMap { get; } = new Dictionary<int, Epd7in3fColor>
        {
            [0x000000] = Epd7in3fColor.Black,
            [0xFFFFFF] = Epd7in3fColor.White,
            [0x00FF00] = Epd7in3fColor.Green,
            [0x0000FF] = Epd7in3fColor.Blue,
            [0xFF0000] = Epd7in3fColor.Red,
            [0xFFFF00] = Epd7in3fColor.Yellow,
            [0xFF8000] = Epd7in3fColor.Orange,
        };
        public int BytesPerPacket { get; set; }

        private readonly GpioController gpio;
        private readonly SpiDevice spi;

        private readonly int dc;
        private readonly int busy;
        private readonly int reset;
        private readonly int pwr;

        private object locker = new();

        public Epd7in3f(GpioController gpio, SpiDevice spi, int dc = 25, int busy = 24, int reset = 17, int pwr = 18, int bytesPerPacket = 4096)
        {
            this.gpio = gpio ?? throw new ArgumentNullException(nameof(gpio));
            this.spi = spi ?? throw new ArgumentNullException(nameof(spi));
            this.dc = dc;
            this.busy = busy;
            this.reset = reset;
            this.pwr = pwr;

            BytesPerPacket = bytesPerPacket;

            lock (locker)
            {
                gpio.OpenPin(reset, PinMode.Output);
                gpio.OpenPin(dc, PinMode.Output);
                gpio.OpenPin(busy, PinMode.InputPullUp);
                gpio.OpenPin(pwr, PinMode.Output, 1);
            }
        }

        public void Initialize()
        {
            lock (locker)
            {
                // EPD hardware init start
                Reset();
                WaitForIdle();
                Thread.Sleep(30);

                SendCommand(0xAA); // CMDH
                SendData(0x49, 0x55, 0x20, 0x08, 0x09, 0x18);

                SendCommand(0x01);
                SendData(0x3F, 0x00, 0x32, 0x2A, 0x0E, 0x2A);

                SendCommand(0x00);
                SendData(0x5F, 0x69);

                SendCommand(0x03);
                SendData(0x00, 0x54, 0x00, 0x44);

                SendCommand(0x05);
                SendData(0x40, 0x1F, 0x1F, 0x2C);

                SendCommand(0x06);
                SendData(0x6F, 0x1F, 0x1F, 0x22);

                SendCommand(0x08);
                SendData(0x6F, 0x1F, 0x1F, 0x22);

                SendCommand(0x13); // IPC
                SendData(0x00, 0x04);

                SendCommand(0x30);
                SendData(0x3C);

                SendCommand(0x41); // TSE
                SendData(0x00);

                SendCommand(0x50);
                SendData(0x3F);

                SendCommand(0x60);
                SendData(0x02, 0x00);

                SendCommand(0x61);
                SendData(0x03, 0x20, 0x01, 0xE0);

                SendCommand(0x82);
                SendData(0x1E);

                SendCommand(0x84);
                SendData(0x00);

                SendCommand(0x86); // AGID
                SendData(0x00);

                SendCommand(0xE3);
                SendData(0x2F);

                SendCommand(0xE0); // CCSET
                SendData(0x00);

                SendCommand(0xE6); // TSSET
                SendData(0x00);
            }
        }

        public void Clear(Epd7in3fColor color = Epd7in3fColor.White)
        {
            lock (locker)
            {
                var buffer = new byte[WIDTH * HEIGHT / 2];
                buffer.AsSpan().Fill((byte)(((byte)color << 4) | ((byte)color)));

                SendCommand(0x10);
                SendData(buffer);

                Flush();
            }
        }

        public void ShowImage(Image<Rgb24> image)
        {
            var buffer = new byte[HEIGHT * WIDTH / 2];

            for (var i = 0; i < buffer.Length; i++)
            {
                var pixelIndex = i * 2;
                var x = pixelIndex % WIDTH;
                var y = pixelIndex / WIDTH;

                var dataH = GetPixelColorByte(image[x, y]);
                var dataL = GetPixelColorByte(image[x + 1, y]);

                var data = (dataH << 4) + dataL;
                buffer[i] = (byte)data;
            }

            lock (locker)
            {
                SendCommand(0x10);
                SendData(buffer);

                Flush();
            }
        }

        public void Sleep()
        {
            lock (locker)
            {
                SendCommand(0x07); // DEEP_SLEEP
                SendData(0xA5);

                Thread.Sleep(2000);
            }
        }

        public void Dispose()
        {
            lock (locker)
            {
                gpio.Write(reset, 0);
                gpio.Write(dc, 0);
                gpio.Write(pwr, 0);

                gpio.ClosePin(reset);
                gpio.ClosePin(dc);
                gpio.ClosePin(busy);
                gpio.ClosePin(pwr);
            }

            locker = null!;
            GC.SuppressFinalize(this);
        }

        private void SendCommand(byte command)
        {
            gpio.Write(dc, 0);
            spi.WriteByte(command);
        }

        private void SendData(byte value)
        {
            gpio.Write(dc, 1);
            spi.WriteByte(value);
        }

        private void SendData(params byte[] buffer) => SendData(buffer.AsSpan());

        private void SendData(ReadOnlySpan<byte> buffer)
        {
            gpio.Write(dc, 1);

            for (var i = 0; i < buffer.Length; i += BytesPerPacket)
            {
                var packet = buffer[i..];
                if (packet.Length > BytesPerPacket)
                {
                    packet = packet[..BytesPerPacket];
                }
                spi.Write(packet);
            }
        }

        private void Reset()
        {
            lock (locker)
            {
                gpio.Write(reset, 1);
                Thread.Sleep(20);
                gpio.Write(reset, 0); // module reset
                Thread.Sleep(2);
                gpio.Write(reset, 1);
                Thread.Sleep(20);
            }
        }

        private void Flush()
        {
            SendCommand(0x04); // POWER_ON
            WaitForIdle();

            SendCommand(0x12); // DISPLAY_REFRESH
            SendData(0x00);
            WaitForIdle();

            SendCommand(0x02); // POWER_OFF
            SendData(0x00);
            WaitForIdle();
        }

        private void WaitForIdle()
        {
            while (gpio.Read(busy) == 0) // 0: busy, 1: idle
            {
                Thread.Sleep(5);
            }
        }

        private byte GetPixelColorByte(Rgb24 rgb24)
        {
            var r = rgb24.R;
            var g = rgb24.G;
            var b = rgb24.B;

            var rgb = (r << 16) + (g << 8) + b;
            var color = ColorMap[rgb];

            return (byte)color;
        }
    }
}