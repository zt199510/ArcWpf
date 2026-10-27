# SegmentedControl

状态：本次实现完成，8项专项通过；实际界面待用户人工验收。水平单选分段选择器；仅执行本控件专项，不使用computer-use，真实界面由用户人工验收。来源冻结见[参考卡](../references/components/SegmentedControl.md)。

## WPF契约

SegmentedControl继承ListBox，SelectionMode强制Single。沿用Items/ItemsSource、SelectedIndex/SelectedItem/SelectedValue/SelectedValuePath、SelectionChanged、ItemTemplate/ItemContainerStyle；不新造字符串Value。默认未选中，调用方显式给初值；选中项再次点击不取消。SegmentedItem继承ListBoxItem，Content/ContentTemplate为标签，Icon在前、Accessory在后，两者可为任意对象；无内容时不保留槽。Label是自动化组名称，不增加可见标题。OptionIntent为路由事件，参数Item是数据项，鼠标进入/获得焦点触发，不等于选中事件。

方向键自动选中并聚焦、循环跳过禁用/隐藏项，Home/End首末；RTL反转左右，Up/Down仍按逻辑前后。Tab只有当前选中可用项可进入，无选中或选中项禁用时以首个可用项为入口。Enter/Space选择当前项。禁用整个控件和单项都阻止用户选择；程序可选禁用项以保留WPF语义。原生Selection/SelectionItem自动化；角色List/ListItem是有意WPF映射。

默认外框border1/radius18，内部padding3，项间gap2，项min-height36/padding12,0/radius14，正常行高44。内容不换行、不压缩；横向隐藏滚动条，选中项显露并预留20 DIP，左右溢出边缘渐变；鼠标滚轮滚动横向内容。根Width/Height属于整体，内部高度是最小值，图标/标签较高时允许增高。

模板：PART_Scroll(ScrollViewer)、PART_Track(FrameworkElement)、PART_Selection(Border)。换模板/卸载/隐藏/禁用停止自有动画和排队回调；缺部件降级仍保留选择API。不修改用户DataContext或公开绑定。

## 完整度矩阵

| 交付项 | 本次范围 | 状态 | 证据/差异 |
|---|---|---|---|
| 单选/绑定/动态集合 | 必做 | 已实现；专项通过 | WPF ListBox单选模型 |
| 文字/图标/Accessory | 必做 | 已实现；专项通过 | 图标前置为WPF扩展，Accessory源站支持 |
| 外形/主题/禁用 | 必做 | 已实现；专项通过 | 44整体/36项高，动态语义资源 |
| 滑动背景/文字颜色 | 必做 | 已实现；专项通过 | morph .42秒临界阻尼、颜色160ms，反向保速 |
| 溢出/淡出/显露 | 必做 | 已实现；专项通过 | 像素ScrollViewer，20 DIP边缘与显露余量 |
| 输入/UIA/OptionIntent | 必做 | 已实现；专项通过 | 原生选择peer，用户输入路径专项 |
| 生命周期/Reduced/Off | 必做 | 已实现；专项通过 | Reduced/Off背景与颜色即时；无闲置帧订阅 |
| Gallery | 必做 | 已实现；专项通过 | Day/Week/Month主例Week；中文/图标/禁用/长项/绑定 |
| 垂直/多选/内容面板 | 不适用 | 不实现 | 本次水平互斥视图选择；面板使用Tabs |

CSS viewport≤380的padding10不按WPF控件宽度套用，本次保持padding12并滚动。无Web弹簧逐帧等价承诺；自定义字体、极长内容、DPI及真实读屏待人工。

最终证据见[验证记录](../verification/2026-10-27-segmented-control.md)。未执行其他控件/全量测试，实际键鼠与多DPI、读屏保留待验收。

2026-10-27用户反馈选择后出现与原版不符的黑色边框：移除项目额外添加的2 DIP FocusVisualStyle，选中背景和原版1 DIP细边框保留；键盘选择、聚焦和自动化契约不变。实际效果待用户复验。
