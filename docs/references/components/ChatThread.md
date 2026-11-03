# ChatThread 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/chat-thread（MIT，免费 Registry）。
- 批次：2026-11-03，项目采集标识，非官方版本。快照在 `tests/Baselines/References/2026-11-03-chat-thread/`（Registry 源码、页面文本、许可、manifest 哈希）。
- 用户图片：无。当前页面是否仍同版：未知，Arc 未公开版本号。
- 离屏截图未入库；WPF 对照结果见 [验证记录](../../verification/2026-11-03-batch5-replication.md)。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 文案、内容组合 | CSV export limit 对话及发送后的回复 | DOM | 文案一致；头像改姓名缩写，图片改原创渐变 |
| 初始值与状态 | 滚到底；状态 Read + 读者头像 | 运行观察 | 一致 |
| 控件本体尺寸/圆角 | 卡片 521.6×436 圆角 34；气泡圆角 18/6；输入框高 42 圆角 21 | DOM+CSS | 一致 |
| 内部对齐锚点 | 内容内边距 12/16/14；滚动沟 10；栈最大 78% | CSS+DOM | 一致 |
| 外部间距 | 行距 2 / 14 / 日期后 10；meta 下 6 | CSS | 一致 |

## 状态与动效索引

弹簧与时长以快照内源码和 `motion.txt` 为准，状态矩阵见[规格](../../components/ChatThread.md)。中间帧未逐帧比对。

## WPF差异和回归入口

附件点击通过 `AttachmentInvoked` 交给应用，控件不打开文件。Arc 的聊天输入框没有 @提及、回应是固定表情行，所以未直接使用 MentionInput 和完整 EmojiPicker；复用了 `Avatar`、`ColorEmojiGlyph` 和 `TextColoring`。

回归：`tests/ArcWpf.Tests/ChatThreadTests.cs`。
