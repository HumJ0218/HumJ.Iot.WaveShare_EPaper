using SixLabors.ImageSharp;

namespace HumJ.Iot.WaveShare_EPaper.Base
{
    public interface IEPaper : IDisposable
    {
        int Width { get; }
        int Height { get; }
        Color[] Palette { get; }

        void Initialize();
        void Reset();
        void Sleep();

        void Clear(Color color);
        void Display(Image image);
        void DisplayPartial(Image image, Rectangle rectangle);
    }
}
