# RadioButton / RadioGroup：M2 规格

状态：代码与程序化验证完成，待用户验收；[来源卡](../references/components/RadioButton.md)。本次包括单项与竖向分组，不包括Radio cards、横向分组、虚拟化或必选验证。实际窗口/截图继续延期。

## 公开契约

RadioButton继承WPF RadioButton；Content为标签，Description(string，空)、Value(object，null)为新增DP；原生GroupName、Command/参数、Checked/Unchecked/Click、bool? IsChecked默认false双向保留。null强制false，IsThreeState强制false；已选项再次点击不取消。Description通过自动化HelpText暴露，显式HelpText优先。单项默认卡片外形；分组外使用GroupName进行原生互斥。

RadioGroup继承HeaderedItemsControl：Header标题、Items/ItemsSource、ItemTemplate/ItemContainerStyle；SelectedValue(object，null)默认双向，SelectedValueChanged冒泡事件。Items可直接放RadioButton；数据项自动生成容器，Value默认数据项自身，可用ItemContainerStyle绑定自定义Value。匹配采用Equals，第一个相等项胜出；null表示无选择，重复值应由使用方避免。无匹配值保留请求，暂时无高亮，后续项目出现后匹配。

组内使用内部唯一GroupName隔离互斥，忽略单项自行指定的组名；离组恢复原组名。组SelectedValue为选择权威，项IsChecked绑定可观察同步，避免同时驱动相互冲突的值。默认无选项时不自动选第一项。禁用项不能通过用户输入或组键盘选中，但可由数据设置选中。改变Value/增删项/卸载重载重新协调。

组内方向键按视觉顺序循环选择，跳过禁用/隐藏项；Left/Right受RTL影响，Up/Down按竖向顺序；Tab进出组仅一次；Space采用原生RadioButton语义。不人为添加Enter切换。单项UIA继承RadioButton的SelectionItem而不是Toggle；分组Header为分组名称。外部选择通知用SelectedValueChanged，保持绑定。

## 视觉与动效

按来源卡几何。圆环中心对齐整行内容块，不套CheckBox首行对齐；多行行高由内容撑开。标签与说明同起点。禁用是WPF扩展，整行opacity.5且禁止输入；焦点环为平台适配。

共享高亮从当前y/height使用ControlSpring .42移动；初次、窗口/行尺寸变化、Reduced/Off及重新加载直接定位。卸载/换模板停止订阅，失去匹配隐藏高亮。圆点scale.4→1使用.26，淡入160ms/淡出120ms；按压.88，未选预览.22；颜色按来源时长过渡。Full可反向，专有内部动画属性使用持久终态，不能移除时钟后闪回；Reduced/Off即时。ControlSpring是WPF临界阻尼适配，不保证与Web求解器完全相同。

RadioButton可选部件PART_Mark(Border)/PART_Dot(Border)/PART_Preview(Border)/PART_Row(Border)/PART_Label(ContentPresenter)。RadioGroup可选PART_Highlight(Border)、PART_ItemsHost(FrameworkElement)；缺失时语义仍可用，动画降级。控件不占用使用方RenderTransform或共享画刷，不修改DataContext。

## 交付矩阵与验收

| 项目 | 范围 | 状态 | 验收 |
|---|---|---|---|
| 原生单项语义/命令/自动化 | 必做 | 已实现 | 互斥、重复点击、禁用、绑定保留、UIA角色 |
| 分组选择/动态项/键盘 | 必做 | 已实现 | 双向值、组隔离、添加删除、方向导航；实际焦点待测 |
| 外形/内容/主题 | 必做 | 已实现 | 17/7中心、12间距、长文本/RTL、动态资源 |
| 圆点/按压/颜色/共享框动效 | 必做 | 已实现 | 真实时钟终态、反向、尺寸即时、卸载/换模板清理 |
| Gallery/Consumer | 必做 | 已实现 | 主样本/独立扩展，构造与构建 |
| 截图/跨DPI/实际键鼠与性能 | 延期验收 | 未运行 | 不以单测代替 |

尚未建立审查通过的WPF截图基准。适配差异与结果交付时补充。

## 实施收口

源码位于src/ArcWpf.Controls/RadioButton，Gallery和Consumer已接入。默认RadioGroupPanel只在行间保留8 DIP；可通过ItemsPanel替换布局，但本次仅验证竖向非虚拟化场景。

RadioButton另公开SelectedBrush、GlyphBrush、IndicatorBorderBrush三个Brush DP，默认null，由默认样式动态资源提供ControlOn、ControlGlyph、BorderStrong；便于资源覆盖。代码仅克隆内部绘制画刷，保持这三个公共属性的绑定。缺省模板下内部部件的颜色、透明度及变换归动效引擎管理。

14项新增、全量84项测试通过；详情和证据边界见 [验证记录](../verification/2026-10-26-radio.md)。共享高亮的WPF叠放/选中悬停、独立项无共享框、字体和求解器差异已记录。实际窗口和跨DPI待用户测试。
