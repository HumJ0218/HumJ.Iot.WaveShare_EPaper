using HumJ.Iot.WaveShare_EPaper.Base;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;

namespace HumJ.Iot.WaveShare_EPaper
{
    public class Epd4in26_old : IEpd
    {
        public int Width => 800;
        public int Height => 480;
        public DisplayMode Mode { get; set; } = DisplayMode.Normal;

        public Color[] Palette =>  (Mode == DisplayMode.Gray4 ? colorData4: colorData2).Keys.ToArray();
        private readonly static IReadOnlyDictionary<Color, byte> colorData2 = new Dictionary<Color, byte>
        {
            {Color.Black, 0},
            {Color.White, 1},
        };
        private readonly static IReadOnlyDictionary<Color, byte> colorData4 = new Dictionary<Color, byte>
        {
            {Color.FromRgb(0, 0, 0), 3},
            {Color.FromRgb(6, 6, 6), 2},
            {Color.FromRgb(40, 40, 40), 1},
            {Color.FromRgb(255, 255, 255), 0},
        };

        private readonly SpiDevice spi;
        private readonly GpioController gpio;
        private readonly int dc;
        private readonly int reset;
        private readonly int busy;

        private readonly byte[] buffer = new byte[800 * 480 / 4];

        public Epd4in26_old(SpiDevice spi, GpioController gpio, int dc = 25, int reset = 24, int busy = 23)
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
                case DisplayMode.Normal:
                    EPD_4in26_Init();
                    break;
                case DisplayMode.Fast:
                    EPD_4in26_Init_Fast();
                    break;
                case DisplayMode.Gray4:
                    EPD_4in26_Init_4GRAY();
                    break;
            }
        }
        public void Display(Image image)
        {
            switch (Mode)
            {
                case DisplayMode.Normal:
                    EPD_4in26_Display(buffer.AsSpan(0, LoadPixelBytes(image)));
                    break;
                case DisplayMode.Fast:
                    EPD_4in26_Display_Fast(buffer.AsSpan(0, LoadPixelBytes(image)));
                    break;
                case DisplayMode.Gray4:
                    EPD_4in26_4GrayDisplay(image);
                    break;
            }
        }
        public void DisplayPartial(Image image, Rectangle destination)
        {
            var data = Mode == DisplayMode.Gray4 ? buffer.AsSpan(0, LoadPixelBytesGray4(image)) : buffer.AsSpan(0, LoadPixelBytes(image));
            EPD_4in26_Display_Part(data, destination.X, destination.Y, destination.Width, destination.Height);
        }
        public void Clear(Color color)
        {
            var data = (Mode == DisplayMode.Gray4? colorData4: colorData2)[color];
            byte fill = 0xFF;

            if (Mode == DisplayMode.Gray4)
            {
                for (var i = 0; i < 4; i++)
                {
                    fill |= (byte)(data << (i * 2));
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    fill |= (byte)(data<< i);
                }
            }

            EPD_4in26_Clear(fill);
        }
        public void Dispose()
        {
            gpio.ClosePin(dc);
            gpio.ClosePin(reset);
            gpio.ClosePin(busy);

            gpio.Dispose();
            spi.Dispose();

            GC.SuppressFinalize(this);
        }
        public void Reset()
        {
            EPD_4in26_Reset();
        }
        public void Sleep()
        {
            EPD_4in26_Sleep();
        }

        public enum DisplayMode { Normal, Fast, Gray4 }

        private int LoadPixelBytes(Image image)
        {
            if (image is Image<Rgb24> source)
            {
                buffer.AsSpan().Clear();
                for (var y = 0; y < image.Height; y++)
                {
                    for (var x = 0; x < image.Width; x++)
                    {
                        var color = Color.FromPixel(source[x, y]);
                        var colorByte = color == Color.White ? 0 : 1;
                        buffer[(y * image.Width + x) / 8] |= (byte)(colorByte << (7 - x % 8));
                    }
                }

                return image.Width * image.Height / 8;
            }
            else
            {
                using var clone = image.CloneAs<Rgb24>();
                return LoadPixelBytes(clone);
            }
        }

        #region EPD_4in26.h
        public void EPD_4in26_Init()
        {
            EPD_4in26_Reset();
            Thread.Sleep(100);

            EPD_4in26_ReadBusy();
            EPD_4in26_SendCommand(0x12);  //SWRESET
            EPD_4in26_ReadBusy();

            EPD_4in26_SendCommand(0x18); // use the internal temperature sensor
            EPD_4in26_SendData(0x80);

            EPD_4in26_SendCommand(0x0C); //set soft start     
            EPD_4in26_SendData(0xAE);
            EPD_4in26_SendData(0xC7);
            EPD_4in26_SendData(0xC3);
            EPD_4in26_SendData(0xC0);
            EPD_4in26_SendData(0x80);

            EPD_4in26_SendCommand(0x01);   //      drive output control    
            EPD_4in26_SendData((byte)((Height - 1) % 256)); //  Y  
            EPD_4in26_SendData((byte)((Height - 1) / 256)); //  Y 
            EPD_4in26_SendData(0x02);

            EPD_4in26_SendCommand(0x3C);        // Border       Border setting 
            EPD_4in26_SendData(0x01);

            EPD_4in26_SendCommand(0x11);        //    data  entry  mode
            EPD_4in26_SendData(0x01);           //       X-mode  x+ y-    

            EPD_4in26_SetWindows(0, Height - 1, Width - 1, 0);

            EPD_4in26_SetCursor(0, 0);

            EPD_4in26_ReadBusy();
        }
        public void EPD_4in26_Init_Fast()
        {
            EPD_4in26_Reset();
            Thread.Sleep(100);

            EPD_4in26_ReadBusy();
            EPD_4in26_SendCommand(0x12);  //SWRESET
            EPD_4in26_ReadBusy();

            EPD_4in26_SendCommand(0x18); // use the internal temperature sensor
            EPD_4in26_SendData(0x80);

            EPD_4in26_SendCommand(0x0C); //set soft start     
            EPD_4in26_SendData(0xAE);
            EPD_4in26_SendData(0xC7);
            EPD_4in26_SendData(0xC3);
            EPD_4in26_SendData(0xC0);
            EPD_4in26_SendData(0x80);

            EPD_4in26_SendCommand(0x01);   //      drive output control    
            EPD_4in26_SendData((byte)((Height - 1) % 256)); //  Y  
            EPD_4in26_SendData((byte)((Height - 1) / 256)); //  Y 
            EPD_4in26_SendData(0x02);

            EPD_4in26_SendCommand(0x3C);        // Border       Border setting 
            EPD_4in26_SendData(0x01);

            EPD_4in26_SendCommand(0x11);        //    data  entry  mode
            EPD_4in26_SendData(0x01);           //       X-mode  x+ y-    

            EPD_4in26_SetWindows(0, Height - 1, Width - 1, 0);

            EPD_4in26_SetCursor(0, 0);

            EPD_4in26_ReadBusy();

            //TEMP (1.5s)
            EPD_4in26_SendCommand(0x1A);
            EPD_4in26_SendData(0x5A);

            EPD_4in26_SendCommand(0x22);
            EPD_4in26_SendData(0x91);
            EPD_4in26_SendCommand(0x20);

            EPD_4in26_ReadBusy();
        }
        public void EPD_4in26_Init_4GRAY()
        {
            EPD_4in26_Reset();
            Thread.Sleep(100);

            EPD_4in26_ReadBusy();
            EPD_4in26_SendCommand(0x12);  //SWRESET
            EPD_4in26_ReadBusy();

            EPD_4in26_SendCommand(0x18); // use the internal temperature sensor
            EPD_4in26_SendData(0x80);

            EPD_4in26_SendCommand(0x0C); //set soft start     
            EPD_4in26_SendData(0xAE);
            EPD_4in26_SendData(0xC7);
            EPD_4in26_SendData(0xC3);
            EPD_4in26_SendData(0xC0);
            EPD_4in26_SendData(0x80);

            EPD_4in26_SendCommand(0x01);   //      drive output control    
            EPD_4in26_SendData((byte)((Width - 1) % 256)); //  Y  
            EPD_4in26_SendData((byte)((Width - 1) % 256)); //  Y 
            EPD_4in26_SendData(0x02);

            EPD_4in26_SendCommand(0x3C);        // Border       Border setting 
            EPD_4in26_SendData(0x01);

            EPD_4in26_SendCommand(0x11);        //    data  entry  mode
            EPD_4in26_SendData(0x01);           //       X-mode  x+ y-    

            EPD_4in26_SetWindows(0, Height - 1, Width - 1, 0);

            EPD_4in26_SetCursor(0, 0);

            EPD_4in26_ReadBusy();

            EPD_4in26_Lut();
        }
        public void EPD_4in26_Clear(byte fill = 0xFF)
        {
            var image = new byte[Width / (Mode == DisplayMode.Gray4 ? 4 : 8)].AsSpan();
            image.Fill(fill);

            EPD_4in26_SendCommand(0x24);   //write RAM for black(0)/white (1)
            for (var i = 0; i < Height; i++)
            {
                EPD_4in26_SendData2(image);
            }

            EPD_4in26_SendCommand(0x26);   //write RAM for black(0)/white (1)
            for (var i = 0; i < Height; i++)
            {
                EPD_4in26_SendData2(image);
            }
            EPD_4in26_TurnOnDisplay();
        }
        public void EPD_4in26_Display(Span<byte> data)
        {
            int i;
            int height = Height;
            int width = Width / 8;

            EPD_4in26_SendCommand(0x24);   //write RAM for black(0)/white (1)
            for (i = 0; i < height; i++)
            {
                EPD_4in26_SendData2(data.Slice(i * width, width));
            }
            EPD_4in26_TurnOnDisplay();
        }
        public void EPD_4in26_Display_Base(Span<byte> data)
        {
            int i;
            int height = Height;
            int width = Width / 8;

            EPD_4in26_SendCommand(0x24);   //write RAM for black(0)/white (1)
            for (i = 0; i < height; i++)
            {
                EPD_4in26_SendData2(data.Slice(i * width, width));
            }

            EPD_4in26_SendCommand(0x26);   //write RAM for black(0)/white (1)
            for (i = 0; i < height; i++)
            {
                EPD_4in26_SendData2(data.Slice(i * width, width));
            }
            EPD_4in26_TurnOnDisplay();
        }
        public void EPD_4in26_Display_Fast(Span<byte> data)
        {
            int i;
            int height = Height;
            int width = Width / 8;

            EPD_4in26_SendCommand(0x24);   //write RAM for black(0)/white (1)
            for (i = 0; i < height; i++)
            {
                EPD_4in26_SendData2(data.Slice(i * width, width));
            }
            EPD_4in26_TurnOnDisplay_Fast();
        }
        public void EPD_4in26_Display_Part(Span<byte> data, int x, int y, int w, int h)
        {
            int height = h;
            int width = (w % 8 == 0) ? (w / 8) : (w / 8 + 1);

            EPD_4in26_Reset();

            EPD_4in26_SendCommand(0x18); // use the internal temperature sensor
            EPD_4in26_SendData(0x80);

            EPD_4in26_SendCommand(0x3C);        // Border       Border setting 
            EPD_4in26_SendData(0x80);

            EPD_4in26_SetWindows(x, y, x + w - 1, y + h - 1);

            EPD_4in26_SetCursor(x, y);

            EPD_4in26_SendCommand(0x24);   //write RAM for black(0)/white (1)
            for (var i = 0; i < height; i++)
            {
                EPD_4in26_SendData2(data.Slice(i * width, width));
            }
            EPD_4in26_TurnOnDisplay_Part();
        }
        public void EPD_4in26_4GrayDisplay(Image source)
        {
            if (source is Image<Rgb24> image)
            {

                // 每个像素2位，0x24寄存器存储高位，0x26寄存器存储低位
                var bufferH = new byte[Width * Height / 8];
                var bufferL = new byte[Width * Height / 8];

                // 遍历图像，将每个像素的4位灰度值分别存储到bufferH和bufferL
                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        var position = y * Width + x;
                        var index = position / 8;
                        var offset = position % 8;

                        var color = image[x, y];
                        var data = colorData4[Color.FromPixel(color)];

                        var dataH = data >> 1;
                        var dataL = data & 1;

                        bufferH[index] |= (byte)(dataH << (7 - offset));
                        bufferL[index] |= (byte)(dataL << (7 - offset));
                    }
                }

                // 将数据发送到两个寄存器
                EPD_4in26_SendCommand(0x24);
                EPD_4in26_SendData2(bufferH);

                EPD_4in26_SendCommand(0x26);
                EPD_4in26_SendData2(bufferL);

                // 刷新显示
                EPD_4in26_TurnOnDisplay_4GRAY();
            }
            else
            {
                using var clone = source.CloneAs<Rgb24>();
                EPD_4in26_4GrayDisplay(clone);
            }
        }
        public void EPD_4in26_Sleep()
        {
            EPD_4in26_SendCommand(0x10); //enter deep sleep
            EPD_4in26_SendData(0x03);
            Thread.Sleep(100);
        }
        #endregion

        #region EPD_4in26.c
        private static readonly byte[] LUT_DATA_4Gray =    //112bytes
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
        public void EPD_4in26_Reset()
        {
            gpio.Write(reset, 1);
            Thread.Sleep(100);
            gpio.Write(reset, 0);
            Thread.Sleep(2);
            gpio.Write(reset, 1);
            Thread.Sleep(100);
        }
        public void EPD_4in26_SendCommand(byte reg)
        {
            Console.WriteLine($"EPD_4in26_SendCommand 0x{reg:X2}");

            gpio.Write(dc, 0);
            spi.WriteByte(reg);
        }
        public void EPD_4in26_SendData(params byte[] data)
        {
            Console.WriteLine($"EPD_4in26_SendData {data.Length}");

            gpio.Write(dc, 1);
            spi.Write(data);
        }
        public void EPD_4in26_SendData2(Span<byte> data)
        {
            Console.WriteLine($"EPD_4in26_SendData2 {data.Length}");

            gpio.Write(dc, 1);
            spi.Write(data);
        }
        public void EPD_4in26_ReadBusy()
        {
            Debug.WriteLine("e-Paper busy");
            while (gpio.Read(busy) != 0)
            {    //=1 BUSY
                Thread.Sleep(20);
            }
            Thread.Sleep(20);
            Debug.WriteLine("e-Paper busy release");
        }
        public void EPD_4in26_TurnOnDisplay()
        {
            EPD_4in26_SendCommand(0x22); //Display Update Control
            EPD_4in26_SendData(0xF7);
            EPD_4in26_SendCommand(0x20); //Activate Display Update Sequence
            EPD_4in26_ReadBusy();
        }
        public void EPD_4in26_TurnOnDisplay_Fast()
        {
            EPD_4in26_SendCommand(0x22); //Display Update Control
            EPD_4in26_SendData(0xC7);
            EPD_4in26_SendCommand(0x20); //Activate Display Update Sequence
            EPD_4in26_ReadBusy();
        }
        public void EPD_4in26_TurnOnDisplay_Part()
        {
            EPD_4in26_SendCommand(0x22); //Display Update Control
            EPD_4in26_SendData(0xFF);
            EPD_4in26_SendCommand(0x20); //Activate Display Update Sequence
            EPD_4in26_ReadBusy();
        }
        public void EPD_4in26_TurnOnDisplay_4GRAY()
        {
            EPD_4in26_SendCommand(0x22);
            EPD_4in26_SendData(0xC7);
            EPD_4in26_SendCommand(0x20);
            EPD_4in26_ReadBusy();
        }
        public void EPD_4in26_Lut()
        {
            EPD_4in26_SendCommand(0x32); //vcom
            for (var count = 0; count < 105; count++)
            {
                EPD_4in26_SendData(LUT_DATA_4Gray[count]);
            }

            EPD_4in26_SendCommand(0x03); //VGH      
            EPD_4in26_SendData(LUT_DATA_4Gray[105]);

            EPD_4in26_SendCommand(0x04); //      
            EPD_4in26_SendData(LUT_DATA_4Gray[106]); //VSH1   
            EPD_4in26_SendData(LUT_DATA_4Gray[107]); //VSH2   
            EPD_4in26_SendData(LUT_DATA_4Gray[108]); //VSL   

            EPD_4in26_SendCommand(0x2C);     //VCOM Voltage
            EPD_4in26_SendData(LUT_DATA_4Gray[109]);    //0x1C
        }
        public void EPD_4in26_SetWindows(int x1, int y1, int x2, int y2)
        {
            EPD_4in26_SendCommand(0x44); // SET_RAM_X_ADDRESS_START_END_POSITION
            EPD_4in26_SendData((byte)(x1 & 0xFF));
            EPD_4in26_SendData((byte)((x1 >> 8) & 0x03));
            EPD_4in26_SendData((byte)(x2 & 0xFF));
            EPD_4in26_SendData((byte)((x2 >> 8) & 0x03));

            EPD_4in26_SendCommand(0x45); // SET_RAM_Y_ADDRESS_START_END_POSITION
            EPD_4in26_SendData((byte)(y1 & 0xFF));
            EPD_4in26_SendData((byte)((y1 >> 8) & 0x03));
            EPD_4in26_SendData((byte)(y2 & 0xFF));
            EPD_4in26_SendData((byte)((y2 >> 8) & 0x03));
        }
        public void EPD_4in26_SetCursor(int x, int y)
        {
            EPD_4in26_SendCommand(0x4E); // SET_RAM_X_ADDRESS_COUNTER
            EPD_4in26_SendData((byte)(x & 0xFF));
            EPD_4in26_SendData((byte)((x >> 8) & 0x03));

            EPD_4in26_SendCommand(0x4F); // SET_RAM_Y_ADDRESS_COUNTER
            EPD_4in26_SendData((byte)(y & 0xFF));
            EPD_4in26_SendData((byte)((y >> 8) & 0x03));
        }
        #endregion
    }
}