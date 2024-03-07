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

    epd.ColorMap[Color.Parse("#2A282B")]= Epd7in3fColor.Black;
    epd.ColorMap[Color.Parse("#BDBDBD")]= Epd7in3fColor.White;
    epd.ColorMap[Color.Parse("#bbb926")]= Epd7in3fColor.Yellow;
    epd.ColorMap[Color.Parse("#9f5d31")]= Epd7in3fColor.Orange;
    epd.ColorMap[Color.Parse("#527d21")]= Epd7in3fColor.Green;
    epd.ColorMap[Color.Parse("#733b3a")]= Epd7in3fColor.Red;
    epd.ColorMap[Color.Parse("#344269")]= Epd7in3fColor.Blue;

    epd.Initialize();
    //epd.Clear();

    // Show images
    _ = Task.Run(() =>
    {
        while (running)
        {
            var files = new DirectoryInfo("./image").GetFiles("*.png").Concat(new DirectoryInfo("./image").GetFiles("*.jpg")).ToArray();
            if (files.Any())
            {
                var fi = files[new Random().Next(files.Length)];
                try
                {
                    var image = Image.Load(fi.FullName);
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
