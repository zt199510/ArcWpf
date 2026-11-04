# ArcWpf 控件交付路线图

更新时间：2026-11-02
范围：`src/ArcWpf.Controls`、`samples/ArcWpf.Gallery`、`tests/ArcWpf.Tests` 与 `docs/components` 的当前盘点。  
规则：代码存在不等于交付完成；“待验收”表示程序化检查已有结果但真实窗口仍需用户复验；“未收口”表示仍有代码、规格或专项验证缺口。

## 当前结论

2026-11-02：AnnouncementBar、AnimatedCounter、Countdown、Timeline、AvatarUpload、FileDropzone、MentionInput、EmojiPicker 已建立 WPF 控件、Arc 主题模板、Gallery 合并示例与基础专项测试。Controls/Gallery 构建通过，专项测试 8/8 通过；真实窗口视觉、Popup、IME、拖放、DPI 与逐帧动效仍待验收。AvatarUpload 和 EmojiPicker 为 Arc Pro 公开资料独立实现，未复用 Pro 源码。 随后已对照线上页面重做，见 [像素记录](verification/2026-11-02-next-controls-pixel.md)。

2026-11-02：当前已纳入路线图的控件均已完成代码、规格、主题、Gallery 和专项验证收口。TreeView、Breadcrumb、Menu、NumberInput、Progress、Pagination、Calendar/DatePicker、List、Search，以及后续的输入类、布局类、反馈类和容器类批次均不再作为“下一项”重复开发。

收口结论以当前提交和各组件验证记录为准；真实窗口、物理输入、读屏、多 DPI、性能或逐帧视觉若在对应记录中标为待验收，仍保持该证据边界，不被本次代码收口覆盖。

PresenceStack 最近一次修正已根据公开页面 HTML/CSS调整，专项 9/9 通过；其真实窗口效果仍需用户复验，保留为“待验收”。

## 已有实现，等待用户窗口复验

这些控件已有代码、规格和程序化验证记录，当前工作是按 Gallery 页面逐项检查真实窗口、主题、焦点、Popup、DPI 和读屏边界：

- Accordion、Alert、Avatar、AvatarGroup、Badge、Button、CheckBox、ComboBox、Dialog、Drawer、ExpandableCard、FormLayout、Popover、PresenceStack、RadioButton、SegmentedControl、Slider、Switch、Tabs、Textarea、Toast、ToastStack、Tooltip。
- Calendar、DatePicker、Input、MultiSelect、OTPInput、Select 已有用户确认或部分确认，但仍保留未覆盖的中文输入、DPI、Popup、读屏或边界专项。
- PasswordInput 已有专项通过，仍待实际窗口验收。

窗口复验顺序按风险安排：先 Popup/焦点类（Popover、Tooltip、Dialog、Drawer、Select、ComboBox、DatePicker、MultiSelect、ToastStack），再输入类（Input、Textarea、PasswordInput、OTPInput、NumberInput、Slider），最后静态和展示类（Avatar、AvatarGroup、Badge、PresenceStack、Accordion、ExpandableCard、Progress、List）。

## 已收口控件索引

|优先级|控件|当前缺口|完成条件|
|---|---|---|---|
|P0|TreeView / Breadcrumb / Popover / CheckBox|已收口|代码、规格、Gallery、专项测试和验证记录已同步|
|P1|Menu / NumberInput / Progress / Calendar / DatePicker / Pagination|已收口|代码、主题、键盘、绑定、专项测试和验证记录已同步|
|P2|List / Search|已收口|代码、Gallery、规格和专项测试已同步|
|M4 批次|ActionButton、CopyButton、ButtonGroup、ContextMenu、UserMenu、HoldToConfirm、InlineEdit、ExpandingSearch|已收口|按来源规格完成组件、Gallery、专项测试和文档记录|
|M4 批次|DateRangePicker、TimePicker、ScrollArea、EmptyState、Skeleton、FilterToolbar、ReorderableList|已收口|按来源规格完成组件、Gallery、专项测试和文档记录|
|M4 批次|PasswordStrength、MoneyInput、PhoneInput、TagInput、ShortcutRecorder、ChipGroup、Rating、ColorPicker|已收口|按来源规格完成组件、Gallery、专项测试和文档记录|
|M4 批次|BottomSheet、HoverCard、ResizablePanels、Card、MetricCard、Stepper、UsageMeter|已收口|按来源规格完成组件、Gallery、专项测试和文档记录|

