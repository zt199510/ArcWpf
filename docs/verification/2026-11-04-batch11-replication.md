# 2026-11-04 第十一批复刻验证

状态：**待验收**。范围：PromptInput、StreamingMessage（含 StreamingScrollHost）、ToolCallCard（含 ToolCallGroup）、ReasoningBlock、CitationList（含 SourceCitation、SourcePopover）、ModelPicker。全部为 Arc Pro 组件，只依据公开文档和页面显示结果。

## 环境

Linux + Wine + Xvfb，字体为 Liberation 改名替身（字宽、字重、纵向 1px 差异属环境差异），不是真实 Windows 窗口。

## 构建与测试

- `dotnet build` 库、Gallery、测试项目：0 错误。
- 专项测试：`--TestCaseFilter` 只选这六个测试类，**74 项全部通过**。
- 全量测试：**未运行**（按用户要求只测这些控件）。

## 真实输入（Wine 窗口内注入鼠标和键盘）

| 控件 | 操作与结果 |
|---|---|
| PromptInput | `/` 打开四条命令，输入 `s` 过滤，Enter 生成令牌；`@` 列表、方向键、令牌前插入后位置跟随；Backspace 整体删除，方向键跳过令牌；Shift+Enter 变两行（高 64），清空回 40；鼠标点列表项连续 12/12 成功；加号菜单打开，再点只关闭，Esc 焦点回按钮；发送后变停止，点击后 `Stopped` |
| ModelPicker | 悬停打开强度和更多模型子菜单，移向侧边不关闭；选 High、选 Sonnet 后总览和确认文字更新；再点胶囊只关闭；点外部关闭；键盘 Down 打开、Right/Left 进出子菜单、Esc 关闭且焦点回胶囊；窄窗口分页 |
| ToolCallCard | 点击展开高 332，收起回 40；分组折叠；Run again 显示 `Using 1 tool` 并 shimmer，结束为 `Used 3 tools`；Allow 触发事件 |
| ReasoningBlock | 展开四步，框高 46→400；Think again 显示计时；Stop 变 Stopped；Fail 显示 Retry |
| CitationList | 悬停 320ms 打开浮层，切卡即时换内容，离开关闭；展开 7 张；点卡触发事件；点正文引用高亮；重新搜索 `Searching 1 of 7`；失败显示操作 |
| StreamingMessage | Replay 显示 Thinking，流式中 Stop 截断在当前位置，Retry 重新开始，结束显示页脚；滚轮上滑后 `stuck=False` 并出现回到最新，离开后不再跟随，点击后回到底部 |

真实输入和回归测试发现并修复的缺陷：

- 浮层定位：WPF 自定义定位回调给的尺寸不含 Child 的 Margin，三个控件向上打开的浮层都压住目标 48px。已修复，并加几何测试。
- ModelPicker 高亮高度动画从 NaN 起步，打开菜单就抛异常。
- 列表在静止指针下打开时，高亮被指针抢走。
- 重播时 StreamingMessage 不显示 Thinking；滚轮事件被滚动器处理后收不到松开信号。
- Gallery：停止按钮随回复增长移动，已把回复放进固定高度的滚动区；工具演示少了第三步。

## 视觉对比

只做并排目视，**未计算像素差**：Gallery 舞台多了演示按钮，示例文字也与 Arc 不同，数字没有意义。对比图在 `artifacts/batch11-2026-11-04/`（不入库）。

## 深色主题

六个控件都切到深色并截图自查（`dark.png`），未见明显配色问题。待批准卡片的 Allow 按钮在深色下对比度偏低，未调整。

## 环境假象（不是控件缺陷，但未在 Windows 确认）

- 浮层四周的黑边：Xvfb 无合成器，透明窗口的边距显示为黑色。现有 MentionInput 浮层同样出现。
- 指针点击后的焦点框：此环境 `SystemParameters.KeyboardCues` 为 True，WPF 因此总显示焦点框。
- 主窗口设为 Topmost 时，Wine 下主窗口可能盖过浮层，点击落到主窗口（6 次中 4 次失败）；取消 Topmost 后 12/12 成功。使用 Topmost 窗口的应用需要在 Windows 上复查。

## 未运行或证据有限

- 真实 Windows 窗口、触控、高 DPI：未运行。
- 动效减弱：只测了 `Off` 档；`Reduced` 档未单独运行。
- 文件拖放和文件对话框：真实输入未运行；粘贴路径只测了内部方法，未走系统剪贴板。
- 动画流畅度和帧率：未测量；弹簧曲线未与 Arc 逐帧对比。
