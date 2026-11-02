# ExpandableText

状态：已实现，专项 2/2 通过；真实窗口测量、长文本、主题、DPI 和 Reduced motion 待验收。

来源：Arc 免费组件 [ExpandableText](https://uiarc.dev/components/expandable-text)，冻结批次 `tests/Baselines/References/2026-11-02-expandable-text/`。

公开契约：`Lines`、`IsExpanded`、`MoreLabel`、`LessLabel`。内容按行数折叠，溢出时显示 Read more；展开后显示 Show less；Enter/Space 激活，内容仍在辅助技术树中。WPF 使用 ContentPresenter 和 MaxHeight 过渡，窗口宽度改变后重新测量。
