using HumJ.Iot.WaveShare_EPaper.Base;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Device.Gpio;
using System.Device.Spi;

namespace HumJ.Iot.WaveShare_EPaper
{
    public class Epd13in3e : IEpd
    {
        public int Width { get; } = 1200;
        public int Height { get; } = 1600;

        public Color[] Palette => [.. PaletteCommand.Keys];
        public Dictionary<Color, byte> PaletteCommand { get; } = new Dictionary<Color, byte>
        {
            [Color.FromRgb(0x00, 0x00, 0x1C)] = 0, // black
            [Color.FromRgb(0xE9, 0xF4, 0xFF)] = 1, // white
            [Color.FromRgb(0xFF, 0xFF, 0x00)] = 2, // yellow
            [Color.FromRgb(0x71, 0x00, 0x05)] = 3, // red
            [Color.FromRgb(0x00, 0x48, 0xA7)] = 5, // blue
            [Color.FromRgb(0x2D, 0x67, 0x2A)] = 6, // green
        };

        private readonly int csm = 22;
        private readonly int css = 27;
        private readonly int rst = 17;
        private readonly int busy = 24;

        private SpiDevice spi;
        private GpioController gpio;

        public Epd13in3e(SpiDevice spi, GpioController gpio, int rst = 17, int busy = 24, int csm = 22, int css = 27)
        {
            this.spi = spi;
            this.gpio = gpio;

            this.rst = rst;
            this.busy = busy;
            this.csm = csm;
            this.css = css;

            gpio.OpenPin(this.csm, PinMode.Output, 1);
            gpio.OpenPin(this.css, PinMode.Output, 1);
            gpio.OpenPin(this.rst, PinMode.Output, 0);
            gpio.OpenPin(this.busy, PinMode.Input);
        }

        public void Initialize()
        {
            Reset();
            WaitForIdle();

            gpio.Write(csm, 0);
            SendData(AN_TM, AN_TM_V);
            CsAll(1);

            CsAll(0);
            SendData(CMD66, CMD66_V);
            CsAll(1);

            CsAll(0);
            SendData(PSR, PSR_V);
            CsAll(1);

            CsAll(0);
            SendData(CDI, CDI_V);
            CsAll(1);

            CsAll(0);
            SendData(TCON, TCON_V);
            CsAll(1);

            CsAll(0);
            SendData(AGID, AGID_V);
            CsAll(1);

            CsAll(0);
            SendData(PWS, PWS_V);
            CsAll(1);

            CsAll(0);
            SendData(CCSET, CCSET_V);
            CsAll(1);

            CsAll(0);
            SendData(TRES, TRES_V);
            CsAll(1);

            gpio.Write(csm, 0);
            SendData(PWR, PWR_V);
            CsAll(1);

            gpio.Write(csm, 0);
            SendData(EN_BUF, EN_BUF_V);
            CsAll(1);

            gpio.Write(csm, 0);
            SendData(BTST_P, BTST_P_V);
            CsAll(1);

            gpio.Write(csm, 0);
            SendData(BOOST_VDDP_EN, BOOST_VDDP_EN_V);
            CsAll(1);

            gpio.Write(csm, 0);
            SendData(BTST_N, BTST_N_V);
            CsAll(1);

            gpio.Write(csm, 0);
            SendData(BUCK_BOOST_VDDN, BUCK_BOOST_VDDN_V);
            CsAll(1);

            gpio.Write(csm, 0);
            SendData(TFT_VCOM_POWER, TFT_VCOM_POWER_V);
            CsAll(1);
        }

        public void Reset()
        {
            gpio.Write(rst, 1);
            Thread.Sleep(30);
            gpio.Write(rst, 0);
            Thread.Sleep(30);
            gpio.Write(rst, 1);
            Thread.Sleep(30);
            gpio.Write(rst, 0);
            Thread.Sleep(30);
            gpio.Write(rst, 1);
            Thread.Sleep(30);
        }

        public void Sleep()
        {
            CsAll(0);
            SendData(0x07); // DEEP_SLEEP
            SendData(0XA5);
            CsAll(1);
        }

        public void Clear(Color color)
        {
            var value = PaletteCommand[color];
            value = (byte)((value << 4) | value);

            var buffer = new byte[Width * Height / 2];
            buffer.AsSpan().Fill(value);

            Display(buffer);
        }

