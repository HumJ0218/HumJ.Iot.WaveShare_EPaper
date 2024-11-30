using HumJ.Iot.WaveShare_EPaper.Base;
using SixLabors.ImageSharp;
using System.Device.Gpio;
using System.Device.Spi;

namespace HumJ.Iot.WaveShare_EPaper
{
    public class Epd7in3f(SpiDevice spi, GpioController gpio, int dc, int reset, int busy) : Epd7in3(spi, gpio, dc, reset, busy)
    {
        public static SpiConnectionSettings SpiConnectionSettings => new(0, 0)
        {
            Mode = SpiMode.Mode0,
            DataBitLength = 8,
            ClockFrequency = 4000000,
            DataFlow = DataFlow.MsbFirst,
            ChipSelectLineActiveState = 0,
        };

        public override Color[] Palette => [.. PaletteCommand.Keys];

        public override Dictionary<Color, byte> PaletteCommand { get; } = new()
        {
            [Color.FromRgb(0x00, 0x00, 0x00)] = 0, // black
            [Color.FromRgb(0xFF, 0xFF, 0xFF)] = 1, // white
            [Color.FromRgb(0x00, 0xFF, 0x00)] = 2, // green
            [Color.FromRgb(0x00, 0x00, 0xFF)] = 3, // blue
            [Color.FromRgb(0xFF, 0x00, 0x00)] = 4, // red
            [Color.FromRgb(0xFF, 0xFF, 0x00)] = 5, // yellow
            [Color.FromRgb(0xFF, 0x7F, 0x00)] = 6, // orange
        };
    }
}