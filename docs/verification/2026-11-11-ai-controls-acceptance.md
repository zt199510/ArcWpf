# 2026-11-11 五项 AI 控件对照验收

状态：五项控件用户验收通过（2026-11-11 反馈“我验收了没问题”）；下列未执行的专项检查仍按未测记录。范围：AttachmentTray、MessageActions、ConversationSidebar、ApprovalCard、ArtifactPreview，及 AI Assistant 联动。未引入 Arc Pro 私有源码。

## 来源与目标

- AttachmentTray 对照 Arc 免费 File Upload，固定来源为 `tests/Baselines/References/2026-11-04-file-upload/`；列表尺寸、进度与完成状态有源码证据。芯片参考公开的 AI Composer 页面。
- MessageActions 对照 Arc Pro 公开 Response Feedback 页面。停止、重新生成、引用属于 ArcWpf AI 对话组合；不能将这些行为称为该 Arc 组件源码复刻。
- ApprovalCard 对照 Arc Pro 公开 Agent Approvals 页面。单卡审批、失败重试与窄宽压缩为本项目实现范围，队列和规则持久化不在范围内。
- ConversationSidebar、ArtifactPreview 没有 Arc 同名控件。前者取 workspace sidebar、ai-chat 的公开视觉；后者组合 Tabs、CodeBlock、JSONViewer 和回复渲染器。两者按 ArcWpf 设计验收。
- 本轮继续使用 2026-11-04 参考卡的页面和快照版本；未重新冻结来源。浏览器实时页面无公开版本号，不能证明它与旧快照完全相同。

## 本轮修正

| 控件 | 已修正的问题 | 证据 |
|---|---|---|
| AttachmentTray | 上传完成对勾补描线，保留缩放淡入 | 专项测试、Windows Gallery 失败重试至 Uploaded |
| MessageActions | `Text` 更新后复制内容同步；评分变化或折叠使旧异步反馈失效 | 专项测试、Windows Gallery 点赞面板展开 |
| ConversationSidebar | 归档当前会话时跳过已归档条目选择下一活跃会话 | 专项测试、Windows Gallery 标题搜索筛选 |
| ApprovalCard | 窄宽操作行压缩，失败文字截断；340 DIP 下 Approve 未越过卡片内边界 | 布局边界测试、Windows Gallery 明暗外观和规则 Popup |
| ArtifactPreview | 删除标签后公开 `SelectedIndex` 同步夹取，清空归零 | 专项测试、Windows Gallery 四类标签切换 |

## 执行结果

- 五项专项测试：57/57 通过。
- PromptInput、StreamingMessage、ToolCallCard、AiAssistant、PiAgentBackend 相关测试：80/80 通过。覆盖已有 AI Assistant 的附件重试、停止与重新生成、会话切换、审批失败与重试、结果预览流程。
- `dotnet test` 本轮重新构建了 Controls、Gallery、AI Assistant 与测试项目，0 错误；仓库原有编译和分析器警告仍在。
- Windows Gallery 实际显示：浅色附件三行状态正常，失败重试先显示 Uploading 0% 后到 Uploaded；MessageActions 点赞后面板展开且选中态可见；ConversationSidebar 输入 `pricing` 后只显示匹配条目；ApprovalCard 明暗两种主题下按钮文字可读，Always allow Popup 在卡片下方打开；ArtifactPreview 四个标签均能显示对应内容，深色下无明显重叠。

## 未验收与差异

- 上述 Windows 操作是当前屏幕的目视和鼠标检查，不等于与 Arc 逐像素一致。完成勾描线、感谢折叠、标签与审批动效的逐帧时间尚未测量。
- 340 DIP 的审批操作行有 WPF 布局边界测试；Gallery 未人工缩到该宽度。失败态长错误文本也仅检查代码布局，未在窗口中触发查看。
- Windows 窗口未逐项测试键盘焦点、IME、屏幕阅读器、触摸、不同 DPI、Reduced/Off 动效、极窄窗口和长列表滚动。审批 Popup 只检查打开与位置，未在窗口中验证翻转。
- AI Assistant 联动有自动化测试；本轮未重新打开独立 AI Assistant 窗口做全流程人工操作。用户的五项控件验收反馈不等同于这条独立程序全流程检查。
