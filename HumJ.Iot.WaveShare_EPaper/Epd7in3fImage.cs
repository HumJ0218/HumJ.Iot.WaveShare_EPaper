using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace HumJ.Iot.WaveShare_EPaper
{
    public static class Epd7in3fImage
    {
        public static IDictionary<int, Epd7in3fColor> ColorMap { get; } = new Dictionary<int, Epd7in3fColor>
        {
            [0x000000] = Epd7in3fColor.Black,
            [0xFFFFFF] = Epd7in3fColor.White,
            [0x00FF00] = Epd7in3fColor.Green,
            [0x0000FF] = Epd7in3fColor.Blue,
            [0xFF0000] = Epd7in3fColor.Red,
            [0xFFFF00] = Epd7in3fColor.Yellow,
            [0xFF8000] = Epd7in3fColor.Orange,
        };

        public static void ShowImage(this Epd7in3f epd, Image<Rgb24> image)
        {
            var buffer = new byte[Epd7in3f.HEIGHT * Epd7in3f.WIDTH / 2];

            for (var i = 0; i < buffer.Length; i++)
            {
                var pixelIndex = i * 2;
                var x = pixelIndex % Epd7in3f.WIDTH;
                var y = pixelIndex / Epd7in3f.WIDTH;

                var dataH = GetPixelColorByte(image[x, y]);
                var dataL = GetPixelColorByte(image[x + 1, y]);

                var data = (dataH << 4) + dataL;
                buffer[i] = (byte)data;
            }

            lock (epd.locker)
            {
                epd.SendCommand(0x10);
                epd.SendData(buffer);

                epd.Flush();
            }
        }

        private static byte GetPixelColorByte(Rgb24 rgb24)
        {
            var r = rgb24.R;
            var g = rgb24.G;
            var b = rgb24.B;

            var rgb = (r << 16) + (g << 8) + b;
            var color = ColorMap[rgb];

            return (byte)color;
        }
    }
}