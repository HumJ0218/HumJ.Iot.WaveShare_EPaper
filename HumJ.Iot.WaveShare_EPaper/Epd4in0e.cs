using HumJ.Iot.WaveShare_EPaper.Base;
using SixLabors.ImageSharp;
using System.Device.Gpio;
using System.Device.Spi;

namespace HumJ.Iot.WaveShare_EPaper
{
    public class Epd4in0e(SpiDevice spi, GpioController gpio, int dc, int reset, int busy) : Epd7Color(spi, gpio, dc, reset, busy)
    {
        public override int Width { get; } = 400;
        public override int Height { get; } = 600;

        public static SpiConnectionSettings SpiConnectionSettings => new(0, 0)
        {
            Mode = SpiMode.Mode0,
            DataBitLength = 8,
            DataFlow = DataFlow.MsbFirst,
            ChipSelectLineActiveState = 0,
        };

        public override Dictionary<Color, byte> PaletteCommand { get; } = new()
        {
            [Color.FromRgb(0x00, 0x00, 0x1C)] = 0, // black
            [Color.FromRgb(0xE9, 0xF4, 0xFF)] = 1, // white
            [Color.FromRgb(0xFF, 0xFF, 0x00)] = 2, // yellow
            [Color.FromRgb(0x71, 0x00, 0x05)] = 3, // red
            [Color.FromRgb(0x00, 0x48, 0xA7)] = 5, // blue
            [Color.FromRgb(0x2D, 0x67, 0x2A)] = 6, // green
        };
    }
}
