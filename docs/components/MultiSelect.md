# MultiSelect

状态：当前版本已获用户人工测试通过反馈（2026-10-26）；专项未测边界保留。范围按本次用户要求加入 M3 表单组；来源已固定，见[参考卡](../references/components/MultiSelect.md)。

## 可实施契约

原生 Control 组合 ListBox（Multiple）、Search（TextBox 派生）与 Popup；不继承单选 ComboBox，不接管 DataContext。Options 为 IEnumerable<MultiSelectOption>，不可变记录 Value/Label/IsEnabled；SelectedValues 为 IList<string> DP，默认每实例空集合、默认双向绑定。用户选择提交新的去重集合并保留绑定；外部 ObservableCollection 增删刷新，数组/普通列表变更需重新赋值。筛选不能丢弃隐藏选择；选项消失保留值，未知值显示原字符串。重复 Value 采用第一项。

Label/Description/Placeholder/SearchPlaceholder/EmptyText 为字符串；MaxVisible 默认2且>=0；MaxDropDownHeight 默认320且有限正数；IsDropDownOpen 默认false双向；SearchText 默认空双向；IsSearchEnabled/CanRemoveTags 默认true（扩展主例关闭以对照原版）；IsReadOnly 默认false。只读 IsPresented/HasSelection，SelectionChanged 路由事件。禁用/只读不允许用户修改，外部绑定仍更新。保留 Validation.HasError 模板反馈。

部件：PART_Trigger、PART_Clear、PART_Tags、PART_Placeholder、PART_Chevron、PART_Popup、PART_MenuFrame、PART_MenuSurface、PART_Search、PART_List、PART_Empty。缺部件不崩溃；换模板/卸载清理旧时钟与订阅。

键盘：触发器 Enter/Space 开关，Up/Down 打开并导航；菜单方向键绕过禁用并循环，Enter/Space 切换当前项；Search 保留 Space/编辑/撤销/IME，合成时不处理选择键；Escape 关闭并归还触发器焦点，Tab 关闭后移至控件外。删除标签保留菜单状态；Clear 清空并关闭。UIA 保留原生列表多选/选项及 Search Value，外层增加 ExpandCollapse 和选择摘要。

## 完整度与动效合同

| 必做项 | 实现目标 | 验证计划 |
|---|---|---|
| API/绑定 | 双向替换、外部集合通知、筛选后保留选择、禁用/只读 | STA绑定与集合测试 |
| 外形/内容 | 单行44高、标签144上限、+N、长文案/180窄宽、clear槽 | 同祖先外框/内容/槽/父容器坐标与时钟中间帧 |
| 标签增减/布局 | 槽宽.4弹簧近似、scale/blur/opacity，保留退场对象 | 反向、终态、清除中间帧 |
| +N/placeholder | 计数滚动双层退出/进入、占位淡化 | 多标签增删与终态 |
| clear/箭头 | 显隐缩放/模糊/淡化、按压.96、旋转180 | Full/Reduced/Off |
| 菜单/选项 | 进入退出、真实多选行、绘制勾号、80ms颜色 | Popup、筛选、退出重开 |
| 颜色/主题 | 外框、标签、操作160ms；保留资源与绑定 | 主题切换/应用覆盖 |
| 生命周期 | 清理全部独占时钟、旧回调失效 | 卸载/换模板/禁用 |
| Gallery | 文档主例与独立扩展绑定/搜索/窄宽/错误 | 构造与实际窗口烟雾 |

所有动效自有属性先持久写入目标基值，再移除时钟；快速反向从当前显示值重定向，不保速度；Reduced无宽度/位移/缩放/模糊、仅短淡化，Off直接终态。来源事实参数集中参考卡。用户视觉验收、IME、多DPI、读屏分别记录，不以程序测试代替。

暂缓：自由文本创建、异步加载、虚拟化（均非本次要求）。实际窗口/视觉由用户人工验证；2026-10-26 用户报告标签遮挡，修正后程序化及离屏结果已通过，实际效果待复验。详见[验证记录](../verification/2026-10-26-multiselect.md)。


## 实施结果与差异（2026-10-26）

上表必做项均已实现：代码在 src/ArcWpf.Controls/MultiSelect，模板在其 Themes 子目录；Gallery 在 MultiSelectPage；专项在 MultiSelectTests。10项专项通过（含旧实现失败的新标签布局回归），逐项证据见验证记录。程序化结果不代表人工交互/视觉通过。

MaxVisible 是上限：窄容器按每项至少24 DIP文本预算及删除操作宽度减少可见标签，多余值进入 +N。原版只按 maxVisible 截取并 flex-shrink；本项目在用户反馈后改为保留可读标签。文字天然宽度采用当前字体/方向/DPI的 FormattedText，向上取整并留1物理像素，避免浮点边界触发省略号。整框44 DIP与箭头位置不变。

菜单原生 Popup 边界碰撞翻转/可用高度限制来自既有 Select/Popover机制；搜索、逐标签删除、Tab离开关闭为WPF扩展。弹簧是 OwnedMotion 的固定采样近似，不是 Motion 原求解器，不保反向速度；阴影与字体栅格化非Web像素等效。全量基线未声明通过。

公开状态更新均在UI线程。SelectedValues可为null（视为空）；未知Value保留且显示原字符串。重复Options Value采用第一项，选中值按首次出现去重展示；不直接改变使用方集合的内容。只有外部集合通知/重新赋值时重算；MultiSelectOption是不可变记录。

API/绑定、外形/内容、逐项动效、主题/输入和Gallery均为“已实现，证据范围见验证记录”；IME真实候选交互、多DPI、读屏及最终视觉仍待用户验收。尚未建立已审查WPF像素基准。
