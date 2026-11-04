# ReasoningBlock

状态：待验收。思考过程折叠块：进行中计时并滚动最新文本，完成后折叠为 Thought for 7s，展开看步骤；支持停止、失败和重试。事实见[参考卡](../references/components/ReasoningBlock.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`Reasoning`（可流式增长，`ReasoningParser` 解析标题）、`Steps`、`Status`（Thinking/Done/Stopped/Error）、`Seconds`、`ModelName`、`ModelMark`、`IsOpen`、`CanStop`、`CanRetry`、`Error`、`Variant`（Card/Plain）；事件 `StopRequested`、`RetryRequested`。

## 复用情况

共用 `Motion/AiKit.cs`（`AiMark`、`AiGlyphs`、`FadeSwap`、`SpringWidthBox`、`TurningChevron`、`AiSpinner`、`CopyAction`、`ChunkFade`）。库不带品牌标记，`Mark` 为空时显示首字母圆片；Gallery 的标记属于样例数据。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 实时文本 | 窗口内滑动，.24 平移；片段 140ms 淡入；显示结果 | `_liveClip`、`ChunkFade` | `ThinkingStreamsLive...` | 曲线未逐帧对比 |
| 展开/收起 | .26 高度弹簧；显示结果 | `_panelClip` | 真实点击：框高 46→400，4 步 | — |
| 计时 | 每秒更新；完成后定格 | `UpdateCounter` | `TheCounterFollowsTheClock...` | — |

## 已知差异

- Gallery 样例文字为原创，比 Arc 示例短，所以整体高度不同。
- 未写完的标题标记会暂缓显示。
- 验证环境为 Wine，字体为替身；真实 Windows 未运行，见[验证记录](../verification/2026-11-04-batch11-replication.md)。
