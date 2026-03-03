using HumJ.Iot.WaveShare_EPaper.Base;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;

namespace HumJ.Iot.WaveShare_EPaper
{
    public class Epd5in79 : IEpd
    {
        public int Width { get; } = 792;
        public int Height { get; } = 272;
        public DisplayMode Mode { get; set; } = DisplayMode.Normal;

        public Color[] Palette => [.. (Mode == DisplayMode.Gray4 ? colorData4.Keys : colorData2.Keys)];

        private readonly static Dictionary<Color, byte> colorData2 = new()
        {
            [Color.Black] = 0,
            [Color.White] = 1,
        };
        private readonly static Dictionary<Color, byte> colorData4 = new()
        {
            [Color.FromRgb(0, 0, 0)] = 3, // 0
            [Color.FromRgb(6, 6, 6)] = 2, // 6.3413257053849973293806708753612 // Math.Pow(255,1.0/3)
            [Color.FromRgb(40, 40, 40)] = 1, // 40.212411701776533947464222847776 // Math.Pow(255,2.0/3)
            [Color.FromRgb(255, 255, 255)] = 0, // 255  // Math.Pow(255,3.0/3)
        };

        private readonly SpiDevice spi;
        private readonly GpioController gpio;
        private readonly int dc;
        private readonly int reset;
        private readonly int busy;

        public Epd5in79(SpiDevice spi, GpioController gpio, int dc, int reset, int busy)
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

        public void Initialize()
        {
            switch (Mode)
            {
                case DisplayMode.Normal: Init(); break;
                case DisplayMode.Fast: InitFast(); break;
                case DisplayMode.Gray4: InitGray4(); break;
                default: throw new NotSupportedException();
            }
        }

        public void Reset()
        {
            gpio.Write(reset, 1);
            Thread.Sleep(200);
            gpio.Write(reset, 0);
            Thread.Sleep(1);
            gpio.Write(reset, 1);
            Thread.Sleep(200);
        }

        public void Clear(Color color)
        {
            SendCommand(0x24);
            SendData(0xFF, 13600);
            SendCommand(0x26);
            SendData(0x00, 13600);

            SendCommand(0xA4);
            SendData(0xFF, 13600);
            SendCommand(0xA6);
            SendData(0x00, 13600);

            Flush();
        }

        public void Display(Image image)
        {
            if (image is Image<Rgb24> imageRgb24)
            {
                switch (Mode)
                {
                    case DisplayMode.Normal: Display(GetBuffer(imageRgb24)); break;
                    case DisplayMode.Fast: DisplayFast(GetBuffer(imageRgb24)); break;
                    case DisplayMode.Gray4: DisplayGray4(GetBufferGray4(imageRgb24)); break;
                    default: throw new NotSupportedException();
                }
            }
            else
            {
                using var clone = image.CloneAs<Rgb24>();
                Display(clone);
            }
        }

        public void DisplayPartial(Image image, int x, int y)
        {
            throw new NotImplementedException();

            //if (image is Image<Rgb24> imageRgb24)
            //{
            //    switch (Mode)
            //    {
            //        case DisplayMode.Normal:
            //        case DisplayMode.Fast: Display(GetBuffer(imageRgb24), x, y, image.Width, image.Height); break;
            //        default: throw new NotSupportedException();
            //    }
            //}
            //else
            //{
            //    using var clone = image.CloneAs<Rgb24>();
            //    DisplayPartial(clone, x, y);
            //}
        }

        public void Sleep()
        {
            SendCommand(0x10); // deep sleep
            SendData(0x03);

            Thread.Sleep(200);
        }

        public void Dispose()
        {
            Reset();
            Sleep();

            gpio.Dispose();
            spi.Dispose();

            GC.SuppressFinalize(this);
        }

        #region Normal
        private void Init()
        {
            Reset();
            ReadBusy(); // waiting for the electronic paper IC to release the idle signal
            SendCommand(0x12); // POWER ON
            ReadBusy(); // waiting for the electronic paper IC to release the idle signal

            SendCommand(0x11);
            SendData(0x01);

            SendCommand(0x44); //  Set Ram X- address Start / End position
            SendData(0x00); //  XStart, POR = 00h
            SendData(0x31); // 400/8-1
            SendCommand(0x45); //  Set Ram Y- address  Start / End position 
            SendData(0x0f);
            SendData(0x01); // 300-1	
            SendData(0x00); //  YEnd L
            SendData(0x00); //  YEnd H 

            SendCommand(0x4e);
            SendData(0x00);
            SendCommand(0x4f);
            SendData(0x0f);
            SendData(0x01);

            ReadBusy();

            SendCommand(0x91);
            SendData(0x00);

            SendCommand(0xC4); //  Set Ram X- address Start / End position
            SendData(0x31); //  XStart, POR = 00h
            SendData(0x00); // 400/8-1
            SendCommand(0xC5); //  Set Ram Y- address  Start / End position 
            SendData(0x0f);
            SendData(0x01); // 300-1	
            SendData(0x00); // YEnd L
            SendData(0x00); // YEnd H 

            SendCommand(0xCE);
            SendData(0x31);
            SendCommand(0xCF);
            SendData(0x0f);
            SendData(0x01);

            ReadBusy();
        }

