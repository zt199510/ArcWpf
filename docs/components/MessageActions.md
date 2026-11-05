# MessageActions

状态：待验收。消息下方的操作行：复制、重新生成、停止、点赞/点踩、引用；拇指打开追问面板。事实见[参考卡](../references/components/MessageActions.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`MessageActions`：`Text`、`IsGenerating`、`CanRegenerate`、`CanQuote`、`CanRate`、`AsksWhy`、`Rating`、`NegativeReasons`、`PositiveReasons`、`PrivacyNote`、`ThanksMessage`、`ThanksDuration`、`SubmitFeedback`；事件 `RegenerateRequested`、`StopRequested`、`QuoteRequested`、`RatingChanged`、`FeedbackSubmitted`。`SubmitFeedback` 抛异常时显示错误和 Try again。

## 复用情况

复用 `CopyAction`、`FadeSwap`、`SpringHeightBox`、`ThreadUi.PlainTextBox`、`ThreadUi.PressScale`。`CopyAction` 和 `AiUi.Icon` 增加可选圆角参数。StreamingMessage 的页脚未改为复用本控件。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 拇指 | 实心淡入 160ms，按下 .94；文档描述 | `Thumb.Show` | 真实输入 act-down；`ThumbsToggle...` | — |
| 面板开合 | 高度弹簧 .24，淡入 160ms；文档描述 | `Present`、`Fold` | 真实输入 h=222→0；折叠中再按可反转 | — |
| 表单与感谢 | 一次交叉淡入，高度弹簧；文档描述 | `FadeSwap`、`ShowThanks` | 真实输入 act-thanks | — |
| 重新生成与停止 | 同位交叉淡入；WPF适配 | `Chrome` | 真实输入 act-generating | Arc 行内无停止 |
| 感谢折叠 | 1600ms 后折叠，评分保留；文档描述 | `_thanks` | 真实输入 folded=True，rating=Down | — |

## 已知差异

- 停止和引用是用户要求的扩展，Arc 该行没有；引用只发事件，由调用方写入输入框。
- Arc 只在悬停时显示行，触屏常显；本控件始终显示，由宿主决定何时放置。
- 验证环境为 Wine，字体为替身；真实 Windows 未运行，见[验证记录](../verification/2026-11-04-batch12-replication.md)。
