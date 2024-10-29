using SixLabors.ImageSharp;

namespace HumJ.Iot.WaveShare_EPaper.Base
{
    /// <summary>
    /// 电子墨水屏接口
    /// </summary>
    public interface IEpd : IDisposable
    {
        /// <summary>
        /// 屏幕宽度
        /// </summary>
        int Width { get; }

        /// <summary>
        /// 屏幕高度
        /// </summary>
        int Height { get; }

        /// <summary>
        /// 调色板
        /// </summary>
        Color[] Palette { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize();

        /// <summary>
        /// 重置
        /// </summary>
        void Reset();

        /// <summary>
        /// 休眠
        /// </summary>
        void Sleep();

        /// <summary>
        /// 清屏
        /// </summary>
        /// <param name="color">清屏的颜色</param>
        void Clear(Color color);

        /// <summary>
        /// 全屏刷新
        /// </summary>
        /// <param name="image">要显示的图片，需要符合屏幕尺寸及颜色</param>
        void Display(Image image);

        /// <summary>
        /// 局部刷新
        /// </summary>
        /// <param name="image">要显示的图片，需要符合屏幕尺寸及颜色</param>
        /// <param name="x">左上角 X 坐标</param>
        /// <param name="y">左上角 Y 坐标</param>
        void DisplayPartial(Image image, int x, int y);
    }
}