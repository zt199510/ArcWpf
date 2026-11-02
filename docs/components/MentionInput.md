# MentionInput

状态：实现中，待真实窗口视觉验收。

来源：`tests/Baselines/References/2026-11-02-mention-input/`。控件继承 WPF `TextBox`，保留原生撤销、选区、中文 IME 和回车语义；当前实现 `@` 人员建议、`#channel`、上下键、Enter/Tab 确认、Escape 关闭、提交事件、结构化 `Mentions`/`MentionAdded`、mention 原子删除与光标跳过，以及按实际 caret 矩形定位并按工作区空间上下翻转的 Popup。镜像 token 背景仍待补齐。