        private void Display((byte[] M, byte[] S) buffer)
        {
            SendCommand(0x24, buffer.M);
            SendCommand(0x26, new byte[buffer.M.Length]);

            SendCommand(0xA4, buffer.S);
            SendCommand(0xA6, new byte[buffer.S.Length]);

            Flush();
        }

        private void Flush()
        {
            SendCommand(0x22);
            SendData(0xF7);
            SendCommand(0x20); // DISPLAY REFRESH
            Thread.Sleep(100); // The delay here is necessary, 200uS at least!!!
            ReadBusy(); // waiting for the electronic paper IC to release the idle signal
        }
        #endregion

        #region Fast
        private void InitFast()
        {
            Reset();
            ReadBusy();
            SendCommand(0x12);
            ReadBusy();

            SendCommand(0x18);
            SendData(0x80);

            SendCommand(0x22);
            SendData(0xB1);
            SendCommand(0x20);
            ReadBusy();

            SendCommand(0x1A);
            SendData(0x64);
            SendData(0x00);

            SendCommand(0x22);
            SendData(0x91);
            SendCommand(0x20);
            ReadBusy();

            SendCommand(0x11);
            SendData(0x01);

            SendCommand(0x44);
            SendData(0x00);
            SendData(0x31);
            SendCommand(0x45);
            SendData(0x0f);
            SendData(0x01);
            SendData(0x00);
            SendData(0x00);

            SendCommand(0x4e);
            SendData(0x00);
            SendCommand(0x4f);
            SendData(0x0f);
            SendData(0x01);

            ReadBusy();

            SendCommand(0x91);
            SendData(0x00);

            SendCommand(0xC4);
            SendData(0x31);
            SendData(0x00);
            SendCommand(0xC5);
            SendData(0x0f);
            SendData(0x01);
            SendData(0x00);
            SendData(0x00);

            SendCommand(0xCe);
            SendData(0x31);
            SendCommand(0xCf);
            SendData(0x0f);
            SendData(0x01);

            ReadBusy();
        }

        private void DisplayFast((byte[] M, byte[] S) buffer)
        {
            SendCommand(0x24, buffer.M);
            SendCommand(0x26, new byte[buffer.M.Length]);

            SendCommand(0xA4, buffer.S);
            SendCommand(0xA6, new byte[buffer.S.Length]);

            FlushFast();
        }

        private void FlushFast()
        {
            SendCommand(0x22);
            SendData(0xC7);
            SendCommand(0x20); // DISPLAY REFRESH
            Thread.Sleep(100); // The delay here is necessary, 200uS at least!!!
            ReadBusy(); // waiting for the electronic paper IC to release the idle signal
        }
        #endregion

        #region Gray4
        private static readonly byte[] LUT_DATA_4Gray = [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,

            0x01, 0x4A, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x01, 0x82, 0x42, 0x00, 0x00, 0x10, 0x00,
            0x01, 0x8A, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,

            0x01, 0x41, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x01, 0x82, 0x42, 0x00, 0x00, 0x10, 0x00,
            0x01, 0x81, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,

            0x01, 0x81, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x01, 0x82, 0x42, 0x00, 0x00, 0x10, 0x00,
            0x01, 0x41, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,

            0x01, 0x8A, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x01, 0x82, 0x42, 0x00, 0x00, 0x10, 0x00,
            0x01, 0x4A, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,

            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,

            0x02, 0x00, 0x00,
            0x22, 0x17, 0x41, 0xA8, 0x32, 0x40,
        ];

