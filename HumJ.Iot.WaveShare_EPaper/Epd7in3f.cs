using HumJ.Iot.WaveShare_EPaper.Base;
using System.Device.Gpio;
using System.Device.Spi;

namespace HumJ.Iot.WaveShare_EPaper
{
    public class Epd7in3f : Epd7in3
    {
        public static SpiConnectionSettings SpiConnectionSettings => new(0, 0)
        {
            Mode = SpiMode.Mode0,
            DataBitLength = 8,
            ClockFrequency = 4000000,
            DataFlow = DataFlow.MsbFirst,
            ChipSelectLineActiveState = 0,
        };

        public Epd7in3f(SpiDevice spi, GpioController gpio, int dc, int reset, int busy) : base(spi, gpio, dc, reset, busy)
        {
            PaletteCommand[0x000000] = 0; // black
            PaletteCommand[0xFFFFFF] = 1; // white
            PaletteCommand[0x00FF00] = 2; // green
            PaletteCommand[0x0000FF] = 3; // blue
            PaletteCommand[0xFF0000] = 4; // red
            PaletteCommand[0xFFFF00] = 5; // yellow
            PaletteCommand[0xFF7F00] = 6; // orange
            //PaletteCommand[-1] = 7; // transparent
        }
    }
}