# FloatingButtonGroup

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/FloatingButtonGroup.md)，验证见[验证记录](../verification/2026-11-03-batch6-replication.md)。

## 范围与 API

`Items`（`FloatingButtonGroupItem`：`Id`、`IsSeparator`、`IconOnly`、`Shortcut`、`ReserveLabels`、`Label`、`Icon`、`IsDisabled`、`IsPressed`）、`Label`、`Variant`（Muted/Floating）、`Size`（Small/Medium）、`Orientation`、`IconOnly`、`TooltipSide`；事件 `ItemInvoked`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 悬停移动 | 共享高亮在按钮间形变（Registry） | `FloatingButtonGroup` 高亮弹簧 | 测试+截图 fbg-hover | 未逐帧比对 |
| 离开 | 高亮淡出 | 同上 | 测试 | 未逐帧比对 |
| 按下/切换 | 高亮收缩；切换项保留淡色 | `ToolbarButton`、高亮状态 | 截图 fbg-present | 高亮叠放次序见参考卡 |
| 文案切换 | `ReserveLabels` 占位，宽度不变 | 按钮内容布局 | 测试 | 无 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
