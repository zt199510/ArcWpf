# 2026-11-04 Pi.NET 后端验证

状态：**待验收**。范围：`samples/ArcWpf.AiAssistant` 接入 Pi.Agent、Pi.Ai 1.4.5。

## 结论边界

**没有连接过真实模型。** 验证用了两种替身：Pi 自带的 Faux，以及本机模拟的 OpenAI 兼容 HTTP 服务（走 Pi 真实的 OpenAI Provider）。所以请求格式、Key 传递和工具往返已验证，真实服务商的返回、Anthropic Provider 和思考模式都没有实际跑过。

## 接入方式

- 用 `Pi.Agent` + `Pi.Ai`，不用 `Pi.CodingAgent`：后者自带 bash 和读写文件，权限超出需要。
- 环境变量选择后端（见 README）；Key 只从环境变量读取。没有 Key 时回到模拟；有 OpenAI Key 但没有模型时也回到模拟，并在界面说明缺什么。
- 示例工具：`get_current_time`（只读，免审批）、`save_note`（要审批，只进结果面板，不写磁盘）。这是用户尚未指定工具时的占位。

## 接口与行为变化

- 后端改为每个会话一个 `IAssistantSession`，审批不再拆成两段串流，`ApprovalNeeded` 带答复句柄。
- 一轮可以有多段文字，与工具卡、审批卡按时间排列。MessageActions 改在回复结束后才出现，生成中用输入框的停止。
- **工具 Retry 和 Request again 都改为重跑本轮**，会重新执行本轮已跑过的工具，也会重新询问审批。Pi 没有单独重跑一个工具的概念。
- 审批不再模拟发送中状态：答复是本地的。

## 接入 Pi 时处理的坑（运行观察）

在 WPF Dispatcher 下用 Faux 实测：

| 观察 | 处理 |
|---|---|
| 事件在第一个 await 后不在 UI 线程 | 用 Channel 交给读取方 |
| 工具开始事件早于审批钩子 | 需审批的工具批准后才显示 |
| 模型错误不抛异常，只有 `StopReason` | 转成 `AssistantException` |
| 工具执行中中止：无结束事件，记录里留下没有结果的工具调用 | 修复记录，退回到最后完整的位置；停止的半截回复因此不会进入上下文 |
| **Pi.NET 1.4.5 缺陷**：`BeforeToolCallResult.Terminate` 在拦截时不生效（判断读的是 `Result.Terminate`，源码确认） | 用 `FinishTurn` 在已取消时结束 |
| 钩子无法改参数 | 包装工具执行，用户编辑的字符串参数在执行时替换；模型的记录里仍是原始参数 |

## 构建与测试

- 解决方案构建：0 错误。
- `AiAssistantTests`、`PiAgentBackendTests`、`StreamingMessageTests`：**48 项全部通过**。其中包括经 Pi OpenAI Provider 的真实 HTTP 往返（Authorization、模型名、工具定义、工具结果回传）。
- 全量测试：**未运行**（按用户要求只测相关内容）。

## 真实输入（Wine 窗口内注入鼠标和键盘）

- **Pi 路线**（Pi OpenAI Provider + 本机模拟服务）：标记显示 OpenAI compatible，不显示示例历史；提问后思考、一段文字、只读工具自动执行、第二段文字、审批卡；Approve 后工具执行，便签进入结果面板；第二个问题的请求带完整历史（`system,user,assistant,tool,assistant,tool,assistant,user`）。
- **模拟路线回归**：审批、拒绝、Request again、工具失败 Retry、连接中断 Retry、停止、切换会话、草稿和附件、归档、上传失败重试、窄窗的日志结果符合预期，无异常。
- 深色：场景跑完无异常，本次**未重新目视**。
- 直接启动 exe（无环境变量）：首屏为模拟模式，显示正常。

截图在 `work/assistant/wpf/`（不入库）。

## 已知限制

- 图片和二进制文件不发给模型，只告诉模型有这个文件；文本文件超过 200,000 字节同样如此。
- 模型和 Key 只能用环境变量配置，还没有设置界面。
- 工具如果忽略取消令牌，停止要等它返回才生效。示例工具不受影响。
- 生成中不能追加消息（Pi 的 `Steer` / `FollowUp`）。
- 会话不持久化，关闭程序后丢失。

## 未运行

真实模型、Anthropic Provider、思考模式、真实 Windows 窗口、高频工具输出更新、长会话性能、全量测试。
