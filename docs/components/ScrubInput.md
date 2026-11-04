# ScrubInput

状态：待验收。检查器数字字段：拖动标签调值，或输入算式和单位。事实见[参考卡](../references/components/ScrubInput.md)，验证见[验证记录](../verification/2026-11-03-batch8-replication.md)。实现在 `src/ArcWpf.Controls/ScrubInput/ScrubInput.cs`。

## 范围与 API

`Label`（文字、`ArcDataGridIcon` 或元素）、`Value`（默认双向）、`Minimum/Maximum`（默认无限）、`Step`、`FineStep`、`CoarseStep`、`Precision`、`PixelsPerStep`（2）、`Wrap`、`Unit`（默认双向）、`Units`、`ResetValue`、`ShowTrack`、`Size`（Medium 36 / Small 30）、`UsePointerLock`（true）；事件 `ValueChanged`、`ValueCommitted`（`ScrubInputChangedEventArgs{Value,Source,Unit}`）、`UnitChanged`；公共 `ScrubExpression.Evaluate`。内部 TextBox 自动化为 Spinner + RangeValue，状态如 `40 percent`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 拖标签：3px 后进入拖动，Shift ×10、Alt ×0.1 并显示标签 | `spring.snappy`；源码确认 | `Advance/Tag` | `ScrubStepsByTravelAndSlopHoldsItBack`、`ModifiersChangeTheStepAndShowATag` | 真实窗口未观察 |
| 到达上下限：外壳拉伸、内容偏移，释放回弹 | 上限 12，`physical(.34)`；源码确认 | `Advance/Paint/EndScrub` | `StrainsPastALimitAndSpringsBack`；si-strain 图 | 真实窗口未观察 |
| 指针锁定：隐藏系统光标并读取移动增量，接近屏幕边缘才回绕，结束时回到起点；绘制替身光标并在窗口边缘环绕 | WPF 适配（无 Pointer Lock API） | `Lock`、`ScrubCursorAdorner` | 未在真实窗口运行 | 真实窗口未观察 |
| 输入非法：红边 900ms 并抖动 | `physical(.3)`，-260px/s；源码确认 | `FlagInvalid` | `InvalidEntryRestoresAndFlags`；si-invalid 对照图 | 真实窗口未观察 |
| 值变化：填充 smooth 滑动，拖动时 1:1 | `spring.smooth`；源码确认 | `AimFill` | `TrackNeedsBothLimitsAndFollowsTheValue` | 真实窗口未观察 |
| 切换单位：旧上移新上升，按下缩放 .94 | `spring.snappy`、.12s；源码确认 | `ScrubUnitSwap`、`PressUnit` | `UnitsCycleAndTypedUnitsSwitch`；si-unit 对照图 | 真实窗口未观察 |
| 状态：悬停/聚焦/拖动的边框、背景、标签色 | .16s；源码确认 | `States` | si-focus 对照图 | 真实窗口未观察 |
| 减弱动效：无拉伸，填充直接到位 | 源码确认 | `Reduced` 分支 | 未单独运行 | 真实窗口未观察 |

## 已知差异

- TextBox 不支持字距，输入值没有 -.14px tracking（WPF 适配）。
- 主示例插图是本项目原创内容，不使用 Arc 的照片；对比时遮蔽该区域。
- 拖动中的替身光标、Alt 键吞并和 `SetCursorPos` 必须在真实 Windows 窗口确认。
