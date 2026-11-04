# RulerSlider 参考卡

## 来源与固定范围

- 页面：Arc 免费 Registry 的 `ruler-slider`（MIT），2026-11-03 冻结。
- 快照：`tests/Baselines/References/2026-11-03-ruler-slider/`。
- 批次：2026-11-03 第八批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 视口 | 高 72，1px 边框，半径 18，边缘淡出 22%/78% | 源码确认 | 一致 |
| 刻度 | 主 28 α.48，半 20 α.20，小 12 α.20，标签 y36 | 源码确认 | 一致，三层叠加 |
| 指针 | 2×36，下圆角，Accent | 源码确认 | 一致 |
| 读数 | 28/30.8 w500，单位 16，间距 .3em | 运行观察：`ruler-slider.txt` | 一致；替身字体差 |
| 主示例 | Height 120–220，172 cm；Weight 30–150 步长 .5 间距 12，68.5 kg | 运行观察 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/RulerSlider.md)。回归：`tests/ArcWpf.Tests/RulerSliderTests.cs`，Gallery 页 `RulerSliderPage`。
