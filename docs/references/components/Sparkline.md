# Sparkline 参考卡

## 来源与固定范围

- 页面：Arc 免费 Registry 的 `sparkline`（MIT），2026-11-03 冻结。
- 快照：`tests/Baselines/References/2026-11-03-sparkline/`。
- 批次：2026-11-03 第八批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 绘图 | 线宽 2.5，面积 9%，基线 1px，内缩 5 | 源码确认 | 一致 |
| 标题 | 标签 14，值 18/25.2 w500，旁注 12/16.8 w500 | 运行观察：`sparkline.txt` | 一致；替身字体差 |
| 主示例 | 360 宽，viewBox 360×101.647，1W/1M/1Y | 运行观察 | 一致 |
| 查看样例 | 2,376 · Sep 17 | 运行观察：sp-scrub | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/Sparkline.md)。回归：`tests/ArcWpf.Tests/SparklineTests.cs`，Gallery 页 `SparklinePage`。
