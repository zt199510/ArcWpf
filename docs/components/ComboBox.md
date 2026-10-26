# ComboBox

状态：本次实现完成，ComboBox专项9/9通过，实际窗口待用户人工验收。来源已冻结，见[参考卡](../references/components/ComboBox.md)。本次只运行ComboBox专项，不运行其他控件测试、全量回归或computer-use；实际窗口交用户人工测试。

## 契约

公开名ArcWpf.Controls.ComboBox，继承Input（原生TextBox）。保留TextBox编辑/粘贴/撤销/IME/选区及Input标签、说明、ErrorMessage/绑定验证。单选由内部原生ListBox承载，不接管DataContext。名称与系统ComboBox同名，原有Select和Gallery中的系统类型使用完整命名，行为不变。

Options: IEnumerable<ComboBoxOption>，记录Value/Label/IsEnabled=true/Keywords=null。集合通知刷新；重复Value采用第一项；空Value保留为空选择语义。SelectedValue:string默认空、双向；未知Value保留但字段显示空（与原版selectedOption查找一致）。QueryText:string默认空双向，打开时同步原生Text，选中和关闭时清空。Text是当前显示文本，使用方应绑定SelectedValue/QueryText而非把Text当选中值。MaxDropDownHeight默认300，有限正值；EmptyMessage默认No matches found；Placeholder默认Search or select…。IsDropDownOpen默认false双向，HasSelection/IsPresented/DisplayPlaceholder只读；SelectedValueChanged路由事件（保留TextBox.SelectionChanged文本选区事件）。

原生IsReadOnly/IsEnabled禁止用户选择、清空、输入；外部赋值仍更新。单行输入，不支持自由文本提交、业务网络、自动新增、虚拟化。

焦点进入/点击文本打开全列表；输入过滤但不提交值；方向键打开或循环活动项、跳过禁用，保持编辑焦点，Enter提交；Escape和外部点击关闭并丢弃查询；Tab正常离开同时关闭。菜单点击保持编辑焦点；clear支持Tab聚焦及原生按钮激活，清除后返回编辑器；组合输入期间不处理Enter/Escape/方向键提交，不强制重写组合文本。UIA保留原生TextBox Value/Text，补ExpandCollapse和原生列表Selection；选项名称为Label。

部件：PART_ContentHost（编辑必需）、PART_Trigger、PART_TextVisual、PART_Clear/Visual、PART_Chevron、PART_Popup、PART_MenuFrame/Surface、PART_List、PART_HeightHost、PART_Empty。可选部件缺失不崩溃；卸载/换模板解除旧部件事件和Popup订阅、清理时钟与延迟回调。

## 完整度与验收矩阵

| 项目 | 本次范围 | 实现状态/验收目标 |
|---|---|---|
| API/绑定/关键词 | 必做 | 已实现；查询不覆盖选择，选中/清空保持绑定，外部集合更新 |
| 外形/对齐/显隐槽 | 必做 | 已实现；44高、180窄宽/长文本；占位与首字符对齐；清除前中后外框与箭头稳定、文本宽按来源重排 |
| 颜色 | 必做 | 已实现；外框/操作160ms，行80ms；主题刷新与绑定 |
| 菜单/高度动效 | 必做 | 已实现；进退场、筛选高度.4近似弹簧；保留退出宿主 |
| 选中文案/空态 | 必做 | 已实现；文字位移/模糊/淡化，空态进入 |
| clear/chevron | 必做 | 已实现；显隐/按压/旋转；快速反向/终态 |
| 键盘/IME/自动化 | 必做 | 已实现；原生编辑；程序化组合输入保护；实际IME/读屏待人工 |
| 生命周期/动效等级 | 必做 | 已实现；Reduced仅淡化高度即时，Off终态；卸载/换模板/反向 |
| Gallery | 必做 | 已实现；文档主例、绑定/关键词/窄宽/长文本/禁用/验证扩展 |
| 网络/虚拟化/自由输入创建 | 不适用 | 不属于本次需求 |

每项动效参数与出处见参考卡。独占内部属性保存持久基值并清除时钟，不能覆盖公共绑定。静态/时钟程序化证据不能替代实际窗口视觉。首次回归基准尚未人工审查。

验证证据见[ComboBox验证记录](../verification/2026-10-26-combobox.md)。

