using HumJ.Iot.WaveShare_EPaper;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
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

            ////Console.WriteLine("Clear Black");
            ////bufferP.AsSpan().Fill(0x00);
            ////epd.DisplayRaw(bufferP, bufferP);

            ////Console.WriteLine("Clear White");
            ////bufferP.AsSpan().Fill(0xFF);
            ////epd.DisplayRaw(bufferP, bufferP);

            ////Console.WriteLine("Clear Random");
            ////Random.Shared.NextBytes(bufferP);
            ////epd.DisplayRaw(bufferP, bufferP);

            if (args.Length > 0) {
                var fi=new FileInfo(args[0]);

                Console.WriteLine($"Show Image {fi.Name}");
                using var image = Image.Load<Rgb24>(fi.FullName);
                var (bufferM, bufferS) = GetImageBuffer(image);
                epd.DisplayRaw(bufferM, bufferS);
            }
            else
            {
                foreach (var fi in new DirectoryInfo("./images").GetFiles())
                {
                    Console.WriteLine($"Show Image {fi.Name}");
                    using var image = Image.Load<Rgb24>(fi.FullName);
                    var (bufferM, bufferS) = GetImageBuffer(image);
                    epd.DisplayRaw(bufferM, bufferS);
                }
            }

            //Console.WriteLine("InitializePartial");
            //epd.InitializePartial2();

            ////Console.WriteLine("Clear White");
            ////bufferP.AsSpan().Fill(0xFF);
            ////epd.DisplayPartialRaw2(bufferP, bufferP);

            ////Console.WriteLine("Clear Black");
            ////bufferP.AsSpan().Fill(0x00);
            ////epd.DisplayPartialRaw2(bufferP, bufferP);

            ////Console.WriteLine("Clear Random");
            ////Random.Shared.NextBytes(bufferP);
            ////epd.DisplayRaw(bufferP, bufferP);

            //foreach (var fi in new DirectoryInfo("./images").GetFiles())
            //{
            //    Console.WriteLine($"Show Image {fi.Name}");
            //    using var image = Image.Load<Rgb24>(fi.FullName);
            //    var (bufferM, bufferS) = GetImageBuffer(image);
            //    epd.DisplayPartialRaw2(bufferM, bufferS);
            //}

            Console.WriteLine("Sleep");
            epd.Sleep();

            (byte[] bufferM, byte[] bufferS) GetImageBuffer(Image<Rgb24> image)
            {
                image.Mutate(ctx =>
                {
                    ctx.Resize(new ResizeOptions { Size = new Size(epd.Width, epd.Height), Mode = ResizeMode.Crop });
                    ctx.HistogramEqualization(new HistogramEqualizationOptions
                    {
                        Method = HistogramEqualizationMethod.AdaptiveTileInterpolation,
                        LuminanceLevels = 256,
                        ClipHistogram = true,
                    });
                    ctx.BinaryDither(KnownDitherings.FloydSteinberg, Color.White, Color.Black);
                });

                var bufferM = new byte[epd.Width / 8 * epd.Height / 2];
                var bufferS = new byte[epd.Width / 8 * epd.Height / 2];

                for (var y = 0; y < epd.Height; y++)
                {
                    for (var x = 0; x < epd.Width / 2; x++)
                    {
                        var pixelM = image[x, y];
                        var pixelS = image[x + epd.Width / 2, y];

                        var p = y * epd.Width/2 + x;
                        var i = p / 8;
                        var o = p % 8;

                        if (Color.FromPixel(pixelM) == Color.White) bufferM[i] |= (byte)(1 << (7 - o));
                        if (Color.FromPixel(pixelS) == Color.White) bufferS[i] |= (byte)(1 << (7 - o));
                    }
                }

                return (bufferM, bufferS);
            }
        }
    }
}