        private void InitGray4()
        {
            Reset();
            ReadBusy();
            SendCommand(0x12);
            ReadBusy();

            SendCommand(0x0C);
            SendData(0x8B);
            SendData(0x9C);
            SendData(0xA6);
            SendData(0x0F);

            SendCommand(0x3C);
            SendData(0x81);

            ReadBusy();

            SendCommand(0x11);
            SendData(0x01);

            SendCommand(0x44);
            SendData(0x00);
            SendData(0x31);
            SendCommand(0x45);
            SendData(0x0f);
            SendData(0x01);
            SendData(0x00);
            SendData(0x00);

            SendCommand(0x4e);
            SendData(0x00);
            SendCommand(0x4f);
            SendData(0x0f);
            SendData(0x01);

            ReadBusy();

            SendCommand(0x91);
            SendData(0x00);

            SendCommand(0xC4);
            SendData(0x31);
            SendData(0x00);
            SendCommand(0xC5);
            SendData(0x0f);
            SendData(0x01);
            SendData(0x00);
            SendData(0x00);

            SendCommand(0xCe);
            SendData(0x31);
            SendCommand(0xCf);
            SendData(0x0f);
            SendData(0x01);

            Lut();
        }

        private void DisplayGray4((byte[] M1, byte[] M2, byte[] S1, byte[] S2) buffer)
        {
            throw new NotImplementedException();
        }

        private void FlushGray4()
        {
            SendCommand(0x22);
            SendData(0xCF);
            SendCommand(0x20); // DISPLAY REFRESH
            Thread.Sleep(100); // The delay here is necessary, 200uS at least!!!
            ReadBusy(); // waiting for the electronic paper IC to release the idle signal
        }

        private void Lut()
        {
            SendCommand(0x32);
            SendData(LUT_DATA_4Gray[0..227]);
            SendCommand(0x3f);
            SendData(LUT_DATA_4Gray[227]);

            SendCommand(0x03);
            SendData(LUT_DATA_4Gray[228]);

            SendCommand(0x04);
            SendData(LUT_DATA_4Gray[229]);
            SendData(LUT_DATA_4Gray[230]);
            SendData(LUT_DATA_4Gray[231]);

            SendCommand(0x2C);
            SendData(LUT_DATA_4Gray[232]);
        }
        #endregion

        #region Partial
        private bool partialMode = false;

        private void InitPartial()
        {
            Reset();
            ReadBusy();
            SendCommand(0x12);
            ReadBusy();

            SendCommand(0x3C);
            SendData(0x80);
        }

        private void Display(byte[] buffer, int x, int y, int w, int h)
        {
            throw new NotImplementedException();
        }

        private void FlushPartial()
        {
            SendCommand(0x22);
            SendData(0xFF);
            SendCommand(0x20); // DISPLAY REFRESH
            Thread.Sleep(100); // The delay here is necessary, 200uS at least!!!
            ReadBusy(); // waiting for the electronic paper IC to release the idle signal
        }
        #endregion

        #region General
        private void SendCommand(byte command, params byte[] data)
        {
            gpio.Write(dc, 0);
            spi.WriteByte(command);

            if (data?.Length > 0)
            {
                SendData(data);
            }
        }

        private void SendData(byte data, int length)
        {
            var buffer = new byte[length].AsSpan();
            buffer.Fill(data);
            SendData(buffer);
        }

        private void SendData(params byte[] data)
        {
            SendData(data.AsSpan());
        }

        private void SendData(Span<byte> data)
        {
            gpio.Write(dc, 1);
            spi.Write(data);
        }

        private void ReadBusy()
        {
            Debug.WriteLine("e-Paper busy");
            while (gpio.Read(busy) == 1) // 0: idle, 1: busy
            {
                Thread.Sleep(200);
            }
            Debug.WriteLine("e-Paper busy release");
        }

        private void SetWindow(int startX, int startY, int endX, int endY)
        {
            throw new NotImplementedException();
        }

        private void SetCursor(int x, int y)
        {
            throw new NotImplementedException();
        }

        private (byte[] bufferM, byte[] bufferS) GetBuffer(Image<Rgb24> image)
        {
            var byteWidth = Width / 2 / 8 + 1;

            var bufferM = new byte[byteWidth * Height];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width / 2; x++)
                {
                    var index = y * byteWidth + x / 8;
                    var offset = 7 - (x % 8);
                    var color = Color.FromPixel(image[x, y]);
                    var data = (byte)(colorData2[color] << offset);
                    bufferM[index] |= data;
                }
            }

            var bufferS = new byte[(Width / 2 / 8 + 1) * Height];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width / 2; x++)
                {
                    var index = y * byteWidth + (x + 4) / 8;
                    var offset = 8 - (x + 4) % 8 - 1;
                    var color = Color.FromPixel(image[Width / 2 + x, y]);
                    var data = (byte)(colorData2[color] << offset);
                    bufferS[index] |= data;
                }
            }

            return (bufferM, bufferS);
        }

        private static (byte[] bufferM1, byte[] bufferM2, byte[] bufferS1, byte[] bufferS2) GetBufferGray4(Image<Rgb24> image)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}