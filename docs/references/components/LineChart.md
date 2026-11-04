# LineChart 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/line-chart；Registry：https://uiarc.dev/r/line-chart.json（MIT，版权 Elia Kuratli），2026-11-04 采集。
- 快照：`tests/Baselines/References/2026-11-04-line-chart/`，含 `manifest.json`、`license.txt`、Registry 源码与 motion 令牌。`registry.txt` SHA-256：ec4a4b7095dab32a40be5f541a87498937ddb72f620784c02f860ae49d5cfd57。
- 演示数据读自公开预览页，放在 `samples/ArcWpf.Gallery/Pages/ChartDemoData.cs`。
- 批次：2026-11-04 第十批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 比例尺 | 从 0 起，数据为负才下探；437→0/200/400/600 | 源码确认 | 一致；`LineScale...` |
| 曲线 | 单调平滑，过每个点且不越过数据 | 源码确认 | 一致；`CurveKeeps...` |
| Tooltip | 标题 + 各序列值，距十字线 14 | 源码确认 | 一致；替身字体致宽度差 |
| 空状态 | `Choose a series to show`、`No data for this range` | 源码确认 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/LineChart.md)。回归：`tests/ArcWpf.Tests/LineChartTests.cs`，Gallery 页 `LineChartPage`。
