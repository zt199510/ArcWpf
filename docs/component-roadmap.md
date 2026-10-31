# ArcWpf 控件交付路线图

更新时间：2026-10-27  
范围：`src/ArcWpf.Controls`、`samples/ArcWpf.Gallery`、`tests/ArcWpf.Tests` 与 `docs/components` 的当前盘点。  
规则：代码存在不等于交付完成；“待验收”表示程序化检查已有结果但真实窗口仍需用户复验；“未收口”表示仍有代码、规格或专项验证缺口。

## 当前结论

当前 Gallery 已接入 38 个控件页面，公共控件目录也有对应实现目录。下一项不应再重复创建已存在的 Tooltip、PresenceStack 或 Avatar 系列，而应先完成真正缺失的 TreeView，并收口 Breadcrumb 与首版验证缺口。

PresenceStack 最近一次修正已根据公开页面 HTML/CSS调整，专项 9/9 通过；其真实窗口效果仍需用户复验，保留为“待验收”。

## 已有实现，等待用户窗口复验

这些控件已有代码、规格和程序化验证记录，当前工作是按 Gallery 页面逐项检查真实窗口、主题、焦点、Popup、DPI 和读屏边界：

- Accordion、Alert、Avatar、AvatarGroup、Badge、Button、CheckBox、ComboBox、Dialog、Drawer、ExpandableCard、FormLayout、Popover、PresenceStack、RadioButton、SegmentedControl、Slider、Switch、Tabs、Textarea、Toast、ToastStack、Tooltip。
- Calendar、DatePicker、Input、MultiSelect、OTPInput、Select 已有用户确认或部分确认，但仍保留未覆盖的中文输入、DPI、Popup、读屏或边界专项。
- PasswordInput 已有专项通过，仍待实际窗口验收。

窗口复验顺序按风险安排：先 Popup/焦点类（Popover、Tooltip、Dialog、Drawer、Select、ComboBox、DatePicker、MultiSelect、ToastStack），再输入类（Input、Textarea、PasswordInput、OTPInput、NumberInput、Slider），最后静态和展示类（Avatar、AvatarGroup、Badge、PresenceStack、Accordion、ExpandableCard、Progress、List）。

## 需要先补代码或专项验证

|优先级|控件|当前缺口|完成条件|
|---|---|---|---|
|P0|TreeView|代码、规格、专项验证和用户手动验收已完成|完成原生层级绑定、展开/选择/键盘/UIA、模板、主题、Gallery、专项测试和验证记录|
|P0|Breadcrumb|代码、专项验证和用户手动验收已完成|完成节点模型、当前项/链接命令、键盘/UIA、溢出策略、Gallery和专项测试|
|P1|Menu|代码、专项验证和用户手动验收已完成|完成修正后的键盘、子菜单、Popup焦点、主题与专项回归|
|P1|NumberInput|代码、专项验证和用户手动验收已完成|补齐边界、步进、键盘、绑定、禁用、Reduced/Off和专项测试|
|P1|Progress|代码、专项验证和用户手动验收已完成|补齐确定/不确定、值绑定、文本、主题、动效和专项测试|
|P1|Calendar/DatePicker|共享模板、专项验证和用户手动验收已完成|完成月份/年视图、范围、黑名单、Popup焦点和专项回归|
|P1|Pagination|代码、专项验证和用户手动验收已完成|补齐页码溢出、边界、键盘/UIA、绑定和完整专项验证|
|P2|List|代码和专项验证已完成，视觉窗口已通过用户手动验收|复核行命中、空状态、滚动、选中/禁用和主题视觉|
|P2|Search|代码、Gallery、专项验证和用户手动验收已完成|补充当前规格、专项结果和实际缺口；若功能完整则转入待验收|
|P2|Calendar|已有用户确认记录，边界专项按验证记录保留|按 DatePicker 共同宿主完成真实窗口和多DPI复验|

## 推荐实施顺序

### 阶段 A：补齐真正缺失控件

1. **TreeView**：先做原生 `TreeView` / `TreeViewItem` 适配，保留层级、选择、展开、键盘和 UIA。
2. **Breadcrumb**：复用 TreeView 的节点语义与现有 Tooltip/Popover 的溢出处理经验。
3. **Menu 收口**：完成当前正在修正的版本，再进入下一项，避免叠加 Popup 焦点问题。

### 阶段 B：完成首版验证缺口

4. NumberInput、Progress、Pagination。  
5. Calendar 与 DatePicker 的共享 Popup/月份视图专项。  
6. List、Search 的规格状态补齐与视觉回归。

### 阶段 C：统一窗口验收

