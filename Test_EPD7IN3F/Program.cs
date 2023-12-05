using HumJ.Iot.WaveShare_EPaper;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Device.Gpio;
using System.Device.Spi;

try
{
    var running = true;

    Console.WriteLine("epd7in3f Demo");
    var gpio = new GpioController();
    var spi = SpiDevice.Create(Epd7in3f.SpiConnectionSettings);

    Console.WriteLine("init and Clear");
    var epd = new Epd7in3f(gpio, spi);

    Epd7in3fImage.ColorMap.Add(0x2A282B, Epd7in3fColor.Black);
    Epd7in3fImage.ColorMap.Add(0xBDBDBD, Epd7in3fColor.White);
    Epd7in3fImage.ColorMap.Add(0xbbb926, Epd7in3fColor.Yellow);
    Epd7in3fImage.ColorMap.Add(0x9f5d31, Epd7in3fColor.Orange);
    Epd7in3fImage.ColorMap.Add(0x527d21, Epd7in3fColor.Green);
    Epd7in3fImage.ColorMap.Add(0x733b3a, Epd7in3fColor.Red);
    Epd7in3fImage.ColorMap.Add(0x344269, Epd7in3fColor.Blue);

    epd.Initialize();
    //epd.Clear();

    // Show images
    _ = Task.Run(() =>
    {
        while (running)
        {
            var files = new DirectoryInfo("./image").GetFiles("*.png");
            if (files.Length != 0)
            {
                var fi = files[new Random().Next(files.Length)];
                try
                {
                    var image = Image.Load<Rgb24>(fi.FullName);
                    if (image.Width >= Epd7in3f.WIDTH && image.Height >= Epd7in3f.HEIGHT)
                    {
                        Console.WriteLine("ShowImage " + fi);
                        epd.ShowImage(image);
                        Thread.Sleep(30000);
                        fi.MoveTo("./image_shown/" + fi.Name, true);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                    Thread.Sleep(1000);
                }
            }
            else
            {
                Console.Error.WriteLine("waiting...");
                Thread.Sleep(1000);
            }
        }
    });

    Console.ReadLine();
    running = false;

    Console.WriteLine("Clear...");
    epd.Clear();

    Console.WriteLine("Goto Sleep...");
    epd.Sleep();

    epd.Dispose();
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
}
