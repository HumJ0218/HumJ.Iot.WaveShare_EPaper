using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Device.Gpio;
using System.Device.Spi;

namespace HumJ.Iot.WaveShare_EPaper.Base
{
    public abstract class Epd7Color : IEpd
    {
        public abstract int Width { get; }
        public abstract int Height { get; }

        public Color[] Palette => [.. PaletteCommand.Keys];
        public abstract Dictionary<Color, byte> PaletteCommand { get; }

        public int BytesPerPacket { get; set; } = 4096;
        public Span<byte> Buffer => buffer;

        private SpiDevice spi;
        private GpioController gpio;
        private readonly int dc;
        private readonly int reset;
        private readonly int busy;

        protected byte[] buffer;

        public Epd7Color(SpiDevice spi, GpioController gpio, int dc, int reset, int busy)
        {
            buffer = new byte[Width * Height / 2];

            this.spi = spi;
            this.gpio = gpio;
            this.dc = dc;
            this.reset = reset;
            this.busy = busy;

            gpio.OpenPin(dc, PinMode.Output);
            gpio.OpenPin(reset, PinMode.Output);
            gpio.OpenPin(busy, PinMode.Input);
        }

        public void Dispose()
        {
            Reset();
            Sleep();

            spi = null!;
            gpio = null!;
            buffer = null!;

            GC.SuppressFinalize(this);
        }

        public void Initialize()
        {
            Reset();

            WriteCommand(Command.CMDH, 0x49, 0x55, 0x20, 0x08, 0x09, 0x18);
            WriteCommand(Command.PWRR, 0x3F, 0x00, 0x32, 0x2A, 0x0E, 0x2A);
            WriteCommand(Command.PSR, 0x5F, 0x69);
            WriteCommand(Command.POFS, 0x00, 0x54, 0x00, 0x44);
            WriteCommand(Command.BTST1, 0x40, 0x1F, 0x1F, 0x2C);
            WriteCommand(Command.BTST2, 0x6F, 0x1F, 0x16, 0x25);
            WriteCommand(Command.BTST3, 0x6F, 0x1F, 0x1F, 0x22);
            WriteCommand(Command.IPC, 0x00, 0x04);
            WriteCommand(Command.PLL, 0x02);
            WriteCommand(Command.TSE, 0x00);
            WriteCommand(Command.CDI, 0x3F);
            WriteCommand(Command.TCON, 0x02, 0x00);
            WriteCommand(Command.TRES, (byte)(Width >> 8), (byte)(Width >> 0), (byte)(Height >> 8), (byte)(Height >> 0));
            WriteCommand(Command.VDCS, 0x1E);
            WriteCommand(Command.T_VDCS, 0x01);
            WriteCommand(Command.AGID, 0x00);
            WriteCommand(Command.PWS, 0x2F);
            WriteCommand(Command.CCSET, 0x00);
            WriteCommand(Command.TSSET, 0x00);

            WriteCommand(Command.PON);
            WaitForIdle();
        }

        public void Reset()
        {
            gpio.Write(reset, 0);
            Thread.Sleep(10);
            gpio.Write(reset, 1);
            Thread.Sleep(10);
        }

        public void Sleep()
        {
            WriteCommand(Command.POF, 0x00);
            WaitForIdle();
        }

        public void Clear(Color color)
        {
            LoadImageData(color);
            Flush();
        }

        public void Display(Image image)
        {
            LoadImageData(image);
            Flush();
        }

        public void DisplayPartial(Image image, int x, int y)
        {
            throw new NotSupportedException();
        }

        public void Flush()
        {
            WriteCommand(Command.DTM, buffer);

            WriteCommand(Command.DRF, 0x00);

            Thread.Sleep(1); // 200us at least
            WaitForIdle();
        }

        private void LoadImageData(Color color)
        {
            var pixel = color.ToPixel<Rgb24>();

            var data = PaletteCommand[pixel];
            buffer.AsSpan().Fill((byte)((data << 4) | data));
        }