## 历史实施顺序（已完成）

### 阶段 A：补齐真正缺失控件（已完成）

1. **TreeView**：先做原生 `TreeView` / `TreeViewItem` 适配，保留层级、选择、展开、键盘和 UIA。
2. **Breadcrumb**：复用 TreeView 的节点语义与现有 Tooltip/Popover 的溢出处理经验。
3. **Menu 收口**：完成当前正在修正的版本，再进入下一项，避免叠加 Popup 焦点问题。

### 阶段 B：完成首版验证缺口（已完成）

4. NumberInput、Progress、Pagination。  
5. Calendar 与 DatePicker 的共享 Popup/月份视图专项。  
6. List、Search 的规格状态补齐与视觉回归。

### 阶段 C：统一窗口验收（按各记录持续维护）

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

## 下一阶段

数据展示控件批次已开始：CodeBlock、JSONViewer、SortableDataTable、TreeTable 已完成实现、Gallery 和精确专项验证。后续如新增 Arc 控件，另立来源分析和交付批次；不覆盖本路线图已收口结论。

### 2026-11-03 Carousel 开发

- [x] 冻结 Arc Carousel Registry、CSS、Markdown、页面、动效令牌和许可快照；见 `tests/Baselines/References/2026-11-03-carousel`。
- [x] 完成 WPF Carousel、Arc 主题模板、Gallery 主示例和 4 项专项测试。
- [x] 增加连续拖拽时相邻卡片的缩放/变暗反馈与横向鼠标滚轮导航。
- [x] 增加触摸捕获、连续位移反馈和触摸释放回位。
- [x] 增加最近指针速度采样和 flick 阈值翻页。
- [x] 使用 Owned/Tween spring 处理可打断的回位，并将释放速度投影到翻页判定。
- [x] 指示器改为 Arc 风格的活动 pill，支持索引更新和点击选择。
- [ ] 真实窗口的触摸、甩动惯性、明暗主题、多 DPI 和逐帧动效验收。

本批次后续顺序为 RadioCards → CookieConsent → Reactions → SwipeActions；当前四项均已完成实现入口。

### 2026-11-03 RadioCards 开发

- [x] 冻结 Arc RadioCards Registry、CSS、Markdown、页面、动效令牌和许可快照；见 `tests/Baselines/References/2026-11-03-radio-cards`。
- [x] 建立 WPF RadioCards、主题模板、Gallery 页面和 4 项专项测试。
- [x] 最小列宽真正传递到卡片容器，禁用项同步到 WPF `IsEnabled`。
- [x] 增加独立 overlay 选中环，按卡片布局位置移动并适配 Full/Reduced/Off。
- [x] 禁用卡片优先显示 `disabledReason`，与 Arc 状态语义一致。
- [x] 对齐 Arc 单一 Tab stop、Home/End 首尾可用项和网格/列表 Meta 布局。
- [ ] 真实窗口环边界、网格响应式、键盘和主题验收。

### 2026-11-03 CookieConsent 开发

- [x] 冻结 Arc CookieConsent Registry、CSS、Markdown、页面、动效令牌和许可快照；见 `tests/Baselines/References/2026-11-03-cookie-consent`。
- [x] 完成同意栏、偏好面板、重新打开按钮、类别开关、版本化本地存储、Gallery 和 4 项专项测试。
- [x] 补齐打开、关闭和提交后的明确按钮焦点归还路径。
- [x] `Position` 与 `ShowReopenButton` 变更在模板已加载后即时刷新位置和重新打开按钮可见性。
- [x] Position 对齐 Arc face 语义：bar/panel 按位置对齐，居中和左侧的 reopen chip 靠左，右侧位置靠右。
- [ ] 真实窗口的焦点归还、明暗主题、逐帧形变动效和多 DPI 验收。

