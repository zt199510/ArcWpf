# ChatThread

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/ChatThread.md)，验证见[记录](../verification/2026-11-03-batch5-replication.md)。

## 范围与 API

`Participants`、`CurrentUserId`、`Messages`、`DefaultMessages`、`Typing`、`ReadBy`、`Reactions`、`Placeholder`、`ShowComposer`、`AllowAttachments`、`Accept`、`GroupWindow`、`Culture`、`Label`、`Labels`；事件 `Sent`、`Reacted`、`RetryRequested`、`AttachmentInvoked`；方法 `ScrollToBottom`、`FocusComposer`。`ChatComposer` 可单独使用。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 新消息 | .46 弹簧，opacity/blur .24 | `Arrive` | 截图 chat-after | 中间帧未比对 |
| 贴底增长 | 内容从原位弹回，.44 | `OnContentResized` | 截图 | 中间帧未比对 |
| 回应 | .3 弹簧；计数滚动 | `UpdateReactions` | 测试 | Wine 无彩色 emoji |
| 已读头像 | 滑到新消息，.44 | `GlideReaders` | 截图终态 | 滑动过程未比对 |
| 输入框增高 | .4 弹簧，上限 150 | `FitField` | 未单独对比 | 待真实窗口 |
| 拖放/粘贴文件 | 托盘展开 | `ChatComposer` | 未运行 | 待真实窗口 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
