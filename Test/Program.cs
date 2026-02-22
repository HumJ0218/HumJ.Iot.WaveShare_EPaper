using HumJ.Iot.WaveShare_EPaper;
using HumJ.Iot.WaveShare_EPaper.Base;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Device.Gpio;
using System.Device.Spi;
using Timer = System.Timers.Timer;

// 初始化电子纸显示器
var epd = new Epd4in26(SpiDevice.Create(new SpiConnectionSettings(0, 0)
{
    ClockFrequency = 20_000_000,
    Mode = SpiMode.Mode0,
    DataFlow = DataFlow.MsbFirst
}), new GpioController(), 25, 17, 24)
{
    Mode = DisplayMode.Normal
};
epd.Initialize();
epd.Clear(Color.White);

var x0 = 240;
var y0 = 240;
var radius = 200;

// 创建静态背景
var background = new Image<Rgb24>(epd.Width, epd.Height);
background.Mutate(ctx =>
{
    ctx.Clear(Color.White);
    ctx.Draw(new SolidPen(Color.White, 7), new EllipsePolygon(x0, y0, radius));
    ctx.Draw(new SolidPen(Color.Black, 5), new EllipsePolygon(x0, y0, radius));

    for (var i = 0; i < 12; i++)
    {
        var angle = i * 30;
        var x = radius * Math.Cos(angle * Math.PI / 180);
        var y = radius * Math.Sin(angle * Math.PI / 180);
        ctx.DrawLine(new SolidPen(Color.Black, 5), new PointF((float)(x0 - x * 0.9), (float)(y0 - y * 0.9)), new PointF((float)(x0 - x * 1), (float)(y0 - y * 1)));
    }
});

// 预先创建绘图笔
var whitePenHour = new SolidPen(Color.White, 11);
var blackPenHour = new SolidPen(Color.Black, 9);
var whitePenMinute = new SolidPen(Color.White, 11);
var blackPenMinute = new SolidPen(Color.Black, 9);
var whitePenSecond = new SolidPen(Color.White, 3);
var blackPenSecond = new SolidPen(Color.Black, 1);
var whitePenClear = new SolidPen(Color.White, 5);

var timer = new Timer(1000);
timer.Elapsed += (sender, e) =>
{
    var time = DateTime.Now;
    var hour = time.Hour % 12;
    var minute = time.Minute;
    var second = time.Second;

    // 克隆静态背景
    using var canvas = background.Clone();

    // 清除上一秒的秒针
    var lastSecondAngle = (second - 1) * 6;
    var lastSecondX = radius * Math.Cos(lastSecondAngle * Math.PI / 180);
    var lastSecondY = radius * Math.Sin(lastSecondAngle * Math.PI / 180);
    canvas.Mutate(ctx =>
    {
        ctx.DrawLine(whitePenClear, new PointF(x0, y0), new PointF((float)(x0 - lastSecondX * 0.8), (float)(y0 - lastSecondY * 0.8)));
    });

    // 计算当前时间的角度
    var hourAngle = (hour + minute / 60.0 + second / 3600.0) * 30;
    var minuteAngle = (minute + second / 60.0) * 6;
    var secondAngle = second * 6;

    // 绘制时针
    var hourX = radius * Math.Cos(hourAngle * Math.PI / 180);
    var hourY = radius * Math.Sin(hourAngle * Math.PI / 180);
    canvas.Mutate(ctx =>
    {
        ctx.DrawLine(whitePenHour, new PointF(x0, y0), new PointF((float)(x0 - hourX * 0.5), (float)(y0 - hourY * 0.5)));
        ctx.DrawLine(blackPenHour, new PointF(x0, y0), new PointF((float)(x0 - hourX * 0.5), (float)(y0 - hourY * 0.5)));
        ctx.DrawLine(new SolidPen(Color.White, 1), new PointF((float)(x0 - hourX * 0.3), (float)(y0 - hourY * 0.3)), new PointF((float)(x0 - hourX * 0.5), (float)(y0 - hourY * 0.5)));
    });

    // 绘制分针
    var minuteX = radius * Math.Cos(minuteAngle * Math.PI / 180);
    var minuteY = radius * Math.Sin(minuteAngle * Math.PI / 180);
    canvas.Mutate(ctx =>
    {
        ctx.DrawLine(whitePenMinute, new PointF(x0, y0), new PointF((float)(x0 - minuteX * 0.7), (float)(y0 - minuteY * 0.7)));
        ctx.DrawLine(blackPenMinute, new PointF(x0, y0), new PointF((float)(x0 - minuteX * 0.7), (float)(y0 - minuteY * 0.7)));
        ctx.DrawLine(new SolidPen(Color.White, 1), new PointF((float)(x0 - minuteX * 0.5), (float)(y0 - minuteY * 0.5)), new PointF((float)(x0 - minuteX * 0.7), (float)(y0 - minuteY * 0.7)));
    });

    // 绘制秒针
    var secondX = radius * Math.Cos(secondAngle * Math.PI / 180);
    var secondY = radius * Math.Sin(secondAngle * Math.PI / 180);
    canvas.Mutate(ctx =>
    {
        ctx.DrawLine(whitePenSecond, new PointF(x0, y0), new PointF((float)(x0 - secondX * 0.8), (float)(y0 - secondY * 0.8)));
        ctx.DrawLine(blackPenSecond, new PointF(x0, y0), new PointF((float)(x0 - secondX * 0.8), (float)(y0 - secondY * 0.8)));
    });

    // 图像处理（如果必要）
    canvas.Mutate(ctx => ctx.BinaryThreshold(0.5f));

    // 显示更新
    epd.DisplayPartial(canvas, 0, 0);
};
timer.AutoReset = true;
timer.Enabled = true;

// 防止主线程退出
await Task.Delay(Timeout.Infinite);