### 2026-11-03 Reactions 来源冻结

- [x] 冻结 Arc Reactions Registry、CSS、Markdown、页面、动效令牌和许可快照；见 `tests/Baselines/References/2026-11-03-reactions`。
- [x] 实现 reaction pills、计数、picker、键盘语义、Popover 碰撞、Gallery 和专项测试。

### 2026-11-03 Reactions 开发

- [x] 完成 WPF Reactions、reaction pills、计数切换、Popup picker、主题、Gallery 和 4 项专项测试。
- [x] 补齐 reaction icon/label 渲染、Popup 上下碰撞定位和关闭焦点归还。
- [x] 补齐 picker 选中态、悬停态和按下反馈，并保留键盘可操作路径。
- [x] Reaction 数据改为可观察对象，计数变更保留原位并加入方向性滚动/回弹反馈。
- [x] picker 使用共享高亮层跟随鼠标悬停和键盘焦点，并按 Arc spring 参数移动。
- [ ] Arc 计数滚动、图标 kick、共享高亮弹簧、完整 Popover 动效和真实窗口验收。

### 2026-11-03 SwipeActions 开发

- [x] 冻结 Arc SwipeActions Registry、CSS、Markdown、页面、动效令牌和许可快照；见 `tests/Baselines/References/2026-11-03-swipe-actions`。
- [x] 完成 WPF SwipeActions/SwipeActionsRow、鼠标和触摸拖拽、full swipe、More 操作、主题、Gallery 和 2 项专项测试。
- [x] 补齐鼠标/触摸捕获、边界橡皮筋、释放速度采样和 Full/Reduced/Off 回弹过渡。
- [x] More 按钮改为包含 leading/trailing 全部动作的原生 ContextMenu。
- [x] 动作层改为真实 WPF 按钮，补齐 tone、图标/文案、点击命中、Escape 和外部点击关闭。
- [x] 动作层随揭示距离连续伸展，外侧动作在长拖拽时承担覆盖区域。
- [x] 菜单视觉主题：已改为自绘 Popup。
- [ ] 真实触摸、多 DPI 和窗口验收。

### 2026-11-03 五项复刻审计

上述五项的“完成”判断已被 [复刻审计](verification/2026-11-03-replica-batch-audit.md) 取代：并排比较发现它们远未达到复刻要求，已按 Arc 源码重写。离屏对比和 56 项专项测试通过；真实窗口、拖拽和触摸仍待验收。

## 2026-11-02 新增控件批次

ThemeSwitch、ToggleGroup、ActionSwap、UndoToast、ExpandableText 已完成来源冻结、WPF 实现、主题、Gallery 和专项测试代码；精确验证见 [验证记录](verification/2026-11-02-theme-toggle-actions.md)。已对照线上页面做像素复刻，见 [像素记录](verification/2026-11-02-theme-toggle-actions-pixel.md)。UndoToast 是 Arc Pro，Registry 不可得，依据公开文档和页面显示独立实现。真实窗口、DPI、读屏和逐帧动效仍待验收。


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

`SortableDataTable`、`ReorderableList`、`FilterToolbar`、`TreeTable`、`CodeBlock`、`JSONViewer`。CodeBlock、JSONViewer、SortableDataTable、TreeTable 已完成本批次实现；后续若扩展虚拟化、异步懒加载或 DataGrid 编辑语义，应另立范围，不把复杂契约偷偷扩展进基础控件。2026-11-03 用户指定的 Arc Data grid 已按独立范围实现为 `ArcDataGrid`（待验收，见 [规格](components/ArcDataGrid.md)）；`SortableDataTable` 契约未变。

### 2026-11-03 第八批复刻

