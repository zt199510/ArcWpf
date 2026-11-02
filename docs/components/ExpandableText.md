# ExpandableText

状态：已按线上页面重做，专项通过；离屏对照见 [像素对照记录](../verification/2026-11-02-theme-toggle-actions-pixel.md)。真实窗口测量、长文本、DPI 和 Reduced motion 待验收。

来源：Arc 免费组件 [ExpandableText](https://uiarc.dev/components/expandable-text)，冻结批次 `tests/Baselines/References/2026-11-02-expandable-text/`。

公开契约：`Lines`、`IsExpanded`、`MoreLabel`、`LessLabel`。内容按行数折叠，溢出时显示 Read more；展开后显示 Show less；Enter/Space 激活，内容仍在辅助技术树中。WPF 用 `ExpandableTextClip`（Decorator）按无限高度测量子元素，一个 `Progress` 值同时驱动高度、遮罩和箭头；曲线 .32,.5,.2,1，时长随距离变化。末行淡出与线上相同：`calc(100% - 1.6lh)` 到 4% 不透明；遮罩必须用绝对坐标，相对坐标会被裁剪掉的子元素高度摊薄。宽度改变后重新测量。
