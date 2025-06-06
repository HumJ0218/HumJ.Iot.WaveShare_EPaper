微雪、佳显墨水屏模块驱动

# `不同厂家的驱动板严禁混用！！！`

# 已支持的屏幕型号

## 4.26 英寸，800x480 分辨率，4灰阶、快刷、局刷，SSD1677

解决了原厂代码中局刷必须使用全屏缓冲数据的问题。

解决了原厂代码中连续局刷时，非局刷区域会来回切换的问题。

[EPD4IN26](https://www.waveshare.net/shop/4.26inch-e-Paper.htm)（未实测）

[GDEQ0426T82](https://www.good-display.cn/product/452.html)

## 7.3 英寸，800x480 分辨率，7彩色，E Ink Spectra 6

[EPD7IN3E](https://www.waveshare.net/shop/7.3inch-e-Paper-E.htm)（未实测）

[GDEP073E01](https://www.good-display.cn/product/520.html)

## 7.3 英寸，800x480 分辨率，7彩色，E Ink Gallery ACeP

[EPD7IN3F](https://www.waveshare.net/shop/7.3inch-e-Paper-F.htm)

[GDEY073D46](https://www.good-display.cn/blank7.html?productId=438)

## 13.3 英寸，1200x1600 分辨率，6彩色，E Ink Spectra 6

优化调色盘，更高的对比度与饱和度，更准确的色调。

相较原厂代码，改用硬件 SPI 接口，大幅提高数据传输速度。

如果用于 [EPD13IN3HAT+E](https://www.waveshare.net/shop/13.3inch-e-Paper-HAT-Plus-E.htm)，可以使用 [SoftwareSpi](https://github.com/dotnet/iot/tree/main/src/devices/SoftwareSpi) 回退到软件 SPI。

[EPD13IN3E](https://www.waveshare.net/shop/13.3inch-e-Paper-E.htm)

[GDEP133C02](https://www.good-display.cn/product/503.html)（实测可用微雪驱动板）
