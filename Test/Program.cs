using HumJ.Iot.WaveShare_EPaper;
using HumJ.Iot.WaveShare_EPaper.Base;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using System.Device.Gpio;
using System.Device.Spi;

var fonts = new FontCollection();
fonts.AddSystemFonts();
Console.WriteLine(string.Join(Environment.NewLine, fonts.Families.Select(m => m.Name)));
var font = new Font(fonts.Families.First(m => m.Name.Contains("mono", StringComparison.InvariantCultureIgnoreCase)), 16);

var brushBlack = new SolidBrush(Color.Black);
var brushWhite = new SolidBrush(Color.White);

using var spi = SpiDevice.Create(Epd7in3e.SpiConnectionSettings);
using var gpio = new GpioController();
using Epd7in3 epd = args.Contains("-f") ? new Epd7in3f(spi, gpio, dc: 25, reset: 24, busy: 23) : new Epd7in3e(spi, gpio, dc: 25, reset: 24, busy: 23);

if (args.Contains("-t"))
{
    ShowTest();
}
else if (args.Contains("-u"))
{
    while (true)
    {
        try
        {
            Console.Title = ("Download");
            var bytes = new HttpClient().GetByteArrayAsync(Console.ReadLine()).Result;

            Console.Title = ("Load");
            var image = Image.Load(bytes);

            Console.Title = ("ShowImage");
            ShowImage(image);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }
}
else
{
    foreach (var file in Directory.EnumerateFiles("./images").OrderBy(m => Random.Shared.Next()))
    {
        try
        {
            Console.Title = ("Load");
            var image = Image.Load(file);

            Console.Title = ("ShowImage");
            ShowImage(image);

            Console.Title = ("Waiting");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }
}

void ShowTest()
{
    Console.Title = ("Buffer");
    for (var i = 0; i < 16; i++)
    {
        var startAt = epd.Buffer.Length / 16 * i;
        var endAt = epd.Buffer.Length / 16 * (i + 1);

        for (var j = startAt; j < endAt; j++)
        {
            epd.Buffer[j] = (byte)(i << 4 | i);
        }
    }

    Console.Title = ("Initialize");
    epd.Initialize();

    Console.Title = ("Flush");
    epd.Flush();

    Console.Title = ("Sleep");
    epd.Sleep();
}

void ShowImage(Image image)
{
    if (image.Width < image.Height)
    {
        Console.Title = ("Rotate");
        image.Mutate(x => x.Rotate(RotateMode.Rotate90));
    }

    //Console.Title = ("Contrast");
    //image.Mutate(x => x.Contrast(1.25f));

    //Console.Title = ("Saturate");
    //image.Mutate(x => x.Saturate(1.25f));

    Console.Title = ("Resize");
    image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(epd.Width, epd.Height), Mode = ResizeMode.Crop }));
    Console.Title = ("Dither");
    image.Mutate(ctx => ctx.Dither(ErrorDither.FloydSteinberg, epd.Palette));

    Console.Title = ("Initialize");
    epd.Initialize();

    Console.Title = ("Display");
    epd.Display(image);

    Console.Title = ("Sleep");
    epd.Sleep();
}