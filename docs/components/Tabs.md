# Tabs：M2规格

状态：代码及程序化验证完成，用户已反馈Tabs测试通过；Gallery顶层导航接入待确认，专项未测边界见验证记录。见[验证记录](../verification/2026-10-26-tabs.md)。[来源卡](../references/components/Tabs.md)。首版范围为顶端水平标签、自动激活、共享选中框、内容切换/高度过渡、溢出滚动。2026-11-11 增补关闭与拖动重排（桌面 Agent 的文档标签需要；Arc 源码没有对应功能，属 ArcWpf 设计），**实现完成，待验收**，用户此前的通过反馈不覆盖这次修改，见[验证记录](../verification/2026-11-11-treeview-tabs-enhancements.md)。仍不包括垂直标签、手动激活、跨窗口拖出或forceMount API。

## WPF公开契约

Tabs继承TabControl，TabItem继承WPF TabItem。保留Items/ItemsSource、SelectedIndex/Item/Value及ValuePath、SelectionChanged、Header/HeaderTemplate、Content/ContentTemplate、ItemTemplate/ItemContainerStyle和原生自动化。TabStripPlacement强制Top；不另造字符串Value，用SelectedValuePath=Tag或数据路径。默认首个可选项及删除后的选择遵循WPF；禁用项不能由用户导航激活，允许程序赋值以遵循原生。

Left/Right按方向循环自动激活（RTL反转），Home/End首尾；仅处理本组标签焦点，不劫持内容内方向键或嵌套Tabs。Tab进入活动面板；Ctrl+Tab/Ctrl+Shift+Tab沿用原生。列表过长出现两个有可访问名称的滚动按钮，选中/聚焦项自动显露。自动化保留Tab/TabItem与Selection/SelectionItem。

只读HasOverflow、CanScrollBackward、CanScrollForward用于默认模板。默认HeaderAlignment=Left（HorizontalAlignment DP），主预览显式Center。内部部件PART_HeaderScroll(ScrollViewer)、PART_HeaderTrack(FrameworkElement)、PART_Selection(Border)、PART_SelectedContentHost(ContentPresenter)、PART_PanelFrame(FrameworkElement)、PART_Outgoing(Image)、PART_ScrollBackward/PART_ScrollForward(ButtonBase)均可替换/缺失时降级；保留原生PART_SelectedContentHost名称。

不修改使用方Width/Height/RenderTransform/绑定/DataContext。高度动画仅占用内部PanelFrame.Height，完成、Off、卸载、换模板后恢复Auto。模板替换解除滚动和布局监听及排队操作。旧快照不响应输入、不创建交互焦点，不再显示旧活内容；真实Content对象由TabItem保留，未承诺未选面板保持Loaded。

## 关闭与重排（2026-11-11）

### 关闭

- `TabItem.IsClosable`（默认 false）：显示关闭按钮 `PART_Close`（18×18，不可聚焦，自动化名称 `Close {Header}`）。数据绑定时用 `ItemContainerStyle` 设置。不可关闭的标签外形不变（最小宽 88）。
- 触发：点击关闭按钮、在标题上按鼠标中键、焦点在 Tabs 内时按 Ctrl+F4 或 Ctrl+W（关闭当前选中且可关闭的标签），或调用 `Tabs.CloseTab(itemOrContainer)`。点击未选中标签的关闭按钮不会先选中它。
- 顺序：`TabClosing`（可 `Cancel`）→ 移除 → `TabClosed`。事件参数的 `Item` 是数据项（直接声明标签时就是 TabItem），`Index` 是关闭前的位置。
- 移除：设置了 `CloseCommand` 时由命令负责（参数为数据项），控件自己不动集合；否则从 `Items` 或可编辑的 `ItemsSource`（`IList`，非只读、非固定长度）移除。数组等固定来源无法移除，`CloseTab` 返回 false。`TabClosed` 只在标签确实同步消失后触发，所以命令里弹“未保存”确认而暂不移除时不会误报。
- 关闭选中标签后选哪个沿用 WPF 原生规则；若键盘焦点原在被关闭的标题上，焦点移到新的选中标题。

