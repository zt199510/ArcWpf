# ElasticSlider 参考卡

## 来源与固定范围

- 页面：Arc 免费 Registry 的 `elastic-slider`（MIT），2026-11-03 冻结。
- 快照：`tests/Baselines/References/2026-11-03-elastic-slider/`。
- 批次：2026-11-03 第八批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 尺寸 | 行高 44，轨道 8，滑块 24，刻度 4 | 源码确认 | 一致 |
| 标签 | 最小高 28，内边距 12，距滑块 10 | 源码确认 | 一致 |
| 主示例 | Volume 64%；Playback speed .25–2.5 步长 .05；档位 .5/1/1.5/2 | 运行观察：`elastic-slider.txt` | 一致 |
| 纵向 | Volume y24，Speed y135.6，说明 y231.2，总高 248 | 运行观察 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/ElasticSlider.md)。回归：`tests/ArcWpf.Tests/ElasticSliderTests.cs`，Gallery 页 `ElasticSliderPage`。