        public void Display(Image image)
        {
            if (image is Image<Rgb24> input)
            {
                var buffer = new byte[Width * Height / 2];

                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        var color = input[x, y];
                        var value = PaletteCommand[color];
                        buffer[y * Width / 2 + x / 2] |= (byte)(value << (4 * (1 - x % 2)));
                    }
                }

                Display(buffer);
            }
            else
            {
                using var clone = image.CloneAs<Rgb24>();
                Display(clone);
            }
        }

        public void DisplayPartial(Image image, int x, int y)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
            Reset();
            Sleep();

            spi.Dispose();
            gpio.Dispose();

            spi = null!;
            gpio = null!;

            GC.SuppressFinalize(this);
        }

        private const byte PSR = 0x00;
        private const byte PWR = 0x01;
        private const byte POF = 0x02;
        private const byte BTST_N = 0x05;
        private const byte BTST_P = 0x06;
        private const byte DRF = 0x12;
        private const byte CDI = 0x50;
        private const byte TCON = 0x60;
        private const byte TRES = 0x61;
        private const byte AN_TM = 0x74;
        private const byte AGID = 0x86;
        private const byte BUCK_BOOST_VDDN = 0xB0;
        private const byte TFT_VCOM_POWER = 0xB1;
        private const byte EN_BUF = 0xB6;
        private const byte BOOST_VDDP_EN = 0xB7;
        private const byte CCSET = 0xE0;
        private const byte PWS = 0xE3;
        private const byte CMD66 = 0xF0;

        private static readonly byte[] PSR_V = [0xDF, 0x69];
        private static readonly byte[] PWR_V = [0x0F, 0x00, 0x28, 0x2C, 0x28, 0x38];
        private static readonly byte[] POF_V = [0x00];
        private static readonly byte[] DRF_V = [0x00];
        private static readonly byte[] CDI_V = [0xF7];
        private static readonly byte[] TCON_V = [0x03, 0x03];
        private static readonly byte[] TRES_V = [0x04, 0xB0, 0x03, 0x20];
        private static readonly byte[] CMD66_V = [0x49, 0x55, 0x13, 0x5D, 0x05, 0x10];
        private static readonly byte[] EN_BUF_V = [0x07];
        private static readonly byte[] CCSET_V = [0x01];
        private static readonly byte[] PWS_V = [0x22];
        private static readonly byte[] AN_TM_V = [0xC0, 0x1C, 0x1C, 0xCC, 0xCC, 0xCC, 0x15, 0x15, 0x55];
        private static readonly byte[] AGID_V = [0x10];
        private static readonly byte[] BTST_P_V = [0xE8, 0x28];
        private static readonly byte[] BOOST_VDDP_EN_V = [0x01];
        private static readonly byte[] BTST_N_V = [0xE8, 0x28];
        private static readonly byte[] BUCK_BOOST_VDDN_V = [0x01];
        private static readonly byte[] TFT_VCOM_POWER_V = [0x02];

        private void CsAll(PinValue value)
        {
            gpio.Write(csm, value);
            gpio.Write(css, value);
        }

        private void SendData(byte cmd)
        {
            spi.WriteByte(cmd);
        }

        private void SendData(Span<byte> data)
        {
            spi.Write(data);
        }

        private void SendData(byte cmd, Span<byte> data)
        {
            spi.WriteByte(cmd);
            spi.Write(data);
        }

        private void WaitForIdle()
        {
            // LOW: busy, HIGH: idle
            while (gpio.Read(busy) == 0)
            {
                Thread.Sleep(10);
            }

            Thread.Sleep(20);
        }

        private void Flush()
        {
            // PON
            CsAll(0);
            SendData(0x04);
            CsAll(1);
            WaitForIdle();

            // DRF
            Thread.Sleep(50);

            CsAll(0);
            SendData(DRF, DRF_V);
            CsAll(1);
            WaitForIdle();

            // POF
            CsAll(0);
            SendData(POF, POF_V);
            CsAll(1);
        }

        private void Display(Span<byte> buffer)
        {
            var lineByteWidth = Width / 2;
            var regionLineByteWidth = lineByteWidth / 2;
            var height = Height;

            gpio.Write(csm, 0);
            SendData(0x10);
            for (int i = 0; i < height; i++)
            {
                SendData(buffer.Slice(i * lineByteWidth, regionLineByteWidth));
            }
            CsAll(1);

            gpio.Write(css, 0);
            SendData(0x10);
            for (int i = 0; i < height; i++)
            {
                SendData(buffer.Slice(i * lineByteWidth + regionLineByteWidth, regionLineByteWidth));
            }
            CsAll(1);

            Flush();
        }
    }
}