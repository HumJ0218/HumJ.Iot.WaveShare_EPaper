using HumJ.Iot.WaveShare_EPaper.Base;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Device.Gpio;
using System.Device.Spi;

namespace HumJ.Iot.WaveShare_EPaper
{
    public class Epd4in26 : IEpd
    {
        public int Width { get; } = 800;
        public int Height { get; } = 480;
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

        public Epd4in26(SpiDevice spi, GpioController gpio, int dc, int reset, int busy)
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
            gpio.Write(reset, 0);
            Thread.Sleep(10);
            gpio.Write(reset, 1);
            Thread.Sleep(10);
        }

        public void Clear(Color color)
        {
            var buffer = new byte[(Width / 8) * Height].AsSpan();
            buffer.Fill((byte)(colorData2[color] == 0 ? 0x00 : 0xFF));

            SendCommand(0x24);
            SendData(buffer);

            SendCommand(0x26);
            SendData(buffer);

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
            if (image is Image<Rgb24> imageRgb24)
            {
                switch (Mode)
                {
                    case DisplayMode.Normal:
                    case DisplayMode.Fast: Display(GetBuffer(imageRgb24), x, y, image.Width, image.Height); break;
                    default: throw new NotSupportedException();
                }
            }
            else
            {
                using var clone = image.CloneAs<Rgb24>();
                DisplayPartial(clone, x, y);
            }
        }

        public void Sleep()
        {
            SendCommand(0x10); // DEEP_SLEEP
            SendData(0x01);

            Thread.Sleep(100);
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
            // EPD hardware init start
            Reset();
            WaitForIdle();

            SendCommand(0x12); //SWRESET
            WaitForIdle();

            SendCommand(0x18); // use the internal temperature sensor
            SendData(0x80);

            SendCommand(0x0C); //set soft start     
            SendData(0xAE, 0xC7, 0xC3, 0xC0, 0x80);

            SendCommand(0x01);   //      drive output control    
            SendData((byte)((Height - 1) % 256), (byte)((Height - 1) / 256)); //  Y 

            SendData(0x02);

            SendCommand(0x3C);        // Border       Border setting 
            SendData(0x01);

            SendCommand(0x11);       //    data  entry  mode
            SendData(0x01);          //       X-mode  x+ y-    

            SetWindow(0, Height - 1, Width - 1, 0);

            SetCursor(0, 0);
            WaitForIdle();

            partialMode = false;
        }

        private void Display(byte[] buffer)
        {
            if (partialMode)
            {
                Init();
            }

            SendCommand(0x24);
            SendData(buffer);

            SendCommand(0x26);
            SendData(buffer);

            Flush();
        }

        private void Flush()
        {
            SendCommand(0x22); // Display Update Control
            SendData(0xF7);
            SendCommand(0x20); // Activate Display Update Sequence
            WaitForIdle();
        }
        #endregion

        #region Fast
        private void InitFast()
        {
            // EPD hardware init start
            Reset();
            WaitForIdle();

            SendCommand(0x12); //SWRESET
            WaitForIdle();


            SendCommand(0x18); // use the internal temperature sensor
            SendData(0x80);

            SendCommand(0x0C); //set soft start     
            SendData(0xAE, 0xC7, 0xC3, 0xC0, 0x80);

            SendCommand(0x01);   //      drive output control    
            SendData((byte)((Height - 1) % 256), (byte)((Height - 1) / 256)); //  Y 
            SendData(0x02);

            SendCommand(0x3C);        // Border       Border setting 
            SendData(0x01);

            SendCommand(0x11);        //    data  entry  mode
            SendData(0x01);           //       X-mode  x+ y-    

            SetWindow(0, Height - 1, Width - 1, 0);

            SetCursor(0, 0);
            WaitForIdle();

            // TEMP (1.5s)
            SendCommand(0x1A);
            SendData(0x5A);

            SendCommand(0x22);
            SendData(0x91);
            SendCommand(0x20);

            WaitForIdle();

            partialMode = false;
        }

