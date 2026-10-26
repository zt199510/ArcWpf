# Tabs：M2规格

状态：代码及程序化验证完成，用户已反馈Tabs测试通过；Gallery顶层导航接入待确认，专项未测边界见验证记录。见[验证记录](../verification/2026-10-26-tabs.md)。[来源卡](../references/components/Tabs.md)。本次范围为顶端水平标签、自动激活、共享选中框、内容切换/高度过渡、溢出滚动；不包括垂直标签、手动激活、关闭/拖动重排、跨窗口拖出或forceMount API。

## WPF公开契约

Tabs继承TabControl，TabItem继承WPF TabItem。保留Items/ItemsSource、SelectedIndex/Item/Value及ValuePath、SelectionChanged、Header/HeaderTemplate、Content/ContentTemplate、ItemTemplate/ItemContainerStyle和原生自动化。TabStripPlacement强制Top；不另造字符串Value，用SelectedValuePath=Tag或数据路径。默认首个可选项及删除后的选择遵循WPF；禁用项不能由用户导航激活，允许程序赋值以遵循原生。

Left/Right按方向循环自动激活（RTL反转），Home/End首尾；仅处理本组标签焦点，不劫持内容内方向键或嵌套Tabs。Tab进入活动面板；Ctrl+Tab/Ctrl+Shift+Tab沿用原生。列表过长出现两个有可访问名称的滚动按钮，选中/聚焦项自动显露。自动化保留Tab/TabItem与Selection/SelectionItem。

只读HasOverflow、CanScrollBackward、CanScrollForward用于默认模板。默认HeaderAlignment=Left（HorizontalAlignment DP），主预览显式Center。内部部件PART_HeaderScroll(ScrollViewer)、PART_HeaderTrack(FrameworkElement)、PART_Selection(Border)、PART_SelectedContentHost(ContentPresenter)、PART_PanelFrame(FrameworkElement)、PART_Outgoing(Image)、PART_ScrollBackward/PART_ScrollForward(ButtonBase)均可替换/缺失时降级；保留原生PART_SelectedContentHost名称。

不修改使用方Width/Height/RenderTransform/绑定/DataContext。高度动画仅占用内部PanelFrame.Height，完成、Off、卸载、换模板后恢复Auto。模板替换解除滚动和布局监听及排队操作。旧快照不响应输入、不创建交互焦点，不再显示旧活内容；真实Content对象由TabItem保留，未承诺未选面板保持Loaded。

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
| 实际窗口/DPI/截图/性能 | 延期验收 | 未运行 | 不以程序化测试替代 |

未建立已审查WPF截图基准；交付时记录差异和实际测试结果。


