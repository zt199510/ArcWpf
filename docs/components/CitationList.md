# CitationList

状态：待验收。引用来源：搜索状态行、逐张到达的来源卡、堆叠的其余来源、正文 `[n]` 高亮对应卡片。`SourcePopover` 是可单独使用的来源详情浮层，`SourceCitation` 是正文内的编号。事实见[参考卡](../references/components/CitationList.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`CitationSource`（`Title`、`Url`、`Id`、`Site`、`Snippet`、`Published`、`Mark`）；`CitationList`：`Sources`、`Status`、`Total`、`Query`、`DurationMs`、`Max`（默认 3）、`Error`、`ShowStatus`、`IsExpanded`、`Highlighted`、`Label`，`Content` 放答案正文；事件 `SourceClicked`、`RetryRequested`。`SourcePopover`：`Show`、`Hide`、`IsOpen`、`Source`、`Target`。`SourceCitation`：`Number`、`Source`、`IsLit`。

## 复用情况

共用 `Motion/AiKit.cs`（`AiMark`、`AiGlyphs`、`FadeSwap`、`SpringWidthBox`、`TurningChevron`、`AiSpinner`、`CopyAction`、`ChunkFade`）。库不带品牌标记，`Mark` 为空时显示首字母圆片；Gallery 的标记属于样例数据。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 来源到达 | 180ms 上升 4；预留槽位不跳动；显示结果 | `CardKindsForTest` 对应逻辑 | `SearchingCounts...`；真实 cite-search | — |
| 详情浮层 | 320ms 悬停意图，160ms 上升 4、缩放 .98；卡片间切换即时；显示结果 | `Hover`、`SourcePopover.Show` | 真实鼠标：cite-hover、切换到 mozilla、离开关闭 | — |
| 引用高亮 | 卡片亮 2.6s；显示结果 | `OnCitationClick`、`_unlight` | `ACitationClick...`；真实点击 high=1 | — |

## 已知差异

- 浮层显示窗口时可能误报离开，改用真实光标判断并短轮询。真实 Windows 上是否出现未验证。
- Gallery 正文未使用行内代码样式。
- 验证环境为 Wine，字体为替身；真实 Windows 未运行，见[验证记录](../verification/2026-11-04-batch11-replication.md)。
