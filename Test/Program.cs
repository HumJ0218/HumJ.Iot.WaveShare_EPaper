using HumJ.Iot.WaveShare_EPaper;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Device.Gpio;
using System.Device.Spi;

Console.WriteLine("Epd13in3e test");

var gpio = new GpioController();
var epd = new Epd13in3e(SpiDevice.Create(new SpiConnectionSettings(0, -1)
{
    ClockFrequency = 20_000_000,
    Mode = SpiMode.Mode0,
}), gpio, dc: 25, rst: 17, busy: 24, csm: 22, css: 27);
epd.Initialize();
Console.WriteLine("EPD initialized");

var image = await LoadImageAsync();
image.Mutate(ctx =>
{
    ctx.Resize(new ResizeOptions { Size = new Size(epd.Width, epd.Height), Mode = ResizeMode.Crop });
    ctx.Dither(KnownDitherings.FloydSteinberg, epd.Palette);
});
image.Save("shown.png");

//epd.Clear();

Console.WriteLine("EPD display image");
epd.Display(image);

Console.WriteLine("EPD display image");
epd.Sleep();

Console.WriteLine("all done");

async Task<Image> LoadImageAsync()
{
    if (args.Length > 0)
    {
        if (Uri.TryCreate(args[0], UriKind.Absolute, out var uri))
        {
            using var httpClient = new HttpClient();
            var bytes = await httpClient.GetByteArrayAsync(uri);
            Console.WriteLine($"Downloaded {bytes.Length} bytes from {uri}");

            return Image.Load(bytes);
        }
        else
        {
            var bytes = await File.ReadAllBytesAsync(args[0]);
            Console.WriteLine($"Read {bytes.Length} bytes from {args[0]}");

            return Image.Load(bytes);
        }
    }
    else
    {
        var image = new Image<Rgb24>(epd.Width, epd.Height);
        for (var i = 0; i < epd.Palette.Length; i++)
        {
            var color = epd.Palette[i];
            var stripHeight = epd.Height / epd.Palette.Length;
            var stripY = i * stripHeight;

            image.Mutate(ctx =>
            {
                ctx.Fill(color, new Rectangle(0, stripY, epd.Width, stripHeight));
            });
        }

        Console.WriteLine("Generated test image");

        return image;
    }
}