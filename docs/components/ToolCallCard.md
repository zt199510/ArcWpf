# ToolCallCard

状态：待验收。工具调用行：运行、成功、失败、待批准、已拒绝五种状态，点开显示参数和结果；`ToolCallGroup` 把多次调用折叠成一行。事实见[参考卡](../references/components/ToolCallCard.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`ToolCallData`（`Id`、`ToolName`、`Title`、`Status`、`Mark`、`Titles`、`Input`、`Output`、`Summary`、`Message`、`DurationMs`）；`ToolOutput.List/Table/Json/Text`；`ToolCallCard`：`Call`、`IsOpen`、`OutputTemplate`；`ToolCallGroup`：`Calls`、`IsActive`、`IsOpen`、`AutoCollapse`、`OutputTemplate`；事件 `ApproveRequested`、`DenyRequested`、`RetryRequested`。

## 复用情况

共用 `Motion/AiKit.cs`（`AiMark`、`AiGlyphs`、`FadeSwap`、`SpringWidthBox`、`TurningChevron`、`AiSpinner`、`CopyAction`、`ChunkFade`）。库不带品牌标记，`Mark` 为空时显示首字母圆片；Gallery 的标记属于样例数据。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 展开/收起 | 高度弹簧；文档描述 | `_panelClip` | 真实点击：h 332→40；`RunningShimmers...` | 曲线未逐帧对比 |
| 运行中 | 标题 shimmer + 转圈；文档描述 | `IsShimmeringForTest` 对应逻辑 | 真实输入 tool-run | — |
| 分组自动收起 | 成功后 450ms 收起，失败不收，用户操作后不再自动收；文档描述 + 运行观察 | `_fold` | `TheGroupFolds...`、`ThePointerChoice...` | — |

## 已知差异

- 待批准卡片的深色边框未在 Arc 捕获中核对（待核对）。
- Arc 内置工具 id 自带品牌标记；这里由调用方传 `Mark`。
- 验证环境为 Wine，字体为替身；真实 Windows 未运行，见[验证记录](../verification/2026-11-04-batch11-replication.md)。
