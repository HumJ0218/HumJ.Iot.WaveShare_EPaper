using HumJ.Iot.WaveShare_EPaper;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Processing;
using System.Device.Gpio;

namespace Epd10in85Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var gpio = new GpioController();

            var sda = gpio.OpenPin(10, PinMode.Output);
            var scl = gpio.OpenPin(11, PinMode.Output);
            var cem = gpio.OpenPin(8, PinMode.Output);
            var ces = gpio.OpenPin(7, PinMode.Output);
            var reset = gpio.OpenPin(24, PinMode.Output);
            var busy = gpio.OpenPin(23, PinMode.Input);

            Console.WriteLine("Epd10in85");
            var epd = new Epd10in85(sda, scl, cem, ces, reset, busy);
            var bufferP = new byte[epd.Width / 2 * epd.Height / 8];

            Console.WriteLine("Initialize");
            epd.Initialize();

            Console.WriteLine("Clear Black");
            bufferP.AsSpan().Fill(0x00);
            epd.DisplayRaw(bufferP, bufferP);

            Console.WriteLine("Clear White");
            bufferP.AsSpan().Fill(0xFF);
            epd.DisplayRaw(bufferP, bufferP);

            Console.WriteLine("Clear Random");
            Random.Shared.NextBytes(bufferP);
            epd.DisplayRaw(bufferP, bufferP);

            Console.WriteLine("Show Image");
            {
                using var image = Image.Load(new DirectoryInfo("./images").GetFiles()[0].FullName);
            var (bufferM, bufferS) = GetImageBuffer(image);
            }

            Console.WriteLine("InitializePartial");
            epd.InitializePartial();

            Console.WriteLine("Clear Black");
            bufferP.AsSpan().Fill(0x00);
            epd.DisplayPartialRaw(bufferP, bufferP);

            Console.WriteLine("Clear White");
            bufferP.AsSpan().Fill(0xFF);
            epd.DisplayPartialRaw(bufferP, bufferP);

            Console.WriteLine("Clear Random");
            Random.Shared.NextBytes(bufferP);
            epd.DisplayRaw(bufferP, bufferP);

            Console.WriteLine("Sleep");
            epd.Sleep();

            (byte[] bufferM, byte[] bufferS) GetImageBuffer(Image image)
            {
                image.Mutate(ctx => {
                    ctx.Resize(epd.Width,epd.Height,)
                });
            }
        }
    }
}