7. Popup/焦点类。  
8. 输入与范围类。  
9. 头像、状态、展示类。  
10. 最后更新总计划中的 M1/M2/M3 状态，不用测试通过替代真实窗口验收结论。

## 每个控件的退出条件

- 规格明确 API、模板部件、键盘、UIA、主题、动效和有意差异。
- Gallery 有主示例和边界示例，公共控件不依赖 Gallery。
- 只运行当前控件专项测试；测试结果、构建命令和程序集版本写入验证记录。
- Full、Reduced、Off、卸载/重载、换模板和快速反向路径有证据。
- 实际窗口验收单独记录，不把离屏渲染或单元测试写成用户视觉通过。
- 外部来源、许可证和复用范围写入参考卡或第三方声明。

## 下一项

当前批次已完成用户手动验收；下一步进入 Arc 后续组件 M4-A。开始前读取 `docs/components/TreeView.md`、`docs/references/components/TreeView.md`、`docs/architecture.md` 和相关控件 Skill；完成后再进入 Breadcrumb。


## Arc 后续补齐建议（2026-10-28）

来源：Arc 官方组件目录 https://uiarc.dev/components，2026-10-28 读取。页面当前显示 Components 130；All/Free/Pro 筛选为 293/137/157，目录还包含 Blocks 与 Special components。以下只把适合 ArcWpf 公共控件库的组件纳入候选，不把 Pro 组件或图表页面当作近期承诺。

### 推荐下一批：交互原语（M4-A）

1. `ActionButton`：补齐 Button 与异步提交/成功状态之间的语义边界。
2. `CopyButton`：复用 Button 的状态 morph，加入剪贴板成功/失败和键盘反馈。
3. `ButtonGroup`（2026-10-28 已实现，待 Windows 验收）、`SplitButton`：建立按钮组布局、默认动作与菜单动作组合。
4. `ContextMenu`、`UserMenu`：复用 Menu 的 Popup、焦点归还、Escape 和键盘导航。
5. `HoldToConfirm`：补充危险操作确认和按住进度，复用 Reduced/Off 动效合同。
6. `InlineEdit`：复用 Input 的编辑、提交、取消、验证和焦点归还。
7. `ExpandingSearch`：在 Search 基础上增加进入/退出容器和触发器焦点管理。

### 第二批：表单和选择（M4-B）

`PasswordStrength`、`MoneyInput`、`PhoneInput`、`TagInput`、`ShortcutRecorder`、`ChipGroup`、`Rating`、`DateRangePicker`、`TimePicker`、`ColorPicker`。其中 DateRangePicker/TimePicker/ColorPicker 应先冻结文化格式、Popup、键盘和读屏契约，再实现；Money/Phone 的格式化不能破坏 IME 和撤销。

### 第三批：布局、反馈和容器（M4-C）

`ScrollArea`、`BottomSheet`、`HoverCard`、`ResizablePanels`、`Card`、`MetricCard`、`EmptyState`、`Skeleton`、`Stepper`、`UsageMeter`。这一批能直接提升 Gallery 与消费应用的页面构成，且可复用现有 Popup、Drawer、Accordion、Progress 和主题令牌。

### 第四批：数据展示（M4-D）

`SortableDataTable`、`ReorderableList`、`FilterToolbar`、`TreeTable`、`CodeBlock`、`JSONViewer`。建议先做 `FilterToolbar` 和 `ReorderableList`，再决定是否为 DataGrid/TreeTable 引入独立虚拟化和列模型；不要把 WPF 原生 DataGrid 的复杂契约偷偷扩展进基础控件。

### 暂缓

`RichTextEditor`、`SignaturePad`、图表 14 项、Advanced charts、Carousel/Card stack、Text effects、Media/Activity 复杂组件以及所有 Pro 条目。它们分别涉及编辑器模型、绘制/命中测试、数据可视化、手势或付费来源边界，应单独立项，不作为下一批连续移植。

### 建议的实际开工顺序

`ActionButton → CopyButton → ButtonGroup/SplitButton → ContextMenu/UserMenu → InlineEdit → ExpandingSearch → DateRangePicker → TimePicker → ScrollArea → EmptyState/Skeleton → FilterToolbar → ReorderableList`。

每个组件继续沿用现有退出条件：规格/参考卡、主示例和边界示例、专项测试、Full/Reduced/Off 与卸载/换模板清理、最终窗口验收。此次用户已完成当前批次手动验收，因此后续记录应从上述新组件开始，不能把当前批次的程序化证据复制成新组件的窗口通过。




## CopyButton 开发（2026-10-28）

CopyButton 已开始实现，范围为 Clipboard 复制、成功反馈、Copy/Check phase、文字 morph 和宽度 spring。



