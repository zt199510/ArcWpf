# CommentThread

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/CommentThread.md)，验证见[记录](../verification/2026-11-03-batch5-replication.md)。

## 范围与 API

`Comments`、`DefaultComments`、`CurrentUser`、`People`、`IsResolved`、`Title`、`Reactions`、`Placeholder`、`MaxDepth`、`NowLabel`、`Labels`、`AlwaysShowActions`；事件 `CommentsChanged`、`ResolvedChanged`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 解决/重开 | 高度 .42，圆角 26↔22 | `OnResolved`、`ApplyRadius` | 测试+截图 comm-resolved | 中间帧未比对 |
| 编辑 | 正文与输入框交叉淡入 | `UpdateEditing` | 测试+截图 comm-edit | 中间帧未比对 |
| 回复折叠 | 高度 .42，箭头 snappy | `UpdateReplies` | 测试+截图 | 中间帧未比对 |
| @提及 | 内联列表，高度 .42 | `CommentComposer` | 测试+截图 comm-reply | 真实按键未运行 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
