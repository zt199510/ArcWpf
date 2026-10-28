# ActionButton 组件规格

状态：实现完成，程序化验证 3/3 通过，实际窗口动效已完成一次 Gallery 复核，Web 中间帧逐项对照仍待用户确认。

## 来源

Arc 官方 Action button 页面：<https://uiarc.dev/components/action-button>（2026-10-28 读取）。来源标记 Free，组件用途是单次异步 Save/Publish/Submit，并在按钮内显示 pending/success 结果。来源行为和视觉仅作参考，不复用上游源码。

## API

`ActionButton : Button` 保留 WPF `Command`、`Click`、键盘和 UI Automation 语义，并增加：

- `Label`、`PendingLabel`、`SuccessLabel`：Idle/Pending/Success 文案。
- `Action`：`Func<Task>` 异步操作；Pending 期间阻止重复激活并保留焦点。
- `ResetAfterMs`：成功后回到 Idle 的延迟；`0` 保留成功状态。
- `IsPending`、`IsSuccess`：只读语义状态。
- `ActionFailed`：携带异常的路由事件；失败回到 Idle。

## 状态与交互

| 状态 | 行为 |
|---|---|
| Idle | 显示 Label 和向右箭头，Enter/Space 触发 Action。 |
| Pending | 显示 PendingLabel，箭头向右离场，保留焦点，阻止重复操作；不把 `IsEnabled` 设为 false。 |
| Success | 显示 SuccessLabel 和勾号；按 `ResetAfterMs` 自动回到 Idle。 |
| Error | 清除 Pending/Success，恢复 Label，触发 `ActionFailed`。 |
| Disabled | 使用 Button 原生禁用和 Automation 行为。 |

## WPF 映射与有意差异

- 来源 `onAction` 映射为 `Func<Task> Action`，来源 `onActionError` 映射为 `ActionFailed` 路由事件；不引入 JavaScript Promise 或 Toast 依赖。
- 来源的 `aria-busy`、`aria-disabled` 和 `role=status` 使用 WPF `IsPending`、Automation 名称和原生 Button Peer 保留；专用 LiveRegion Peer 暂缓。
- 来源逐字 blur/letter layout 动效现已复用 Button 的 PART_LabelSlot/PART_Label 内容过渡，并接入箭头离场、勾号绘制和按压缩放；逐笔勾号绘制仍是 WPF 适配差异；Reduced/Off 由共享动效等级控制。

## Gallery 与验收

Gallery 展示自动复位成功、保持成功和失败三种状态。专项测试覆盖异步成功、重复激活、自动复位、失败事件、绑定/属性边界、禁用和卸载。实际窗口需复验鼠标命中、Enter/Space、焦点保持、明暗主题、Reduced/Off 和长文案不裁剪。




