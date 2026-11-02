# ActionSwap

状态：已实现，专项 2/2 通过；真实窗口异步状态、动效、主题和 DPI 待验收。

来源：Arc 免费组件 [ActionSwap](https://uiarc.dev/components/action-swap)，冻结批次 `tests/Baselines/References/2026-11-02-action-swap/`。

公开契约：`States`、`Value`、`OnAction`、`FailureLabel`。按压后进入工作态，异步成功切换目标状态，失败显示失败文案；`HoldFor` 后回到首状态。WPF 使用 Button 键盘/命令路径，动画受 `ArcMotion` 控制。
