# StreamingMessage

状态：待验收。流式助手消息：按整词揭示、只淡入新片段、可停止和重试；另有 `StreamingScrollHost` 负责贴底跟随。事实见[参考卡](../references/components/StreamingMessage.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`Text`、`Append()`、`Status`（Thinking/Streaming/Done/Stopped/Error）、`Error`、`ModelName`、`ModelMark`、`Sources`、`CanStop`、`CanRetry`、`ShowFeedback`、`ThinkingLabel`、`IsRevealing`；事件 `StopRequested`、`RetryRequested`、`FeedbackGiven`。`StreamingMarkdown.Parse` 支持段落、有序/无序列表、短标题、粗体、行内代码、代码围栏和 `[n]` 引用，未写完的标记不会露出。`StreamingScrollHost`：`IsStuckToBottom`，上滑松开，并显示回到最新按钮。

## 复用情况

共用 `Motion/AiKit.cs`（`AiMark`、`AiGlyphs`、`FadeSwap`、`SpringWidthBox`、`TurningChevron`、`AiSpinner`、`CopyAction`、`ChunkFade`）。库不带品牌标记，`Mark` 为空时显示首字母圆片；Gallery 的标记属于样例数据。 引用用 `SourceCitation`，等待态用 `TextShimmer`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 揭示节奏 | 34ms 一拍、每拍 3 个整词，落后时加量；文档描述 | `Frame`、`NextStop` | `ARunningReplyRevealsWholeWords...`（步进）；真实 stream-mid | — |
| 片段淡入 | 文档 160ms，实现 150ms；已显示文字不重建 | `ChunkFade`、`Update` | 同上（首个 Run 引用不变） | 差 10ms，待核对 |
| Thinking | 首词前显示 shimmer | `Chrome` | 真实输入发现重播时不显示并已修复；`AReplayFrom...` | — |
| 贴底跟随 | 柔和逼近底部；上滑松开；减弱动效时直接跳 | `StreamingScrollHost.Frame` | 真实滚轮：stuck=False、离开后不再跟随；点回到最新后贴底 | 跟随系数为独立近似 |

## 已知差异

- 代码块没有语法着色，复制按钮只有图标（Arc 带 Copy 文字）。
- 未完成时 Retry 带文字，Arc 完成态为纯图标。
- 会话切换、版本、编辑不属于本控件范围。
- 验证环境为 Wine，字体为替身；真实 Windows 未运行，见[验证记录](../verification/2026-11-04-batch11-replication.md)。
