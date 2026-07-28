using HumJ.Iot.WaveShare_EPaper.Base;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using System.Device.Gpio;
using System.Device.Spi;

namespace HumJ.Iot.WaveShare_EPaper
{
    public class Epd10in85 : IEpd
    {
        public int Width { get; } = 1360;

        public int Height { get; } = 480;

        public DisplayMode Mode { get; } = DisplayMode.Normal;

        public bool IsPartial { get; private set; }

        public Color[] Palette => [.. colorData2.Keys];

        private readonly static Dictionary<Color, byte> colorData2 = new()
        {
            [Color.Black] = 0,
            [Color.White] = 1,
        };

        private const bool dcc = false;
        private const bool dcd = true;
        private readonly bool gpio;

        private readonly SpiDevice? spi_m;
        private readonly SpiDevice? spi_s;
        private readonly GpioPin dc;
        private readonly GpioPin reset;
        private readonly GpioPin busy;

        private readonly GpioPin? sda;
        private readonly GpioPin? scl;
        private readonly GpioPin? cem;
        private readonly GpioPin? ces;

        private static readonly byte[] clearBlack;
        private static readonly byte[] clearWhite;

        static Epd10in85()
        {
            clearBlack = new byte[40800];
            clearWhite = new byte[40800];

            clearWhite.AsSpan().Fill(0xFF);
        }

        public Epd10in85(SpiDevice spi_m, SpiDevice spi_s, GpioPin dc, GpioPin reset, GpioPin busy)
        {
            ArgumentNullException.ThrowIfNull(spi_m);
            ArgumentNullException.ThrowIfNull(spi_s);
            ArgumentNullException.ThrowIfNull(dc);
            ArgumentNullException.ThrowIfNull(reset);
            ArgumentNullException.ThrowIfNull(busy);

            if (dc.GetPinMode() != PinMode.Output) throw new NotSupportedException(nameof(dc));
            if (reset.GetPinMode() != PinMode.Output) throw new NotSupportedException(nameof(reset));
            if (busy.GetPinMode() != PinMode.Input) throw new NotSupportedException(nameof(busy));

            this.spi_m = spi_m;
            this.spi_s = spi_s;

            this.dc = dc;
            this.reset = reset;
            this.busy = busy;

            gpio = false;
        }

        public Epd10in85(GpioPin sda, GpioPin scl, GpioPin cem, GpioPin ces, GpioPin reset, GpioPin busy)
        {
            ArgumentNullException.ThrowIfNull(sda);
            ArgumentNullException.ThrowIfNull(scl);
            ArgumentNullException.ThrowIfNull(cem);
            ArgumentNullException.ThrowIfNull(ces);
            ArgumentNullException.ThrowIfNull(reset);
            ArgumentNullException.ThrowIfNull(busy);

            if (sda.GetPinMode() != PinMode.Output) throw new NotSupportedException(nameof(sda));
            if (scl.GetPinMode() != PinMode.Output) throw new NotSupportedException(nameof(scl));
            if (cem.GetPinMode() != PinMode.Output) throw new NotSupportedException(nameof(cem));
            if (ces.GetPinMode() != PinMode.Output) throw new NotSupportedException(nameof(ces));
            if (reset.GetPinMode() != PinMode.Output) throw new NotSupportedException(nameof(reset));
            if (busy.GetPinMode() != PinMode.Input) throw new NotSupportedException(nameof(busy));

            this.sda = sda;
            this.scl = scl;
            this.cem = cem;
            this.ces = ces;

            this.dc = null!;
            this.reset = reset;
            this.busy = busy;

            gpio = true;
        }

        public void Reset()
        {
            reset.Write(1);
            Thread.Sleep(200);
            reset.Write(0);
            Thread.Sleep(2);
            reset.Write(1);
            Thread.Sleep(200);
        }

        public void Initialize()
        {
            IsPartial = false;

            Reset();
            ReadBusy();

            SendCommandAll(0x4D, 0x55);
            SendCommandAll(0xA6, 0x38);
            SendCommandAll(0xB4, 0x5D);
            SendCommandAll(0xB6, 0x80);
            SendCommandAll(0xB7, 0x00);
            SendCommandAll(0xF7, 0x02);
            SendCommandAll(0xAE, 0xA0);
            SendCommandAll(0xE0, 0x01);
            SendCommandAll(0x00, 0x9F, 0x0D);
            SendCommandAll(0x06, 0x57, 0x24, 0x28, 0x32, 0x08, 0x48);
            SendCommandAll(0x61, 0x02, 0xA8, 0x01, 0xE0);
            SendCommandAll(0x62, 0x00, 0x00, 0x00, 0x00);
            SendCommandAll(0x60, 0x31);
            SendCommandAll(0x50, 0x97);
            SendCommandAll(0xE8, 0x01);
            SendCommandAll(0x04);

            Thread.Sleep(200);
            ReadBusy();
        }

        public void InitializePartial()

        {
            IsPartial = true;

            Reset();
            ReadBusy();

            SendCommandAll(0x00, 0x1F);
            SendCommandAll(0xE0, 0x03);
            SendCommandAll(0xE5, 0x64);
            SendCommandAll(0x04);
            Thread.Sleep(200);

            ReadBusy();
        }

