# ToastStack 参考基准

来源：[Arc ToastStack](https://uiarc.dev/components/toast-stack)、[冻结批次](../../../tests/Baselines/References/2026-10-26-toast-stack/manifest.json)、[源码快照](../../../tests/Baselines/References/2026-10-26-toast-stack/toast-stack.json)、[CSS](../../../tests/Baselines/References/2026-10-26-toast-stack/toast-stack.module.css)。

| 维度 | Arc事实 | WPF落地 |
|---|---|---|
| 语义 | 非阻塞通知堆栈；最新通知在前 | `ToastStack : ItemsControl`，`Show/Update/Dismiss` 管理队列 |
| 队列 | 默认最多12条，默认同时展示3条；复用ID更新原通知 | `Limit=12`、`VisibleToasts=3`、ID更新同一 `ToastStackItem` |
| 折叠 | 后续卡片按14px露出，缩放步长5%；悬停/焦点展开，间距12px | 自定义 `ToastStackPanel` 负责测量和排列 |
| 类型 | success、info、warning、error、loading | `ToastStackType`；warning/error 默认时长为基础值1.6倍，loading不自动关闭 |
| 卡片 | 416px最大宽度、圆角26、边框1、SurfaceRaised、Floating阴影 | WPF主题复现几何与语义颜色 |
| 交互 | 关闭、动作按钮、Alt+T、悬停暂停、批量关闭 | `ActionCommand`、`Dismiss`、`Hotkey`、悬停/焦点暂停 |
| 动效 | 入场、堆叠展开、退出、拖拽右滑 | 首期实现稳定堆叠与基础过渡；右滑手势暂缓 |

WPF 版本沿用现有 Toast 的单项关闭语义，但不复用 ToastHost 的单成功样式；两者可以并存，避免破坏已通过的 Toast API。