- [x] ScrubInput 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-03-batch8-replication.md)
- [x] ElasticSlider 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-03-batch8-replication.md)
- [x] RulerSlider 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-03-batch8-replication.md)
- [x] Sparkline 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-03-batch8-replication.md)
- [x] Gauge 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-03-batch8-replication.md)
- [x] ShareAccess 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-03-batch9-replication.md)
- [x] MetricTicker 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-03-batch9-replication.md)
- [x] RealtimeStream 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-03-batch9-replication.md)
- [x] TocProgress 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-03-batch9-replication.md)
- [x] WalletStack 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-03-batch9-replication.md)
- [x] ActivityHeatmap 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-04-batch10-replication.md)
- [x] BarChart 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-04-batch10-replication.md)
- [x] LineChart 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-04-batch10-replication.md)
- [x] DonutChart 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-04-batch10-replication.md)
- [x] FunnelChart 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-04-batch10-replication.md)
- [x] PromptInput 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-04-batch11-replication.md)
- [x] StreamingMessage 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-04-batch11-replication.md)
- [x] ToolCallCard 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-04-batch11-replication.md)
- [x] ReasoningBlock 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-04-batch11-replication.md)
- [x] CitationList / SourcePopover 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-04-batch11-replication.md)
- [x] ModelPicker 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-04-batch11-replication.md)

### 2026-11-03 第七批复刻

- [x] `TextMorph`、`TextReveal`、`TextShimmer`、`IconMorph`、`InViewTitle` 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-03-batch7-replication.md)。

### 2026-11-03 第六批复刻

- [x] `FloatingButtonGroup`、`ExpandingButtonGroup`、`CardStack`、`LiquidTabBar`、`CoverFlow` 完成实现、Gallery 和专项测试；状态为待验收，LiquidTabBar、CoverFlow 属 Arc Pro，开源前需确认许可，见[验证记录](verification/2026-11-03-batch6-replication.md)。

### 2026-11-03 第五批复刻

- [x] `ConfirmPopover`、`DropdownMenu`、`BillingToggle`、`ChatThread`、`CommentThread`、`ImageCompare` 完成实现、Gallery 和专项测试；状态为待验收，见[验证记录](verification/2026-11-03-batch5-replication.md)。

### 2026-11-03 第四批复刻

- [x] 用户明确要求后，`RichTextEditor`、`SignaturePad` 移出暂缓，并与 `CommandMenu`、`OnboardingChecklist` 一起完成实现、Gallery 和专项测试；状态待验收，见 [验证记录](verification/2026-11-03-batch4-replication.md)。

### 暂缓

图表 14 项、Advanced charts、Carousel/Card stack、Text effects、Media/Activity 复杂组件以及所有 Pro 条目。它们分别涉及编辑器模型、绘制/命中测试、数据可视化、手势或付费来源边界，应单独立项，不作为下一批连续移植。

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
- [x] HoldToConfirmTests（7）/InlineEditTests（6）及 Gallery/规格资料已纳入统一收口；真实窗口验收仍按验证记录保留边界。

## ExpandingSearch 开发（2026-10-31）

- [x] 实现 collapsed/expanded 搜索触发器、输入、清除、Escape、Enter、焦点归还和宽度动效。
- [x] 接入主题、Gallery 和 3 项专项测试。
- [x] Arc ExpandingSearch 的实现、Gallery、规格和专项资料已收口；来源与真实窗口边界按验证记录保留。

## ExpandingSearch 截图对照修正（2026-10-31）

- [x] 展开后补齐 Recent 信息面板、条目图标、标题和元数据，面板与输入框同步展开宽度。
- [x] Recent 信息面板、来源对照修正和专项资料已收口；真实窗口边界按验证记录保留。

## DateRangePicker 开发（2026-10-31）

- [x] 基于 ArcWpf Calendar/DatePicker 实现双 Calendar Popup、范围选择、边界、BlackoutDates、Clear、Gallery 和 3 项专项测试。
- [x] DateRangePicker 实现、范围选择、Gallery、规格和专项资料已收口；Popup/多 DPI/主题边界按验证记录保留。