        public void Display(Image image)
        {
            throw new NotImplementedException();
        }

        public void DisplayPartial(Image image, int x, int y)
        {
            throw new NotImplementedException();
        }

        public void DisplayRaw(byte[] bufferM, byte[] bufferS)
        {
            SendCommandM(0x13, bufferM);
            SendCommandS(0x13, bufferS);
            TurnOnDisplay();
        }

        public void DisplayPartialRaw(byte[] bufferM, byte[] bufferS)
        {
            SendCommandAll(0x50, 0xA9, 0x07);
            SendCommandAll(0x91); //This command makes the display enter partial mode
            SendCommandAll(0x90, 0, 0, (byte)(((Width - 1) / 2) >> 8), (byte)((Width - 1) / 2), 0, 0, (byte)((Height - 1) >> 8), (byte)(Height - 1)); //resolution setting

            SendCommandAll(0x13, clearBlack);
            TurnOnDisplay();
            SendCommandAll(0x13, clearWhite);
            TurnOnDisplay();

            SendCommandM(0x13, bufferM);
            SendCommandS(0x13, bufferS);
            TurnOnDisplay();
        }

        public void Clear(Color color)
        {
            var buffer = new byte[Width / 2 * Height / 8];
            buffer.AsSpan().Fill((byte)(color == Color.White ? 0xFF : 0x00));

            SendCommandAll(0x10, color == Color.White ? clearWhite: clearBlack);
            SendCommandAll(0x13, color == Color.White ? clearWhite : clearBlack);

            TurnOnDisplay();
        }

        public void Clear(byte pixel = 0xFF)
        {
            var buffer = new byte[Width / 2 * Height / 8];
            buffer.AsSpan().Fill(pixel);

            SendCommandAll(0x10, buffer);
            SendCommandAll(0x13, buffer);

            TurnOnDisplay();
        }

        public void Sleep()
        {
            SendCommandAll(0x02);
            Thread.Sleep(200);
            ReadBusy();

            SendCommandAll(0x07, 0xA5); // deep sleep
            Thread.Sleep(2000);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            throw new NotImplementedException();
        }

        private void ReadBusy()
        {
            while (busy.Read() == 0) // 0: busy, 1: idle
            {
                Thread.Sleep(200);
            }
        }

        private void TurnOnDisplay()
        {
            SendCommandAll(0x12);
            Thread.Sleep(100); // The delay here is necessary, 200uS at least!!!
            ReadBusy(); // waiting for the electronic paper IC to release the idle signal
        }

        private void SendCommandAll(byte command, params byte[] data)
        {
            if (gpio)
            {
                cem!.Write(0);
                ces!.Write(0);

                GpioWriteByte(dcc, command);
                foreach (var d in data)
                {
                    GpioWriteByte(dcd, d);
                }

                cem!.Write(1);
                ces!.Write(1);
            }
            else
            {
                SpiSendCommand(spi_m!, command, data);
                SpiSendCommand(spi_s!, command, data);
            }
        }
        private void SendCommandM(byte command, params byte[] data)
        {
            if (gpio)
            {
                cem!.Write(0);

                GpioWriteByte(dcc, command);
                foreach (var d in data)
                {
                    GpioWriteByte(dcd, d);
                }

                cem!.Write(1);
            }
            else
            {
                SpiSendCommand(spi_m!, command, data);
            }
        }
        private void SendCommandS(byte command, params byte[] data)
        {
            if (gpio)
            {
                ces!.Write(0);

                GpioWriteByte(dcc, command);
                foreach (var d in data)
                {
                    GpioWriteByte(dcd, d);
                }

                ces!.Write(1);
            }
            else
            {
                SpiSendCommand(spi_s!, command, data);
            }
        }

        private void SpiSendCommand(SpiDevice spi, byte command, params byte[] data)
        {
            dc.Write(0);
            spi.WriteByte(command);
            if (data.Length > 0)
            {
                SpiSendData(spi, data);
            }
        }
        private void SpiSendData(SpiDevice spi, params byte[] data)
        {
            dc.Write(1);
            spi.Write(data);
        }
        private void GpioWriteByte(bool dc, byte b)
        {
            if (!gpio) return;

            sda!.Write(dc);
            scl!.Write(1);
            scl.Write(0);

            sda!.Write((b & 0b_10000000) != 0);
            scl!.Write(1);
            scl.Write(0);

            sda.Write((b & 0b_01000000) != 0);
            scl.Write(1);
            scl.Write(0);

            sda.Write((b & 0b_00100000) != 0);
            scl.Write(1);
            scl.Write(0);

            sda.Write((b & 0b_00010000) != 0);
            scl.Write(1);
            scl.Write(0);

            sda.Write((b & 0b_00001000) != 0);
            scl.Write(1);
            scl.Write(0);

            sda.Write((b & 0b_00000100) != 0);
            scl.Write(1);
            scl.Write(0);

            sda.Write((b & 0b_00000010) != 0);
            scl.Write(1);
            scl.Write(0);

            sda.Write((b & 0b_00000001) != 0);
            scl.Write(1);
            scl.Write(0);
        }
    }
}
