# HoldToConfirm 组件规格

状态：实现完成，HoldToConfirmTests 3/3 通过；实际窗口待人工验收。来源冻结于 `tests/Baselines/References/2026-10-29-hold-to-confirm/`。

## API

`HoldToConfirm : System.Windows.Controls.Button` 保留 WPF Button 的命令、键盘和 UI Automation 语义，并增加：

- `Label`、`ConfirmedLabel`：Idle 与完成态文案。
- `Duration`：按住时长，默认 1200ms。
- `Tone`：`Accent`、`Danger`、`Neutral`。
- `Confirmed`：可选受控完成态；未设置时控件自行保持完成态。
- `IsConfirmed`、`IsHolding`、`Progress`：只读状态。
- `ConfirmCommand` / `ConfirmCommandParameter`：完成后调用。
- `ConfirmedRaised`：完成路由事件；`HoldChanged` 报告按住开始/结束。

## 范围矩阵

| 交付项 | 状态 | 证据/剩余差异 |
|---|---|---|
| 按住填充与完成回调 | 已实现 | HoldToConfirmTests 通过；窗口观感待验收 |
| 提前松开、移出 24 DIP 回退 | 已实现 | OwnedMotion spring；窗口观感待验收 |
| Space/Enter、失焦、禁用 | 已实现 | 专项覆盖部分；窗口焦点待验收 |
| Accent/Danger/Neutral 与明暗主题 | 已实现 | 语义资源；窗口待验收 |
| 确认态勾号、文案与填充层 | 已实现 | WPF 模板适配；图标绘制与来源不同 |
| touch vibration、移动端响应式 | 暂缓 | WPF 桌面首期不模拟 navigator.vibrate 与 640px bottom sheet |

来源 React/Motion 代码仅作为行为和视觉证据，不直接复用；WPF 实现独立保留原生 Button/Automation 能力。


## 2026-10-29 Arc 对齐修正

- 填充层改为固定完整尺寸的反色 face，外层 clip host 按 Progress 改变宽度；不再横向缩放文字和图标。
- 确认文案和图标加入来源对应的淡入/上浮过渡；确认勾号使用 StrokeDashOffset 绘制。
- 提前松开后的快速重新按住从当前可见填充位置继续；开始新 hold 会停止旧回退时钟。
- Tone 颜色同时作用于基础图标/文字和填充层。

