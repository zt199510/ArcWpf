# UndoToast

状态：Arc Pro，Registry 不可用；依据公开文档、公开页面的显示几何和样式独立实现，专项通过；离屏对照见 [像素对照记录](../verification/2026-11-02-theme-toggle-actions-pixel.md)，真实窗口待验收。

来源：[UndoToast](https://uiarc.dev/components/undo-toast)，冻结批次 `tests/Baselines/References/2026-11-02-undo-toast/`。Registry 返回 404，未复用付费源码。

公开契约：基于 `ItemsControl`。`KeySelector`、`Actions`、`RowShortcuts`、`OnUndo`/`OnCommit`、`NounSingular`/`NounPlural`、`Empty`、`UndoShortcut`、`Label`、`Duration`、`PauseOnHover`、`Viewport`，以及 `Remove`/`Undo`/`Commit` 和移除、恢复、提交事件。提供 `UndoToastRow`（行高 60.2，悬停时元信息换成操作按钮）。提交失败时重开行并提示；撤销失败时保留批次。恢复行的闪烁按公开 CSS：accent 11%，延迟 .12s，时长 1.8s，18% 到峰值。

证据较弱：倒计时环的起点和方向（按顶部、顺时针处理）；演示头像是自绘渐变，未使用 Arc 图片。
