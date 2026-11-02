# ActionSwap

状态：已按线上页面重做，专项通过；离屏对照见 [像素对照记录](../verification/2026-11-02-theme-toggle-actions-pixel.md)。真实窗口异步状态、动效、主题和 DPI 待验收。

来源：Arc 免费组件 [ActionSwap](https://uiarc.dev/components/action-swap)，冻结批次 `tests/Baselines/References/2026-11-02-action-swap/`。

公开契约：`States`、`Value`、`OnAction`、`FailureLabel`、`Size`（高 36/44/50）。状态的 `Icon` 是路径字符串并带 `IconStroke`，`Tone` 对应成功/警告/危险色；文字逐字母变形，宽度、图标和折叠共用弹簧，工作态最短保持 450ms，失败保持 1800ms。按压后进入工作态，异步成功切换目标状态，失败显示失败文案；`HoldFor` 后进入下一状态。WPF 基于 `ButtonBase` 的键盘/命令路径，动画受 `ArcMotion` 控制。
