# 2026-11-04 第十二批复刻验证

状态：**待验收**。范围：AttachmentTray、MessageActions、ConversationSidebar、ApprovalCard、ArtifactPreview。

## 环境

Linux + Wine + Xvfb，字体为 Liberation 改名替身（字宽、字重、纵向 1px 差异属环境差异），不是真实 Windows 窗口。

## 构建与测试

- `dotnet build` 库、Gallery、测试项目：0 错误。
- 专项测试：`--TestCaseFilter` 选这五个测试类，以及代码或共用部件被本批改动的 StreamingMessage、PromptInput、ToolCallCard，**103 项全部通过**，其中新增 53 项。
- 全量测试：**未运行**（按用户要求只测这些控件）。

## 真实输入（Wine 窗口内注入鼠标和键盘）

日志：`artifacts/batch12-2026-11-04/real-input.log`（不入库）。

| 控件 | 操作与结果 |
|---|---|
| AttachmentTray | 点缩略图触发预览；添加文件显示 `Uploading 10%` 和进度条，完成后条折叠、显示对勾；失败显示原因和重试，重试后从 0 走到 Uploaded；移除后焦点到相邻移除按钮；切芯片布局，滚轮横移 |
| MessageActions | 复制后显示对勾；点踩打开面板，选原因后 Submit 可用，备注可输入；模拟失败显示 Try again，再发显示感谢，随后折叠且评分保留；点赞面板 Esc 关闭，焦点回拇指；再按拇指清除；引用带文字；重新生成切成停止，拇指禁用 |
| ConversationSidebar | 悬停显示行操作；点行选中；重命名框全选，输入后 Enter 保存；F2 进入、Esc 还原；方向键移动，Delete 归档；展开归档区后恢复；搜索过滤并隐藏空分组，Down、Enter 选中；无结果提示；Esc 清空；新建置顶并选中 |
| ApprovalCard | 点值进入编辑，输入框内按 a 不会批准；Esc 还原；Enter 保存后出现还原按钮；Always allow 菜单右对齐，再点触发器只关闭，Esc 回到触发器；Approve 发送中 shimmer，失败显示 Retry 和 Deny，重试后 Approved；d 拒绝后 Request again 回到待处理；Enter 批准；菜单选工作区后为 Allowed by rule |
| ArtifactPreview | 四个标签都能切换；文件页 Open、Download 触发事件 |

真实输入和回归测试发现并修复的缺陷：

- MessageActions 面板折叠后再按拇指抛异常：外框被重复挂到新父元素。
- ConversationSidebar 恢复的归档行不可见：淡出动画一直占着透明度，直接赋值无效。
- ApprovalCard 菜单在触发器上再点不关闭：透明外边距盖住了触发器。菜单还左偏 48px：外边距在 Host 的子元素上时，定位回调给的尺寸已包含外边距，与第十一批的情况相反。已加几何测试。
- ArtifactPreview 内容没有撑满：TabControl 按选中 TabItem 的对齐方式摆放内容，TabItem 为居中。改为绑定宽度，已加测试。
- ArtifactPreview 的 JSON 页出现两层外框、Markdown 页内边距不生效、复制按钮样式不一致。
- 共享图标 `Lucide.Ban` 的斜线画到了画布外，只显示圆圈。这是既有缺陷，影响其他用到它的控件。
- 深色切换后 Approve 键帽颜色不更新：改为跟随主题。

## 视觉对比

只做并排目视，**未计算像素差**：Gallery 舞台多了演示按钮，示例文字也与 Arc 不同，数字没有意义。对比图在 `artifacts/batch12-2026-11-04/`（不入库）：`compare-approval.png`、`compare-feedback.png`、`compare-tray.png`。

ConversationSidebar 和 ArtifactPreview 在 Arc 中没有同名组件，**无法做还原对比**，只能判断是否与 Arc 的视觉语言一致。

## 深色主题

五个控件都切到深色并截图自查（`dark.png`）。ApprovalCard 的 Approve 按钮文字对比度偏低，与第十一批 Allow 按钮同一令牌，未调整。其余未见明显配色问题。

## 环境假象

- 透明浮层周围的黑色边：Xvfb 没有合成器。
- 指针点击后出现焦点环：环境中 `SystemParameters.KeyboardCues` 为 True。
- Markdown 同一段文字深浅不一，且每次运行位置不同：读取所有文字段的 Foreground 都是 `#FF0B0B0B`，属替身字体在该环境下的渲染差异。
- 中等字重看起来不粗：替身字体没有 Medium。

## 未运行

真实 Windows 窗口、真实字体、触摸、高 DPI、`Reduced` 动效级别的真实观察、屏幕阅读器、大列表性能、全量测试。动效只在窗口中观察过起止状态和部分中间帧，未逐帧比对 Arc。
