using HumJ.Iot.WaveShare_EPaper;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using System.Device.Gpio;
using System.Device.Spi;

Console.WriteLine("Hello, World!");
var gpio = new GpioController();

Console.WriteLine("初始化电子纸显示器");
var epd = new Epd4in0e(SpiDevice.Create(Epd4in0e.SpiConnectionSettings), gpio, 25, 17, 24);
var pwr = gpio.OpenPin(18, PinMode.Output, 1);
epd.Initialize();

Console.WriteLine("显示图像");
var bytes = File.ReadAllBytes(args.Length > 0 ? args[0] : "test.png");
var image = Image.Load<Rgb24>(bytes);

image.Mutate(ctx =>
{
    if (image.Width > image.Height)
    {
        ctx.Rotate(90);
    }

    ctx.Resize(new ResizeOptions
    {
        Size = new Size(epd.Width, epd.Height),
        Mode = ResizeMode.Crop,
    });
    ctx.Dither(ErrorDither.FloydSteinberg, epd.Palette);
});

epd.Display(image);

Console.WriteLine("休眠");
epd.Sleep();
pwr.Write(PinValue.Low);