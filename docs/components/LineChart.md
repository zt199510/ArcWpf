# LineChart

状态：待验收。折线图：多序列、平滑或折线曲线、十字线 Tooltip、图例开关和区间切换形变。事实见[参考卡](../references/components/LineChart.md)，验证见[验证记录](../verification/2026-11-04-batch10-replication.md)。

## 范围与 API

`Data`（`LineChartDatum`）、`Series`（`LineChartSeries(Key, Label, Color, Dashed, Area)`）、`Label`、`Unit`、`PlotHeight`、`FormatValue`、`FormatTick`、`HiddenSeries`、`IsLoading`、`EmptyLabel`、`ShowLegend`、`CategoryLabel`、`Curve`；事件 `ActiveChanged`。

## 复用情况

共用 `Motion/ChartKit.cs`（比例尺、曲线、`Timed`、`TimedColor`、`ChartSurface`）、`GestureSpring`、`FrameLoop`、`InViewOnce`、`RollText`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 入场描线 | Considered×1.75（.84s），InOut；源码确认 | `StartDraw`、`_draw` | `DrawsOnceSeen...`、`NothingDraws...`（步进） | 真实节奏未肉眼观察 |
| Tooltip | 跟随十字线，靠近右缘翻到左侧；源码确认 | `Tip` | 真实鼠标：line-hover、line-flip；`TooltipFlips...` | — |
| 区间切换形变 | 从屏幕上的当前形状过渡；源码确认 | `OnData` | 真实输入：line-7d、line-90d；`NewDataMorphs...` | — |
| 隐藏序列 | 压平并重算比例尺（600→300）；源码确认 | `ToggleSeries` | 真实点击：line-hidden；`HidingASeries...` | — |
| 减弱动效 | 直接到位；源码确认 | `Moving` 各分支 | `WithoutMotion...`（Off） | Reduced 档走同一分支，未单独运行 |

## 已知差异

- Arc 的 LineChart 没有用户缩放手势。任务里的“缩放”在这里对应区间切换形变和比例尺弹簧；刷选图是 Arc 的另一个组件，未包含在本批。
