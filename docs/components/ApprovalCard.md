# ApprovalCard

状态：本轮用户验收通过（2026-11-11）；专项未测项见验证记录。工具调用前的权限确认：风险、可编辑参数、拒绝、始终允许、批准、失败重试和重新请求。事实见[参考卡](../references/components/ApprovalCard.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`ApprovalRequest`（`AgentName`、`ModelName`、`ModelMark`、`Asked`、`ToolMark`、`Title`、`Reason`、`Risk`、`Arguments`）、`ApprovalArgument`；`ApprovalCard`：`Request`、`Status`（`Pending`、`Sending`、`Failed`、`Approved`、`Denied`、`AllowedByRule`、`NotSent`）、`Workspace`、`SendDecision`、`Decide()`；事件 `Decided`、`RequestedAgain`。`SendDecision` 抛异常进入 Failed，取消进入 NotSent。键盘：a 或 Enter 批准，d 拒绝，输入框和菜单内不响应。

## 复用情况

复用 `FadeSwap`、`SpringHeightBox`、`TextShimmer`、`TurningChevron`、`ToolbarButton`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 换请求 | 淡出 100、淡入 200、3px 上移，高度弹簧；文档描述 | `OnRequest` | 真实输入 appr-next | — |
| 决策行 | 标签交叉淡入；文档描述 | `Decisions` | 真实输入 sending→failed→approved | — |
| 发送中 | 文字 shimmer；文档描述 | `SendingRow` | 真实输入 appr-sending | — |
| 菜单 | 箭头翻转，再点只关闭；运行观察 | `OpenMenu`、`Place` | 真实输入 at=-108,42；`AlwaysAllowMenu...` | 规则飞入动画未做 |

## 已知差异

- 只做单个请求。队列、j/k、规则弹层、已决日志属于 Arc 的整块组件，未实现。
- “重新请求”按钮是用户要求的扩展：Arc 只有发送失败后的 Retry。重新请求会丢弃上次的参数修改。
- Always allow 只报告结果，规则持久化由调用方负责。
- 窄宽时隐藏 Enter 键帽并收紧按钮内边距；失败信息在操作按钮前截断，避免溢出。Arc 无对应窄宽基准，属 WPF 适配。
- 深色下 Approve 仍沿用 Accent 令牌；本轮 Windows Gallery 目视文字可读，未做色彩对比度数值测量。
- 旧批次使用 Wine；本轮已做部分 Windows Gallery 目视与鼠标检查，范围及未测项见[验证记录](../verification/2026-11-11-ai-controls-acceptance.md)。
