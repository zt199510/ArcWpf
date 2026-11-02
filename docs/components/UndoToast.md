# UndoToast

状态：公开页面可读，Arc Pro Registry 不可用；按公开 API 独立 WPF 实现，专项 2/2 通过，真实窗口待验收。

来源：[UndoToast](https://uiarc.dev/components/undo-toast)，冻结批次 `tests/Baselines/References/2026-11-02-undo-toast/`。Registry 返回 404，未复用付费源码。

公开契约：`Remove`、`Undo`、`Commit`、`Duration`、`PauseOnHover`、`ToastPlacement`，以及移除/恢复/提交事件。WPF 首期保留批次撤销、倒计时、悬停暂停和 Ctrl+Z；行高度弹簧、完整行渲染和服务端失败回滚作为后续扩展。
