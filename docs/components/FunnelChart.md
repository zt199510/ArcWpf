# FunnelChart

状态：待验收。漏斗图：流式色带、分层或分离视图、流动粒子、步骤列 Tooltip，窄宽度下转为纵向。事实见[参考卡](../references/components/FunnelChart.md)，验证见[验证记录](../verification/2026-11-04-batch10-replication.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`Stages`（`FunnelStage(Key, Label, Value, Values)`）、`Segments`（`FunnelSegment`）、`Label`、`Unit`、`View`、`Flow`、`Orientation`、`PlotHeight`、`FormatValue`、`ActiveIndex`、`EmptyLabel`；事件 `ActiveChanged`。

## 复用情况

共用 `Motion/ChartKit.cs`（比例尺、曲线、`Timed`、`TimedColor`、`ChartSurface`）、`GestureSpring`、`FrameLoop`、`InViewOnce`、`RollText`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 入场生长 | 从中线展开；文档描述 + 显示结果，弹簧参数为近似 | `Seen`、`_growth` | `TheBandGrows...`（步进） | 弹簧曲线未与 Arc 逐帧对比 |
| 视图切换 | 从屏幕形状过渡，分离时间隙 2；显示几何确认 | `Refresh`、`BuildViews` | 真实输入：funnel-split（7.0/193.0）；`SplitOpens...` | — |
| 步骤悬停 | 非当前列 .5，列底高亮，Tooltip 换位；显示结果 | `TipContent`、`Refresh` | 真实鼠标：funnel-hover；`HoverFocuses...` | 替身字体窄，Tooltip 留在右侧，Arc 翻到左侧 |
| 流动粒子 | 约 40 颗活跃；文档描述 + 显示结果 | `FlowFrame`、`Spawn` | 真实运行 39 颗；`FlowStreams...` | 速度与物理为独立近似 |
| 减弱动效 | 直接到位，粒子停止；文档描述 | `Seen`、`OnFlow` | `WithoutMotion...`（Off） | Reduced 档走同一分支，未单独运行 |

## 已知差异

- 内部实现独立，未使用 Pro 源码，粒子速度和入场弹簧只是近似。
- `View` 默认 `total`，也可设为 `split` 或某个分段 key；纵向阈值 420 由显示结果推断。
