# Select：M2 规格

状态：代码与程序化验证完成，待用户验收。来源见[参考卡](../references/components/Select.md)。不做可编辑输入、多选、搜索框或分组列表。

## API与行为

Select继承WPF ComboBox，IsEditable强制false。保留Items/ItemsSource、SelectedItem/SelectedValue/SelectedValuePath、DisplayMemberPath、ItemTemplate/ItemContainerStyle、SelectionChanged、IsDropDownOpen、MaxDropDownHeight。选择绑定使用原生默认双向契约，DisplayMemberPath或TextSearch.TextPath负责文字搜索。新增Label(string空)、Description(string空)、Placeholder(string默认Select an option)；只读IsPresented表示实际弹层显示。

SelectItem继承ComboBoxItem，使用Content/IsEnabled，禁用项不可通过用户操作选择；允许程序赋值选中禁用项以遵循WPF。不另造Options模型。直接SelectItem配合SelectedValuePath=Tag或数据对象配合SelectedValuePath。

关闭时Enter/Space/无修饰Down打开；原生Alt+Down/F4仍可用。打开后方向键/前缀搜索和提交沿用ComboBox，Escape取消当前高亮而保留原选择；Tab关闭后按WPF焦点规则离开。原生关闭状态Up/Home/End选择行为属于桌面适配。DropDownOpened为逻辑打开通知，DropDownClosed沿用实际Popup关闭通知；不新增承诺未经验证的全局事件顺序。

模板必要PART_Popup、PART_Trigger；缺失时关闭请求复位，不产生独立孤窗。其他动效部件可缺省降级。外部点击由原生ComboBox捕获处理；Owner失活/关闭、卸载、换模板、锚点隐藏或被裁剪立即关闭。Owner移动/尺寸/DPI/布局变化重定位。Popup桥接DataContext/FlowDirection/ArcMotion，命中关闭后立即禁止，动画重开取消旧完成回调。

自动化继承ComboBox角色、ExpandCollapse/Selection，Name优先显式值再Label；HelpText优先显式值再Description；动画旧值装饰不作为交互控件。无Label应显式命名。

## 外形与动效

按参考卡几何和间距，有限宽度触发器文字省略号，不为长文本扩宽。标签/说明与触发框左边缘对齐；触发器文字内缩13。控件整体Width包含标签说明，Popup只锚定44高触发框。

文本Full240/160ms方向位移与模糊；Reduced仅120ms交叉淡化，Off即时。首次/未加载/自定义ItemTemplate立即放置，不复播历史；连续变化清理旧快照，只保留最近过渡。触发框颜色160ms；箭头Full580ms旋转，Reduced/Off即时。菜单按参考卡；指示器Full240ms延迟40，Reduced/Off立即；选项高亮颜色120ms。内部属性保存持久终态，不能使用永久时钟遮掩错误；卸载/换模板/等级变化取消旧时钟。

## 交付矩阵

| 项目 | 范围 | 状态 | 验收 |
|---|---|---|---|
| 选择/绑定/禁用/API | 必做 | 已实现 | 原生绑定/容器/数据对象动态变化 |
| 触发器/菜单/项布局 | 必做 | 已实现 | 44/36/13边距及截断、菜单锚定 |
| Popup/生命周期/定位 | 必做 | 已实现 | 纯计算+无效宿主；实际焦点/DPI待测 |
| 动效/终态/打断 | 必做 | 已实现 | 真实Dispatcher时钟完成清理与重开代次 |
| Gallery/Consumer/说明 | 必做 | 已实现 | 同源主示例及独立扩展 |
| 实际键鼠/读屏/跨DPI/截图 | 延期验收 | 未运行 | 不把单测称为视觉通过 |

WPF滚动条替代源站滚动按钮，自定义项模板跳过文字滚动过渡、保留原生显示；字体与阴影近似明确保留。尚未建立审查通过的截图基准。

## 实施收口

代码位于src/ArcWpf.Controls/Select，Gallery与Consumer已接入。新增12项、全量96项程序化测试通过。完整证据和未测边界见 [验证记录](../verification/2026-10-26-select.md)。原生Popup实际窗口及视觉仍待验收。