        private void LoadImageData(Image image)
        {
            if (image is Image<Rgb24> source)
            {
                Rgb24 pixel;
                byte data_H, data_L, data;
                int index = 0;

                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x += 2)
                    {
                        pixel = source[x, y];
                        data_H = PaletteCommand[pixel];

                        pixel = source[x + 1, y];
                        data_L = PaletteCommand[pixel];

                        data = (byte)((data_H << 4) | data_L);
                        buffer[index++] = data;
                    }
                }
            }
            else
            {
                using var clone = image.CloneAs<Rgb24>();
                LoadImageData(clone);
            }
        }

        protected void WaitForIdle()
        {
            while (gpio.Read(busy) == 0) // 0: busy, 1: idle
            {
                Thread.Sleep(1);
            }
        }

        protected void WriteCommand(Command command, params byte[] data)
        {
            gpio.Write(dc, 0);
            spi.WriteByte((byte)command);

            if (data.Length > 0)
            {
                WriteData(data);
            }
        }

        protected void WriteData(params byte[] data)
        {
            gpio.Write(dc, 1);

            for (var i = 0; i < data.Length; i += BytesPerPacket)
            {
                var packet = data[i..];
                if (packet.Length > BytesPerPacket)
                {
                    packet = packet[..BytesPerPacket];
                }
                spi.Write(packet);
            }
        }

        /// <summary>
        /// 指令寄存器
        /// </summary>
        protected enum Command : byte
        {
            /// <summary>
            /// Panel Setting Register
            /// </summary>
            PSR = 0x00,

            /// <summary>
            /// Power Setting Register
            /// </summary>
            PWRR = 0x01,

            /// <summary>
            /// Power Off
            /// </summary>
            POF = 0x02,

            /// <summary>
            /// Power Off Sequence Setting
            /// </summary>
            POFS = 0x03,

            /// <summary>
            /// Power On
            /// </summary>
            PON = 0x04,

            /// <summary>
            /// Booster Soft Start 1
            /// </summary>
            BTST1 = 0x05,

            /// <summary>
            /// Booster Soft Start 2
            /// </summary>
            BTST2 = 0x06,

            /// <summary>
            /// Deep Sleep
            /// </summary>
            DSLP = 0x07,

            /// <summary>
            /// Booster Soft Start 3
            /// </summary>
            BTST3 = 0x08,

            /// <summary>
            /// Data Transmission Mode
            /// </summary>
            DTM = 0x10,

            /// <summary>
            /// Display Refresh
            /// </summary>
            DRF = 0x12,

            /// <summary>
            /// Inter-communication Protocol Control
            /// </summary>
            IPC = 0x13,

            /// <summary>
            /// Phase-Locked Loop Control
            /// </summary>
            PLL = 0x30,

            /// <summary>
            /// Temperature Sensor Enable
            /// </summary>
            TSE = 0x41,

            /// <summary>
            /// Clock Division Index
            /// </summary>
            CDI = 0x50,

            /// <summary>
            /// Timing Control
            /// </summary>
            TCON = 0x60,

            /// <summary>
            /// Resolution Setting
            /// </summary>
            TRES = 0x61,

            /// <summary>
            /// Revision Code
            /// </summary>
            REV = 0x70,

            /// <summary>
            /// VCOM Voltage Setting
            /// </summary>
            VDCS = 0x82,

            /// <summary>
            /// Test VCOM Voltage Setting
            /// </summary>
            T_VDCS = 0x84,

            /// <summary>
            /// Analog Gain Control
            /// </summary>
            AGID = 0x86,

            /// <summary>
            /// Command Handoff
            /// </summary>
            CMDH = 0xAA,

            /// <summary>
            /// Clock Configuration Set
            /// </summary>
            CCSET = 0xE0,

            /// <summary>
            /// Power Save Mode
            /// </summary>
            PWS = 0xE3,

            /// <summary>
            /// Touch Screen Setting
            /// </summary>
            TSSET = 0xE6,
        }
    }
}