        private void DisplayFast(byte[] image)
        {
            if (partialMode)
            {
                InitFast();
            }

            SendCommand(0x24);
            SendData(image);

            SendCommand(0x26);
            SendData(image);

            FlushFast();
        }

        private void FlushFast()
        {
            SendCommand(0x22); // Display Update Control
            SendData(0xC7);
            SendCommand(0x20); // Activate Display Update Sequence
            WaitForIdle();
        }
        #endregion

        #region Gray4
        private static readonly byte[] LUT_DATA_4Gray = // 112bytes
        [
            0x80, 0x48, 0x4A, 0x22, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x0A, 0x48, 0x68, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x88, 0x48, 0x60, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xA8, 0x48, 0x45, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x07, 0x1E, 0x1C, 0x02, 0x00,
            0x05, 0x01, 0x05, 0x01, 0x02,
            0x08, 0x01, 0x01, 0x04, 0x04,
            0x00, 0x02, 0x00, 0x02, 0x01,
            0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x01,
            0x22, 0x22, 0x22, 0x22, 0x22,
            0x17, 0x41, 0xA8, 0x32, 0x30,
            0x00, 0x00,
        ];

        private void InitGray4()
        {
            // EPD hardware init start
            Reset();
            WaitForIdle();

            SendCommand(0x12); //SWRESET
            WaitForIdle();

            SendCommand(0x18); // use the internal temperature sensor
            SendData(0x80);

            SendCommand(0x0C); //set soft start     
            SendData(0xAE);
            SendData(0xC7);
            SendData(0xC3);
            SendData(0xC0);
            SendData(0x80);

            SendCommand(0x01);   //      drive output control    
            SendData((byte)((Height - 1) % 256), (byte)((Height - 1) / 256)); //  Y 
            SendData(0x02);

            SendCommand(0x3C);        // Border       Border setting 
            SendData(0x01);

            SendCommand(0x11);        //    data  entry  mode
            SendData(0x01);          //       X-mode  x+ y-    

            SetWindow(0, Height - 1, Width - 1, 0);

            SetCursor(0, 0);
            WaitForIdle();

            Lut();

            partialMode = false;
        }

        private void DisplayGray4((byte[] L, byte[] H) buffer)
        {
            if (partialMode)
            {
                InitGray4();
            }

            SendCommand(0x24);
            SendData(buffer.L);

            SendCommand(0x26);
            SendData(buffer.H);

            FlushGray4();
        }

        private void FlushGray4()
        {
            SendCommand(0x22); // Display Update Control
            SendData(0xC7);
            SendCommand(0x20); //  Activate Display Update Sequence
            WaitForIdle();
        }

        private void Lut()
        {
            SendCommand(0x32);
            SendData(LUT_DATA_4Gray.AsSpan(0, 105));

            SendCommand(0x03); //VGH
            SendData(LUT_DATA_4Gray[105]);

            SendCommand(0x04); //
            SendData(LUT_DATA_4Gray[106], LUT_DATA_4Gray[107], LUT_DATA_4Gray[108]); // VSH1 VSH2 VSL

            SendCommand(0x2C); //VCOM Voltage
            SendData(LUT_DATA_4Gray[109]); // 0x1C
        }
        #endregion

        #region Partial
        private bool partialMode = false;

        private void InitPartial()
        {
            Reset();

            SendCommand(0x18); // Temperature Sensor Selection 
            SendData(0x80); // Internal temperature sensor 

            SendCommand(0x3C); //BorderWavefrom
            SendData(0x80);

            SendCommand(0x01);   //      drive output control    
            SendData((byte)((Height - 1) % 256), (byte)((Height - 1) / 256)); //  Y 

            SendCommand(0x11);        //    data  entry  mode
            SendData(0x01);           //       X-mode  x+ y-    

            partialMode = true;
        }