### 重排

- `Tabs.AllowReorder`（默认 false）。要求标签能原地调整顺序：`Items` 或可编辑且未排序/过滤的 `ItemsSource`；否则拖动不会开始。
- 鼠标：在标题上按下并横向移动超过系统拖动阈值后开始，Tabs 捕获鼠标。被拖标题跟随指针，限制在标签条两端之内；其前缘越过相邻标题的中点时，相邻标题让到空出的位置（Full 为 200ms 过渡，Reduced/Off 立即到位）。松开后被拖标题从松手处滑入新位置。Escape、失去鼠标捕获、模板替换或动效等级变化会取消并复位。
- 溢出时指针停在标签条两端 33 DIP 内会自动滚动。
- 键盘：焦点在标题上按 Ctrl+Shift+←/→ 移动一位（RTL 反向，跳过隐藏标签），焦点留在被移动的标题上。
- 被拖标题在拖动和滑入期间 `TabItem.IsDragging` 为 true：模板给它一块 Surface 底色并抬高层级，盖住它经过的相邻文字。
- 选中标签被移动时，控件让相邻项逐个越过它，而不是把它移除再插入，因此不触发 `SelectionChanged`、不重建其内容，`SelectedIndex` 绑定只收到最终位置变化。提交后触发 `TabMoved`（`Item`、`OldIndex`、`NewIndex`）。
- 位移通过标题面板的内部附加属性在排列阶段实现，不占用标题的 `RenderTransform`。

## 动效合同

指示器位置与宽度：.42临界阻尼适配，连续重选保留当前值/速度；首次/尺寸变化立即定位。内容：根据新旧索引确定方向，RTL反转；新面板x±8到0（.4），淡入240ms；旧面板反向6，120ms淡出。内部高度从当前高度到新面板自然高度（.4），完成恢复Auto。Reduced只进入120ms/退出100ms交叉淡化，Off立即完成。标题前景160ms；滚动Full平滑、Reduced/Off即时。

退出使用内存RenderTargetBitmap固定图像，避免UIElement双亲与旧内容仍能输入。只捕获本控件内容，单边最多4096像素/总像素受限；失败或过大跳过旧图像淡出，仍显示新内容，不保存截图文件。位图不含独立Popup/HwndHost内容；属于WPF适配。连续切换仅保留最近旧图像，旧完成回调不得清理新一轮。弹簧非Web求解器逐帧保证，字体、像素舍入和阴影差异保留。

## 交付与验收矩阵

| 项目 | 范围 | 状态 | 证据出口 |
|---|---|---|---|
| 原生选择/数据/内容状态/自动化 | 必做 | 程序化通过 | 双向绑定、动态增删、分组隔离、角色 |
| 外形/标题/内容间距 | 必做 | 程序化通过 | 88/36/4/16最终坐标、共享框位置 |
| 键盘/溢出/RTL | 必做 | 程序化通过 | 导航算法、显露位置；实际焦点待验收 |
| 选中框/内容/高度/清理 | 必做 | 程序化通过 | 真实时钟、弹簧收敛、Auto恢复、反向与模板清理 |
| Gallery/Consumer | 必做 | 程序化通过 | 同源主预览与高度/长列表/嵌套/绑定扩展 |
| 关闭：按钮、中键、Ctrl+F4、取消、命令、数据源 | 必做（2026-11-11） | 已实现，待验收 | `TabsTests` 关闭 5 项；Wine 真实鼠标和按键 |
| 重排：拖动、让位、落位、取消、边缘滚动、键盘 | 必做（2026-11-11） | 已实现，待验收 | `TabsTests` 重排 4 项（含真实时钟）；Wine 真实拖动和按键 |
| 实际窗口/DPI/截图/性能 | 延期验收 | 未运行 | 不以程序化测试替代 |

未建立已审查WPF截图基准；交付时记录差异和实际测试结果。


