# DonutChart

状态：待验收。环形图：分段、中心数值、图例，支持悬停抬起、隐藏分段和合并“其他”。事实见[参考卡](../references/components/DonutChart.md)，验证见[验证记录](../verification/2026-11-04-batch10-replication.md)。

## 范围与 API

`Data`（`DonutChartDatum(Key, Label, Value, Color)`）、`Label`、`Unit`、`FormatValue`、`TotalLabel`、`RingSize`、`RingThickness`、`GroupBelow`、`MaxSegments`、`OtherLabel`、`ActiveKey`、`HiddenKeys`、`LegendAction`、`ShowLegend`、`LegendPlacement`、`EmptyLabel`。

## 复用情况

共用 `Motion/ChartKit.cs`（比例尺、曲线、`Timed`、`TimedColor`、`ChartSurface`）、`GestureSpring`、`FrameLoop`、`InViewOnce`、`RollText`。`LegendPlacement`（Auto、Below、Beside）是本项目新增，用来让 Gallery 在 471.6 宽度时对齐 Arc 的上下堆叠示例。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 入场扫开 | 所有分段从顶部一起出发，尾边依次慢 .04s；源码确认 | `Show`、`OnData`（`first` 分支） | `TheRingWaits...`（步进） | 真实节奏未肉眼观察 |
| 悬停抬起 | 外移 4、加粗 2，中心数值滚动；源码确认 | `LiftBy`、`Lift`、`Thicken` | 真实鼠标：donut-hover；`HoverLifts...` | — |
| 隐藏分段 | 原地闭合，其余重分配；至少保留一段；源码确认 | `RowClick`、`ToggleHidden` | 真实点击：donut-hidden（23,770）；`HidingRedistributes...` | — |
| 切换数据 | 旧分段闭合后移除，图例行折叠进“其他”；源码确认 | `OnData`（`Leaving`） | 真实输入：donut-week；`LeavingSegments...` | — |
| 减弱动效 | 直接到位；源码确认 | `Moving` 各分支 | `WithoutMotion...`（Off） | Reduced 档走同一分支，未单独运行 |

## 已知差异

- 图例位置默认 `Auto`，宽度足够时放在环的右侧；`donut-beside` 已截图确认。
- 前四段用新增的 `Series1..4` 令牌，之后的分段走其他配色。
