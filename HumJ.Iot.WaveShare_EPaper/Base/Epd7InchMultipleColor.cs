using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Device.Gpio;
using System.Device.Spi;

namespace HumJ.Iot.WaveShare_EPaper.Base
{
    public abstract class Epd7InchMultipleColor : IEPaper
    {
        public abstract int Width { get; }
        public abstract int Height { get; }
        public Color[] Palette => PaletteCommand.Keys.Select(v => Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v)).ToArray();

        public int BytesPerPacket { get; set; } = 4096;
        public Span<byte> Buffer => buffer;

        public readonly Dictionary< int,byte> PaletteCommand = [];

        private const byte PSR = 0x00;
        private const byte PWRR = 0x01;
        private const byte POF = 0x02;
        private const byte POFS = 0x03;
        private const byte PON = 0x04;
        private const byte BTST1 = 0x05;
        private const byte BTST2 = 0x06;
        private const byte DSLP = 0x07;
        private const byte BTST3 = 0x08;
        private const byte DTM = 0x10;
        private const byte DRF = 0x12;
        private const byte IPC = 0x13;
        private const byte PLL = 0x30;
        private const byte TSE = 0x41;
        private const byte CDI = 0x50;
        private const byte TCON = 0x60;
        private const byte TRES = 0x61;
        private const byte REV = 0x70;
        private const byte VDCS = 0x82;
        private const byte T_VDCS = 0x84;
        private const byte AGID = 0x86;
        private const byte CMDH = 0xAA;
        private const byte CCSET = 0xE0;
        private const byte PWS = 0xE3;
        private const byte TSSET = 0xE6;

        private SpiDevice spi;
        private GpioController gpio;
        private int dc;
        private int reset;
        private int busy;

        private byte[] buffer = new byte[800 * 480 / 2];

        public Epd7InchMultipleColor(SpiDevice spi, GpioController gpio, int dc, int reset, int busy)
        {
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
            gpio.ClosePin(dc);
            gpio.ClosePin(reset);
            gpio.ClosePin(busy);

            spi = null!;
            gpio = null!;
            buffer = null!;

            GC.SuppressFinalize(this);
        }

        public void Initialize()
        {
            Reset();

            WriteCommand(CMDH);
            WriteData(0x49);
            WriteData(0x55);
            WriteData(0x20);
            WriteData(0x08);
            WriteData(0x09);
            WriteData(0x18);

            WriteCommand(PWRR);
            WriteData(0x3F);
            WriteData(0x00);
            WriteData(0x32);
            WriteData(0x2A);
            WriteData(0x0E);
            WriteData(0x2A);

            WriteCommand(PSR);
            WriteData(0x5F);
            WriteData(0x69);

            WriteCommand(POFS);
            WriteData(0x00);
            WriteData(0x54);
            WriteData(0x00);
            WriteData(0x44);

            WriteCommand(BTST1);
            WriteData(0x40);
            WriteData(0x1F);
            WriteData(0x1F);
            WriteData(0x2C);

            WriteCommand(BTST2);
            WriteData(0x6F);
            WriteData(0x1F);
            WriteData(0x16);
            WriteData(0x25);

            WriteCommand(BTST3);
            WriteData(0x6F);
            WriteData(0x1F);
            WriteData(0x1F);
            WriteData(0x22);

            WriteCommand(IPC);
            WriteData(0x00);
            WriteData(0x04);

            WriteCommand(PLL);
            WriteData(0x02);

            WriteCommand(TSE);
            WriteData(0x00);

            WriteCommand(CDI);
            WriteData(0x3F);

            WriteCommand(TCON);
            WriteData(0x02);
            WriteData(0x00);

            WriteCommand(TRES);
            WriteData(0x03);
            WriteData(0x20);
            WriteData(0x01);
            WriteData(0xE0);

            WriteCommand(VDCS);
            WriteData(0x1E);

            WriteCommand(T_VDCS);
            WriteData(0x01);

            WriteCommand(AGID);
            WriteData(0x00);

            WriteCommand(PWS);
            WriteData(0x2F);

            WriteCommand(CCSET);
            WriteData(0x00);

            WriteCommand(TSSET);
            WriteData(0x00);

            WriteCommand(PON);
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
            WriteCommand(POF);
            WriteData(0x00);
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

        public void DisplayPartial(Image image, Rectangle rectangle)
        {
            throw new NotSupportedException();
        }

        public void Flush()
        {
            WriteCommand(DTM);
            WriteData(buffer);

            WriteCommand(DRF);
            WriteData(0x00);

            Thread.Sleep(1); // 200us at least
            WaitForIdle();
        }

        private void LoadImageData(Color color)
        {
            var pixel=color.ToPixel<Rgb24>();

            var data = PaletteCommand[((pixel.R << 16) | (pixel.G << 8) | (pixel.B << 0))];
            buffer.AsSpan().Fill((byte)((data << 4) | data));
        }

        private void LoadImageData(Image image)
        {
           
            using Image<Rgb24> imageCopy = image.CloneAs<Rgb24>();

            Rgb24 pixel;
            byte data_H, data_L, data;
            int index = 0;

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x += 2)
                {
                    pixel = imageCopy[x, y];
                    data_H = PaletteCommand[pixel.R << 16 | pixel.G << 8 | pixel.B];

                    pixel = imageCopy[x + 1, y];
                    data_L = PaletteCommand[pixel.R << 16 | pixel.G << 8 | pixel.B];

                    data = (byte)((data_H << 4) | data_L);
                    buffer[index++] = data;
                }
            }
        }

        private void WaitForIdle()
        {
            while (gpio.Read(busy) == 0) // 0: busy, 1: idle
            {
                Thread.Sleep(1);
            }
        }

        private void WriteCommand(byte command)
        {
            gpio.Write(dc, 0);
            spi.WriteByte(command);
        }

        private void WriteData(byte data)
        {
            gpio.Write(dc, 1);
            spi.WriteByte(data);
        }

        private void WriteData(ReadOnlySpan<byte> data)
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
    }
}
