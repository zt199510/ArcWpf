# Tooltip：M2规格

状态：实现及程序化验证完成，待用户实际窗口验收。见[验证记录](../verification/2026-10-26-tooltip.md)。[参考卡](../references/components/Tooltip.md)。本次包含悬停/焦点提示、四方向翻转/避让、共享延迟、进退场、文本/尺寸变化、主题/动效、Gallery和Consumer。

## 公开契约及WPF适配

Tooltip : ContentControl。Content/ContentTemplate用于触发控件；Tip(object,null)与TipTemplate(DataTemplate,null)用于提示内容；Side(PopoverSide,Top)验证枚举。只读IsOpen/IsPresented/ActualSide，Close()可关闭当前提示。不同于WPF ToolTip属性，也不同于Web asChild：使用arc:Tooltip包裹触发按钮，不修改按钮样式、命令或焦点。包装本身不进入Tab序列。

鼠标进入冷态等待250ms；键盘焦点立即；Dispatcher内共享warm状态，已打开或关闭300ms内的新提示立即淡入。离开触发器到气泡留120ms穿越间隙，指针停留气泡保持打开；气泡不接受点击/Tab焦点。按Escape、按下触发器、Owner失活/关闭、锚点不可见、卸载、换模板关闭，Escape/点击后的同轮悬停抑制重开，完全离开后恢复。不会抢焦点或捕获鼠标，不为关闭调用Focus。

Tip为字符串/数字时动画文本与尺寸；空/null关闭。TipTemplate/其他对象由ContentPresenter呈现，不承诺文本动画。提示只补充说明，不放交互元素。默认无HelpText时为直接UIElement触发器绑定提示文字，显式名称/HelpText优先，解除包装时仅清除自有Binding；不得用提示代替图标按钮名称。Popup内容Peer为ToolTip角色。

使用PART_Popup、PART_Frame、PART_Surface、PART_Text、PART_Custom部件。Popup明确桥接DataContext/FlowDirection/ArcMotion；模板动态资源沿逻辑链查找。初始加载没有用户触发不补播。实际状态IsPresented持续至退场结束，公开IsOpen先关闭；重开取消旧回调，重复关闭不破坏退出。卸载/模板变更清理延迟、所有Owner/Popup/输入事件、动画和自动化绑定。

## 几何与动效合同

默认气泡最大240宽、18圆角、左右16上下12、边框1、14字体/19.6行高；顶部/底部左右四边offset8，工作区留12，无箭头。首入160ms透明度与y±3/scale.97；warm120ms只透明度；退出120ms到透明度0/scale.98，Reduced120ms仅透明度，Off立即。

文本变化新文本240ms、旧文本120ms，尺寸.42spring采样；内部尺寸完成恢复Auto。主题更新/未显示时即时重排。动画只占内部部件属性，不破坏触发器或Tip公开绑定；清时钟后持久基值维持正确终态。快速重定向不保连续速度，属于WPF近似。

窗口移动/尺寸变化/触发器布局变化时跟随；不可见或移出滚动裁剪关闭。屏幕DPI计算复用Popover现有辅助，实际跨屏/DPI验证仍待运行。自定义复杂内容过高不提供交互滚动，改用Popover；触屏长按、禁用控件提示、原生ToolTipService自动接管不在本次范围。

## 完整度矩阵

| 交付项 | 范围 | 实现状态 | 证据/边界 |
|---|---|---|---|
| 包装/Tip绑定/自动化 | 必做 | 已实现，程序化通过 | STA/Peer/HelpText绑定；真实读屏未测 |
| 延迟/共享warm/关闭生命周期 | 必做 | 已实现，程序化通过 | 状态与Dispatcher计时器；真实窗口未测 |
| 几何/定位/翻转 | 必做 | 已实现，程序化通过 | 布局和定位数学；跨屏未测 |
| 进退场/文案/尺寸/Reduced/Off | 必做 | 已实现，程序化通过 | 真实时钟/后续刷新 |
| Gallery/Consumer | 必做 | 已实现，程序化通过 | 同源文案，连续图标/四边/长文字/动态内容 |
| 实际窗口/截图/DPI/性能 | 延期 | 未运行 | 按用户既定约定，单测不替代 |

