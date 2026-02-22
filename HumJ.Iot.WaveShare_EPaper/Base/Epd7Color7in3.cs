using System.Device.Gpio;
using System.Device.Spi;

namespace HumJ.Iot.WaveShare_EPaper.Base
{
    public abstract class Epd7Color7in3(SpiDevice spi, GpioController gpio, int dc, int reset, int busy) : Epd7Color(spi, gpio, dc, reset, busy)
    {
        public override int Width { get; } = 800;
        public override int Height { get; } = 480;
    }
}