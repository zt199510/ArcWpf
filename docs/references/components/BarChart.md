# BarChart 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/bar-chart；Registry：https://uiarc.dev/r/bar-chart.json（MIT，版权 Elia Kuratli），2026-11-04 采集。
- 快照：`tests/Baselines/References/2026-11-04-bar-chart/`，含 `manifest.json`、`license.txt`、Registry 源码与 motion 令牌。`registry.txt` SHA-256：142e679514af4aab6cfeee4a69629f7b62887250584f4108d2c3ed45a9bbb463。
- 演示数据读自公开预览页，放在 `samples/ArcWpf.Gallery/Pages/ChartDemoData.cs`。
- 批次：2026-11-04 第十批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 比例尺 | 四行内最小整洁步长，86→0/30/60/90 | 源码确认 | 一致；`BarScale...` |
| 布局 | 柱宽占比 .58，最大 28，圆角 4 | 源码确认 | 一致；`Fill`、`MaxBar`、`Radius` |
| 静止标题 | Daily average 为均值，擦洗时显示 Total | 运行观察 | 一致 |
| 月视图 | 刻度 0–120，轴标签从最新一天往回取 | 运行观察 | 一致；bar-month |

## WPF差异和回归入口

差异见[组件规格](../../components/BarChart.md)。回归：`tests/ArcWpf.Tests/BarChartTests.cs`，Gallery 页 `BarChartPage`。
