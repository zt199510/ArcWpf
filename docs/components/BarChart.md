# BarChart

状态：待验收。柱状图：标题数值随擦洗变化，含均值线、自适应坐标轴和缩放式区间切换。事实见[参考卡](../references/components/BarChart.md)，验证见[验证记录](../verification/2026-11-04-batch10-replication.md)。

## 范围与 API

`Data`（`BarChartDatum(Key, Label, Value, AxisLabel)`）、`Label`、`Period`、`Unit`、`AverageLabel`、`ValueLabel`、`CategoryLabel`、`ShowAverage`、`PlotHeight`、`FormatValue`。

## 复用情况

共用 `Motion/ChartKit.cs`（比例尺、曲线、`Timed`、`TimedColor`、`ChartSurface`）、`GestureSpring`、`FrameLoop`、`InViewOnce`、`RollText`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 入场生长 | 逐根错开，步长 min(.04, .36/n)，morph 弹簧；源码确认 | `Grower`、`Show` | `BarsWaitToBeSeen...`（步进） | 真实节奏未肉眼观察 |
| 均值线 | 柱子开始后 .12s 淡入并升起；源码确认 | `Mean`、`Show` | `BarsWaitToBeSeen...` | — |
| 擦洗 | 游标 Instant .12s，标题滚动；源码确认 | `SetActive`、`_cursorFade`、`Headline` | 真实鼠标：bar-hover；`Headline...` | — |
| 区间切换 | 围绕最新一根缩放，刻度与轴标签交叉淡化；源码确认 | `OnData`、`Aim`、`Leave` | 真实输入：bar-mid、bar-month；`ANewRange...` | — |
| 键盘 | 方向键、Home/End、PageUp/PageDown、Esc；源码确认 | `KeyScrub`、`Press` | 真实按键；`KeysWalk...` | — |
| 减弱动效 | 直接到位；源码确认 | `Moving` 各分支 | `WithoutMotion...`（Off） | Reduced 档走同一分支，未单独运行 |

## 已知差异

- RollingNumber 逐位错开未实现，整段用 `RollText` 滚动（WPF 适配）。
- 修复记录：数据在 Loaded 前赋值时，柱子曾直接到满高而跳过入场，已修复并由 `BarsWaitToBeSeen...` 覆盖。
