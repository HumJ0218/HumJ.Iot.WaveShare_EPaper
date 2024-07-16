using HumJ.Iot.WaveShare_EPaper.Base;
using System.Device.Gpio;
using System.Device.Spi;

namespace HumJ.Iot.WaveShare_EPaper
{
    public class Epd7in3e : Epd7InchMultipleColor
    {
        public static SpiConnectionSettings SpiConnectionSettings => new(0, 0)
        {
            Mode = SpiMode.Mode0,
            DataBitLength = 8,
            ClockFrequency = 10000000,
            DataFlow = DataFlow.MsbFirst,
            ChipSelectLineActiveState = 0,
        };

        public override int Width => 800;

        public override int Height => 480;

        public Epd7in3e(SpiDevice spi, GpioController gpio, int dc, int reset, int busy) : base(spi, gpio, dc, reset, busy)
        {
            PaletteCommand[0x000000] = 0; // black
            PaletteCommand[0xFFFFFF] = 1; // white
            PaletteCommand[0xFFFF00] = 2; // yellow
            PaletteCommand[0xFF0000] = 3; // red
            PaletteCommand[0xFF7F00] = 4; // orange
            PaletteCommand[0x0000FF] = 5; // blue
            PaletteCommand[0x00FF00] = 6; // green
            //PaletteCommand[0xFFFFFF] = 7; // clear
        }
    }
}