        private void Display(byte[] buffer, int x, int y, int w, int h)
        {
            if (!partialMode)
            {
                InitPartial();
            }

            SetWindow(x, Height - y - 1, x + w - 1, Height - y - h);
            SetCursor(x, Height - y - 1);

            SendCommand(0x24);   //Write Black and White image to RAM
            SendData(buffer);

            //SendCommand(0x26);   //Write Black and White image to RAM
            //SendData(image);

            FlushPartial();

            SendCommand(0x24);   //Write Black and White image to RAM
            SendData(buffer);
        }

        private void FlushPartial()
        {
            SendCommand(0x22); // Display Update Control
            SendData(0xFF);

            SendCommand(0x20); //  Activate Display Update Sequence
            WaitForIdle();
        }
        #endregion

        #region General
        private void SendCommand(byte command)
        {
            gpio.Write(dc, 0);
            spi.WriteByte(command);
        }

        private void SendData(params byte[] data)
        {
            gpio.Write(dc, 1);
            spi.Write(data);
        }

        private void SendData(Span<byte> data)
        {
            gpio.Write(dc, 1);
            spi.Write(data);
        }

        private void WaitForIdle()
        {
            var endTime = DateTime.Now.AddSeconds(5);

            while (DateTime.Now < endTime)
            {
                if (gpio.Read(busy) == 0)
                {
                    return;
                }
            }

            throw new TimeoutException();
        }

        private void SetWindow(int startX, int startY, int endX, int endY)
        {
            SendCommand(0x44); // SET_RAM_X_ADDRESS_START_END_POSITION
            SendData((byte)(startX & 0xFF), (byte)((startX >> 8) & 0x03), (byte)(endX & 0xFF), (byte)((endX >> 8) & 0x03));


            SendCommand(0x45); // SET_RAM_Y_ADDRESS_START_END_POSITION
            SendData((byte)(startY & 0xFF), (byte)((startY >> 8) & 0x03), (byte)(endY & 0xFF), (byte)((endY >> 8) & 0x03));
        }

        private void SetCursor(int x, int y)
        {
            SendCommand(0x4E); // SET_RAM_X_ADDRESS_COUNTER
                               // x point must be the multiple of 8 or the last 3 bits will be ignored
            SendData((byte)(x & 0xFF), (byte)((x >> 8) & 0x03));

            SendCommand(0x4F); // SET_RAM_Y_ADDRESS_COUNTER
            SendData((byte)(y & 0xFF), (byte)((y >> 8) & 0xFF));
        }

        private static byte[] GetBuffer(Image<Rgb24> image)
        {
            // 每个像素 1 位
            var buffer = new byte[image.Width * image.Height / 8];

            // 遍历图像，将每个像素的灰度值存储到 buffer
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    var position = y * image.Width + x;
                    var index = position / 8;
                    var offset = position % 8;

                    var color = image[x, y];
                    var data = colorData2[Color.FromPixel(color)];
                    buffer[index] |= (byte)(data << (7 - offset));
                }
            }

            return buffer;
        }

        private static (byte[] bufferL, byte[] bufferH) GetBufferGray4(Image<Rgb24> image)
        {
            // 每个像素 2 位，0x24 寄存器存储低位，0x26 寄存器存储高位
            var bufferL = new byte[image.Width * image.Height / 8];
            var bufferH = new byte[image.Width * image.Height / 8];

            // 遍历图像，将每个像素的 2 位灰度值分别存储到 bufferH 和 bufferL
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    var color = image[x, y];
                    var data = colorData4[Color.FromPixel(color)];

                    var position = y * image.Width + x;
                    var index = position / 8;
                    var offset = position % 8;

                    var dataL = data & 1;
                    var dataH = data >> 1;

                    bufferL[index] |= (byte)(dataL << (7 - offset));
                    bufferH[index] |= (byte)(dataH << (7 - offset));
                }
            }

            return (bufferL, bufferH);
        }
        #endregion
    }
}