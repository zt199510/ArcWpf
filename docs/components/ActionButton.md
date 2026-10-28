# ActionButton 组件规格

状态：2026-10-28 按冻结 Registry 源码重做状态链与动效，解决方案构建通过；ActionButtonTests 与实际窗口观察待在 Windows 运行。

## 来源

Arc 官方 Action button 页面：<https://uiarc.dev/components/action-button>（2026-10-28 读取）。来源标记 Free，组件用途是单次异步 Save/Publish/Submit，并在按钮内显示 pending/success 结果。行为、几何与动效参数以冻结的 Registry 源码为准（见[参考卡](../references/components/ActionButton.md)），WPF 实现为项目代码。

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
| Pending | 显示 PendingLabel，箭头向右离场、spinner 圆环进入并旋转；保留焦点，阻止重复操作且不按压；不把 `IsEnabled` 设为 false。 |
| Success | 任务完成立即显示 SuccessLabel，spinner 离场、勾号自绘；按 `ResetAfterMs` 自动回到 Idle；再次点击重新执行。 |
| Error | 清除 Pending/Success，恢复 Label，触发 `ActionFailed`。 |
| Disabled | 使用 Button 原生禁用和 Automation 行为。 |

## WPF 映射与有意差异

- 来源 `onAction` 映射为 `Func<Task> Action`，来源 `onActionError` 映射为 `ActionFailed` 路由事件；不引入 JavaScript Promise 或 Toast 依赖。
- 来源的 `aria-busy`、`aria-disabled` 和 `role=status` 使用 WPF `IsPending`、Automation 名称和原生 Button Peer 保留；专用 LiveRegion Peer 暂缓。
- 文案用 PART_Text/PART_PreviousText 交叉淡入（不复用 Button 的 blur 内容过渡），宽度在 PART_Morph 上以临界阻尼 spring 近似 visualDuration 0.24；CSS cubic-bezier 用自定义 BezierEase 求解。勾号描绘用 StrokeDashOffset 模拟 pathLength。Reduced/Off 由共享动效等级控制。
- 源码为单一实心样式；继承的 `Variant`/`Size` 不改变 ActionButton 外观。

## Gallery 与验收

Gallery 展示自动复位成功、保持成功和失败三种状态。专项测试覆盖异步成功、重复激活、自动复位、失败事件、绑定/属性边界、禁用和卸载。实际窗口需复验鼠标命中、Enter/Space、焦点保持、明暗主题、Reduced/Off 和长文案不裁剪。




