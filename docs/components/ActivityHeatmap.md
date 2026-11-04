# ActivityHeatmap

状态：待验收。贡献热力图：周列日行的网格、五级颜色、悬停气泡、图例预览与固定、选中环。事实见[参考卡](../references/components/ActivityHeatmap.md)，验证见[验证记录](../verification/2026-11-04-batch10-replication.md)。

## 范围与 API

`Days`（`ActivityDay(Date, Count)`）、`Label`、`Period`、`UnitOne`、`UnitOther`、`Thresholds`、`WeekStart`、`SelectedDate`、`Actions`；事件 `DateChosen`。

## 复用情况

共用 `Motion/ChartKit.cs`（比例尺、曲线、`Timed`、`TimedColor`、`ChartSurface`）、`GestureSpring`、`FrameLoop`、`InViewOnce`、`RollText`。全部单元格画在一个 `ChartSurface` 上，星期列固定，滚动偏移由控件自己维护。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 入场波浪 | 沿对角线错开，透明度 .36s、缩放 spring 令牌 .58s；源码确认 | `Reveal`、`Rebuild` | `CellsWaveInAlongTheDiagonalOnceSeen`（步进） | 真实节奏未肉眼观察 |
| 切换区间 | 颜色 .32s 与缩放沿波浪方向过渡，方向随前后而变；源码确认 | `Rebuild`（`Wave`/`WaveBack`） | 真实输入：heat-sweep、heat-2023 | — |
| 悬停气泡 | 跟随单元格换位，离开后关闭；源码确认 | `Hover`、`PlaceTip` | 真实鼠标：heat-hover；`HoverOpens...` | — |
| 图例预览与固定 | 其他等级降到 .2，Tween.Fast；说明行换文案；源码确认 | `Highlight`、`Pin` | 真实鼠标：heat-legend；`LegendPreview...` | — |
| 减弱动效 | 直接到位；源码确认 | `Reveal`、`Moving` | `WithoutMotionItSettlesAsSoonAsItLoads`（Off） | Reduced 档走同一分支，未单独运行 |

## 已知差异

- 单元格没有逐格自动化节点（WPF 适配）：单表面绘制，键盘焦点和气泡文案由控件维护，读屏只读到整体摘要与当前格。
- 原生横向滚动条换成细滚动轨，支持 Shift+滚轮、触控平移和拖动（WPF 适配）。Arc 的截图上看不到这条轨，所以对比图上有差异。
- `Thresholds` 默认按最大值四等分，与 Arc 一致；Gallery 为对齐演示设为 `[3, 6, 10]`。
- 触控平移未运行。
