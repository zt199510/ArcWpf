# CommentThread 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/comment-thread（MIT，免费 Registry）。
- 批次：2026-11-03，项目采集标识，非官方版本。快照在 `tests/Baselines/References/2026-11-03-comment-thread/`（Registry 源码、页面文本、许可、manifest 哈希）。
- 用户图片：无。当前页面是否仍同版：未知，Arc 未公开版本号。
- 离屏截图未入库；WPF 对照结果见 [验证记录](../../verification/2026-11-03-batch5-replication.md)。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 文案、内容组合 | Hero headline；Jasmine、Emma、Marcus、Daniel 四条 | DOM | 文案一致；头像改姓名缩写 |
| 初始值与状态 | Jasmine 下 2 条回复，Daniel 为第二条根评论；👍 已按下 | DOM | 一致 |
| 控件本体尺寸/圆角 | 根圆角 26，解决后 22；输入框最小高 38 圆角 14 | CSS | 一致 |
| 内部对齐锚点 | 头部 52.2；评论内边距 10；回复轨道左 24、内缩 12 | DOM | 一致；纵向差 0~2 |

## 状态与动效索引

弹簧与时长以快照内源码和 `motion.txt` 为准，状态矩阵见[规格](../../components/CommentThread.md)。中间帧未逐帧比对。

## WPF差异和回归入口

操作按钮默认悬停才显示（CSS `hover:hover`），`AlwaysShowActions` 用于触屏。@提及建议按 Arc 内联展开，不用 MentionInput 的弹层。结构沿用 ChatThread 的工具，但 Arc 两者的尺寸和动效不同，分别实现。

回归：`tests/ArcWpf.Tests/CommentThreadTests.cs`。