## SplitButton 开发（2026-10-28）

SplitButton 已实现主动作与 Menu Popup 组合，SplitButtonTests 2/2 通过；实际窗口待人工验收。



## ContextMenu 开发（2026-10-28）

ContextMenu 已实现宿主附加、右键/键盘打开、MenuItem 导航和焦点归还；ContextMenuTests 3/3 通过，实际窗口待人工验收。


## HoldToConfirm 开发（2026-10-29）

- [x] 冻结 Arc Hold to confirm Registry、CSS、Markdown、页面、动效令牌和许可快照；见 	ests/Baselines/References/2026-10-29-hold-to-confirm。
- [x] 完成参考卡与规格，明确按住填充、提前松开回退、Space/Enter、失焦、禁用和 tone。
- [x] 实现原生 WPF HoldToConfirm、主题、Gallery 和 3 项专项测试；Controls/Gallery 构建通过。
- [ ] 实际窗口的按住观感、鼠标捕获、明暗主题、Reduced/Off、多 DPI 和触摸验收。

## InlineEdit 开发（2026-10-29）

- [x] 冻结 Arc Inline edit Registry、CSS、Markdown、页面、动效令牌和许可快照；见 	ests/Baselines/References/2026-10-29-inline-edit。
- [x] 完成参考卡与规格，明确展示/编辑、Enter/Escape、失焦、异步保存、验证、失败回滚和多行模式。
- [x] 实现原生 WPF InlineEdit、主题、Gallery 和 3 项专项测试；Controls/Gallery 构建通过。
- [ ] 实际窗口原位尺寸、中文 IME、焦点、长文案、多行高度、明暗主题和 Reduced/Off 验收。

## HoldToConfirm / InlineEdit 复刻修正（2026-10-31）

- [x] 对照冻结源码重做两控件模板与行为，Gallery 主示例改为源站内容；解决方案构建通过。
- [ ] 在 Windows 上运行 HoldToConfirmTests（7）/InlineEditTests（6）并完成实际窗口验收（本机无法运行 WPF）。

## ExpandingSearch 开发（2026-10-31）

- [x] 实现 collapsed/expanded 搜索触发器、输入、清除、Escape、Enter、焦点归还和宽度动效。
- [x] 接入主题、Gallery 和 3 项专项测试。
- [ ] Arc ExpandingSearch Registry/CSS 来源冻结待网络恢复；真实窗口比例、输入起点、主题和 Reduced/Off 待验收。

## ExpandingSearch 截图对照修正（2026-10-31）

- [x] 展开后补齐 Recent 信息面板、条目图标、标题和元数据，面板与输入框同步展开宽度。
- [ ] Arc Registry/CSS 来源冻结和真实窗口比例、输入起点、主题、Reduced/Off 验收待补。

## DateRangePicker 开发（2026-10-31）

- [x] 基于 ArcWpf Calendar/DatePicker 实现双 Calendar Popup、范围选择、边界、BlackoutDates、Clear、Gallery 和 3 项专项测试。
- [ ] Arc DateRangePicker Registry/CSS 来源冻结、范围 band/hover 预览、真实窗口比例、Popup 焦点、多 DPI 和主题验收待补。

## TimePicker / ScrollArea 开发（2026-10-31）

- [x] 实现 TimePicker 时间列表 Popup、MinuteIncrement、SelectedTime 和 ScrollArea 原生 ScrollViewer 宿主。
- [x] 接入主题、Gallery 页面和专项测试；Controls 构建通过，专项 6/6 通过。
- [ ] Arc Registry/CSS 来源、Gallery 完整构建、真实窗口 Popup/滚动/主题/多 DPI 验收待补。

## ExpandingSearch / DateRangePicker / TimePicker / ScrollArea 源码复刻（2026-10-30）

- [x] 冻结四控件 Registry、CSS、Markdown、页面、动效令牌和许可；补参考卡。
- [x] 按源码重做模板、行为、动效与 Gallery 主示例，重写专项测试；Controls/Gallery/Tests 构建 0 错误。
- [ ] 在 Windows 运行 16 项专项测试并完成实际窗口验收（见 [验证记录](verification/2026-10-30-arc-source-rebuild.md)）。

## EmptyState 开发（2026-10-31）

- [x] 建立明确标记的 WPF fallback EmptyState，包含图标、标题、说明、主操作和 UIA。
- [x] 接入主题、Gallery 和专项测试；Controls 构建通过，EmptyState 专项 3/3 通过。
- [ ] Arc EmptyState Registry/CSS 来源恢复后网页参数、图标、间距和动效复核；真实窗口验收待补。
