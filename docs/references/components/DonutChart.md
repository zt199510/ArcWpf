# DonutChart 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/donut-chart；Registry：https://uiarc.dev/r/donut-chart.json（MIT，版权 Elia Kuratli），2026-11-04 采集。
- 快照：`tests/Baselines/References/2026-11-04-donut-chart/`，含 `manifest.json`、`license.txt`、Registry 源码与 motion 令牌。`registry.txt` SHA-256：7f5c311af1d74c30af6bac9336f82b35b075a11375106d431daa7a53b49b8d6a。
- 演示数据读自公开预览页，放在 `samples/ArcWpf.Gallery/Pages/ChartDemoData.cs`。
- 批次：2026-11-04 第十批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 合并规则 | 低于 `GroupBelow` 且至少两段才合并 | 源码确认 | 一致；`SmallPartsGroup...` |
| 占比文案 | `41%`、`<1%`、`0%` | 源码确认 | 一致；`ShareText...` |
| 几何 | 分段间隙 3，圆角 4，外边距 7 | 源码确认 | 一致 |
| 中心 | 默认总数；悬停时显示分段名和值 | 运行观察 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/DonutChart.md)。回归：`tests/ArcWpf.Tests/DonutChartTests.cs`，Gallery 页 `DonutChartPage`。
