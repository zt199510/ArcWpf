# HoldToConfirm 组件规格

状态：已按冻结源码重做（2026-10-31），Linux 环境仅完成编译；HoldToConfirmTests 7 项未运行，实际窗口待人工验收。来源冻结于 `tests/Baselines/References/2026-10-29-hold-to-confirm/`（2026-10-31 复核与线上 registry 一致）。

## API

`HoldToConfirm : System.Windows.Controls.Button` 保留 WPF Button 的键盘和 UI Automation 语义，并增加：

- `Label`、`ConfirmedLabel`（默认 `Done`）：Idle 与完成态文案。
- `Duration`：按住时长，默认 1200ms。
- `Tone`：`Accent`（默认）、`Danger`、`Neutral`。
- `IconData`：前置图标几何（24 单位网格，描边 1.75），默认 lucide trash-2；对应源码 `icon`。
- `Confirmed`：可选受控完成态；`false` 重置，清除后交还控件自管。完成时若受控值仍为 `false`，填充回退（源码“parent declined”）。
- `IsConfirmed`、`IsHolding`、`Progress`：只读状态。
- `ConfirmCommand` / `ConfirmCommandParameter`、`ConfirmedRaised`：完成回调；`HoldChanged`：按住开始/结束。
- `FillBrush`、`OnFillBrush`、`DangerBrush`：主题输入，样式以 DynamicResource 提供。

## 范围矩阵

| 交付项 | 范围 | 实现状态 | 证据 / 剩余差异 |
|---|---|---|---|
| 线性填充、完成回调、提前松开 smooth 弹簧回退、快速重按续填 | 必做 | 已实现 | 源码确认；测试未运行 |
| 移出 24 DIP 取消、Space/Enter 按住、失焦取消、禁用 | 必做 | 已实现 | 源码确认；窗口待验收 |
| 按压缩放 .97（宽 >220 为 .985，snappy 弹簧）作用于整个按钮含填充层 | 必做 | 已实现 | 旧版只缩放 Surface，填充层错位 |
| 文字在填充边缘换色（双 face + 直边裁剪，填充覆盖边框、自带圆角） | 必做 | 已实现 | 源码确认 |
| 完成：文案 popLayout 交叉（上浮 .3em + blur 4→0 / 退场 -.3em + blur 2）、标签宽度 morph 弹簧 .42 | 必做 | 已实现 | 旧版宽度瞬跳、无退场文字 |
| 图标切换：scale .6→1 snappy、opacity/blur .16；勾号 pathLength .32s 延迟 .08 绘制 | 必做 | 已实现 | 旧版勾号 dash 偏移导致不可见 |
| 受控 `Confirmed`：外部完成时填充 smooth 弹簧到满，重置时回退 | 必做 | 已实现 | 旧版瞬跳 |
| Tone：accent/neutral 描边 border-strong，danger 文字/图标 danger、描边 danger 32% 混入 border | 必做 | 已实现 | color-mix(oklab) 以 sRGB 近似 |
| Hover：填充色 6% 混入 surface，.16s 过渡；完成态不显示 hover、光标恢复箭头 | 必做 | 已实现 | 旧版用 SurfaceMuted 且无过渡 |
| 禁用 opacity .5（含填充层）.16s 过渡 | 必做 | 已实现 | |
| 屏幕阅读器提示 “Press and hold for N seconds…”、完成播报 | 必做 | 已实现 | HelpText + LiveRegionChanged 适配 aria-describedby / role=status |
| Reduced：无按压缩放/回退弹簧，文案图标仅淡入淡出；Off：直接终态 | 必做 | 已实现 | |
| touch vibration | 不适用 | — | WPF 桌面无 navigator.vibrate |

来源 React/Motion 代码仅作为行为与视觉证据，未直接复用。弹簧窗口按项目约定取 visualDuration × 2.07。