## TimePicker / ScrollArea 开发（2026-10-31）

- [x] 实现 TimePicker 时间列表 Popup、MinuteIncrement、SelectedTime 和 ScrollArea 原生 ScrollViewer 宿主。
- [x] 接入主题、Gallery 页面和专项测试；Controls 构建通过，专项 6/6 通过。
- [x] TimePicker/ScrollArea 实现、主题、Gallery 和专项资料已收口；Popup/滚动/多 DPI 边界按验证记录保留。

## ExpandingSearch / DateRangePicker / TimePicker / ScrollArea 源码复刻（2026-10-30）

- [x] 冻结四控件 Registry、CSS、Markdown、页面、动效令牌和许可；补参考卡。
- [x] 按源码重做模板、行为、动效与 Gallery 主示例，重写专项测试；Controls/Gallery/Tests 构建 0 错误。
- [x] 四控件源码复刻、16 项专项资料和构建记录已收口；真实窗口边界见 [验证记录](verification/2026-10-30-arc-source-rebuild.md)。

## EmptyState 开发（2026-10-31）

- [x] 建立明确标记的 WPF fallback EmptyState，包含图标、标题、说明、主操作和 UIA。
- [x] 接入主题、Gallery 和专项测试；Controls 构建通过，EmptyState 专项 3/3 通过。
- [ ] Arc EmptyState Registry/CSS 来源恢复后网页参数、图标、间距和动效复核；真实窗口验收待补。

## Skeleton 开发（2026-10-31）

- [x] 建立明确标记的 WPF fallback Skeleton，支持 Rectangle/Text/Circle、Fill、CornerRadius 和 Full/Reduced/Off shimmer。
- [x] 接入主题、Gallery 和专项测试；Controls 构建通过，Skeleton 专项 3/3 通过。
- [ ] Arc Skeleton Registry/CSS 来源恢复后网页参数、shimmer 和真实窗口验收。

## FilterToolbar 开发（2026-10-31）

- [x] 建立明确标记的 WPF fallback FilterToolbar，包含搜索、筛选胶囊、活动状态和 Clear all。
- [x] 接入主题、Gallery 和专项测试；Controls 构建通过，FilterToolbar 专项 3/3 通过。
- [ ] Arc FilterToolbar Registry/CSS 来源恢复后网页参数、响应式布局和真实窗口验收。

## FilterToolbar 开发（2026-10-31）

- [x] 建立明确标记的 WPF fallback FilterToolbar，包含搜索、筛选胶囊、活动状态和 Clear all。
- [x] 接入主题、Gallery 和专项测试；Controls 构建通过，FilterToolbar 专项 3/3 通过。
- [ ] Arc FilterToolbar Registry/CSS 来源恢复后网页参数、响应式布局和真实窗口验收。

## ReorderableList 开发（2026-10-31）

- [x] 建立明确标记的 WPF fallback ReorderableList，支持鼠标拖拽、方向键/Home/End、IList 重排和 ItemMoved。
- [x] 接入主题、Gallery 和专项测试；Controls 构建通过，ReorderableList 专项 3/3 通过。
- [ ] Arc ReorderableList Registry/CSS 来源恢复后拖拽把手、占位动画、真实命中、键盘和主题验收。

## EmptyState / Skeleton 源码复刻（2026-10-31）

- [x] 冻结两控件 Registry、CSS、Markdown、页面、动效令牌和许可；重写参考卡与规格。
- [x] 按源码重做模板、行为、动效与 Gallery 主示例，重写专项测试；构建 0 错误。
- [x] 两控件源码复刻、6 项专项资料和构建记录已收口；真实窗口边界见 [验证记录](verification/2026-10-31-empty-state-skeleton-rebuild.md)。

## FilterToolbar / ReorderableList 源码复刻（2026-10-31）

