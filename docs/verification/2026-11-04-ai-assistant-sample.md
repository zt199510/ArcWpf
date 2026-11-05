# 2026-11-04 AI 助手面板示例程序验证

状态：**待验收**。范围：独立程序 `samples/ArcWpf.AiAssistant`，以及为它修改的两处库代码。

## 范围调整

用户要求 AI 助手面板单独做成 WPF 程序，不放进 Gallery。程序不引用 Gallery，示例数据也是自己的一份。回复由 `SimulatedBackend` 按脚本产生，**没有模型、没有网络，不读取附件，也不改任何文件**，界面上标有 Simulated。接真实模型时实现 `IAssistantBackend`，再赋给 `AssistantPanel.Backend`；这条路径**未运行**。

是否由运动控制框架使用这个面板，用户尚未确认，本次没有按它设计。

## 环境

Linux + Wine + Xvfb，字体为 Liberation 改名替身，不是真实 Windows 窗口。

## 库代码改动

- `StreamingMessage.HidesFoot` 改为公开：回复下方已有 MessageActions 时，不再出现第二个复制按钮。
- `ThemeWatch` 改为监听主题重建结束时写入的 `ArcWpf.ThemeRevision`。**这是既有缺陷**：原来监听 `Surface`，它写入时回调同步触发，排在后面的令牌仍是旧值，运行时切深色后 `Border`、`ControlTrack` 等还停在浅色。第十二批的深色检查是先切主题再建控件，所以没有发现。影响所有基于 `ArcChromeButton` 的控件。

## 构建与测试

- 应用和测试项目构建：0 错误。
- `AiAssistantTests` 新增 14 项，与 StreamingMessageTests、ArtifactPreviewTests 合计 **40 项全部通过**。
- 主题回归测试 `ChromeButtonsReadTheWholeThemeOnASwitch`：暂时还原修复后失败（期望 `#FF2B2B2B`，实际 `#FFE9E9E9`），恢复后通过。
- `ThemeWatch` 是共用部件，因此加跑会切换主题的 15 个测试类：**198 项，194 通过，4 失败**。还原 `ThemeWatch` 改动后，这 4 项以相同断言失败，与本次修复无关，未处理：
  - `SearchRestoresGroupsAndPreservesCurrentPage`、`SearchKeyboardOpensOnePageAndEscapeClears`、`GeometrySelectionRevealAndStickyHeading`：断言 slider 搜索只有一条结果或选中特定页面，实际有 2 条。
  - `DownloadCompletesWithRealImageBlurFadeAndSettles`：`HasImageError` 为真。
- 全量测试：**未运行**（按用户要求只测相关控件）。

## 真实输入（Wine 窗口内注入鼠标和键盘）

| 场景 | 结果 |
|---|---|
| 主流程 | 提示按钮填入输入框，Enter 发送；思考、工具运行、审批卡等待；Approve 发送中后执行工具，串流回答，结果面板展开 4 个标签 |
| 审批 | Deny 后回答说明未改动且无结果；Request again 回到待处理，清掉后续内容，再批准可完成；等待审批时按停止为 NotSent，发送决定途中按停止不会执行工具 |
| 回复操作 | 复制、引用进输入框、点赞；重新生成清空旧内容和结果后重跑 |
| 失败恢复 | 工具失败后在卡片 Retry 成功；连接中断显示错误，Retry 第二次成功；串流和思考中停止都停在当前位置 |
| 多会话 | 回复进行中切走再回来，内容和结果都在，后台会话会继续跑到审批；草稿、输入框附件、结果、文件按会话保留；重命名同步标题；归档当前会话后切到下一个 |
| 附件 | 上传进度；失败后在文件列表重试成功；移除同步到输入框；发送后失败文件不留在列表 |
| 窄窗 | 1120 以下结果面板浮在线程上，820 以下会话栏也浮动；关闭按钮和遮罩都能关；窗口缩小时已打开的面板自动收起 |
| 深色 | 运行时切换后选中行、结果面板和已批准状态的审批卡目视正常。**待处理审批卡的 Approve 按钮文字对比度偏低**，与第十二批记录的是同一令牌，未调整；工具开关开启状态在修复后未单独复查 |

另外直接启动 exe（不经过测试程序），`import` 截取 X 根窗口，首屏显示正常。这只证明能启动，后续操作都由测试程序注入。

真实输入发现并修复的缺陷：

- 窄窗下结果面板盖住自己的开关，点会话栏也关不掉：加关闭按钮，遮罩只盖中间区域。
- 上传失败的文件发送后仍留在文件列表。
- 输入框附件不随会话切换。
- 审批发送途中按停止仍会继续执行。
- 运行时切深色后选中行浅底浅字，见上面的 `ThemeWatch`。

## 视觉判断

只做目视自查。Arc 没有这样一个完整面板，**无法做还原对比**，整体布局属 ArcWpf 组合设计。截图在 `artifacts/ai-assistant-2026-11-04/`（不入库）。

## 待观察

一次运行中，审批卡出现约 0.3 秒就点击 Approve 没有生效，等待 0.7 秒以上再点则正常。推测是入场动画期间目标还在移动，但没有证实，需要在真实窗口确认是否影响使用。

## 环境假象

- 窗口外的黑边：Xvfb 根窗口。
- 同一段文字深浅不一、中等字重不明显：替身字体，第十二批已记录。

## 未运行

真实 Windows 窗口、真实字体、真实模型后端、触摸、高 DPI、`Reduced` 动效级别、屏幕阅读器、长会话性能、全量测试。