- [x] 冻结两控件 Registry、CSS、Markdown、页面、动效令牌和许可；重写参考卡与规格。
- [x] 按源码重做（含可单独使用的 FilterMenu）、Gallery 主示例与专项测试；构建 0 错误。
- [x] 两控件源码复刻、6 项专项资料和构建记录已收口；真实窗口边界见 [验证记录](verification/2026-10-31-filter-reorder-rebuild.md)。

## 输入类 8 控件源码复刻（2026-10-31）

PasswordStrength、MoneyInput、PhoneInput、TagInput、ShortcutRecorder、ChipGroup、Rating、ColorPicker 此前为网络受限时的 WPF fallback。

- [x] 冻结 8 个 Registry 源码、CSS、页面 demo、动效令牌和许可（含 ColorPicker 依赖的 text-morph）；重写参考卡与规格。
- [x] 按源码重做控件、Gallery 主示例（源 demo 内容）与专项测试；Controls/Gallery/Tests 构建 0 错误。
- [x] 八控件源码复刻、专项资料和构建记录已收口；真实窗口边界见 [验证记录](verification/2026-10-31-input-batch-rebuild.md)。

## BottomSheet 开发（2026-11-01）

- [x] 基于 Drawer/DrawerHost 实现 BottomSheet，复用遮罩、Escape、外部点击、焦点归还和 Bottom 动效。
- [x] 按冻结 Registry/CSS 接入默认 45%/92% detent、抓手键盘、拖拽速度投影、橡皮筋、动态遮罩、内容滚动、主题、Gallery 和专项测试；BottomSheet/Drawer 精确专项 16/16 通过。
- [ ] safe-area、逐帧节奏、真实鼠标/触摸、多 DPI 和 Gallery 实际窗口观察。

## HoverCard 开发（2026-11-01）

- [x] 基于 Tooltip/Popover 生命周期实现 HoverCard，支持 hover 延迟打开/关闭、Escape 和四向 Popup。
- [x] 接入主题、Gallery 和专项测试；Controls 构建通过，HoverCard 专项 3/3 通过。
- [ ] Arc HoverCard Registry/CSS 来源恢复后延迟、碰撞、焦点、真实窗口和主题验收。

## ResizablePanels 开发（2026-11-01）

- [x] 建立明确标记的 WPF fallback ResizablePanels，复用 GridSplitter，支持左右内容、比例、最小宽度和键盘/鼠标调整。
- [x] 接入主题、Gallery 和专项测试；Controls 构建通过，ResizablePanels 专项 3/3 通过。
- [ ] Arc ResizablePanels Registry/CSS 来源恢复后拖拽把手、边界回弹、比例持久化和真实窗口验收。

## Card / MetricCard 开发（2026-11-01）

- [x] 建立明确标记的 WPF fallback Card/MetricCard，支持标题、说明、内容、底部和数值/趋势展示。
- [x] 接入主题、Gallery 和专项测试；Controls 构建通过，Card 专项筛选通过。
- [ ] Arc Card/MetricCard Registry/CSS 来源恢复后圆角、阴影、数值排版和真实窗口验收。

## Stepper 开发（2026-11-01）

- [x] 建立明确标记的 WPF fallback Stepper，支持步骤列表、CurrentIndex、Next/Previous 和 UIA 状态。
- [x] 接入主题、Gallery 和专项测试；Controls 构建通过，Stepper 专项筛选通过。
- [ ] Arc Stepper Registry/CSS 来源恢复后连接线、选中动效、键盘和真实窗口验收。

## UsageMeter 开发（2026-11-01）

- [x] 建立明确标记的 WPF fallback UsageMeter，支持 Used/Limit、Percentage、WarningThreshold、Unit 和 UIA。
- [x] 接入主题、Gallery 和专项测试；Controls 构建通过，UsageMeter 专项 3/3 通过。
- [ ] Arc UsageMeter Registry/CSS 来源恢复后数字排版、阈值颜色、动效和真实窗口验收。
