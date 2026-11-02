# ArcWpf 控件统一收口记录（2026-11-02）

用户确认当前纳入范围的控件已完成收口。本记录作为当前状态入口，配合各组件规格、参考卡和专项验证记录使用。

收口范围覆盖：

- 基础与导航：TreeView、Breadcrumb、Menu、ContextMenu、UserMenu、Pagination、List、Search。
- 表单与选择：NumberInput、Progress、Calendar、DatePicker、DateRangePicker、TimePicker、PasswordStrength、MoneyInput、PhoneInput、TagInput、ShortcutRecorder、ChipGroup、Rating、ColorPicker。
- 交互与编辑：ActionButton、CopyButton、ButtonGroup、HoldToConfirm、InlineEdit、ExpandingSearch。
- 布局与展示：ScrollArea、EmptyState、Skeleton、FilterToolbar、ReorderableList、BottomSheet、HoverCard、ResizablePanels、Card、MetricCard、Stepper、UsageMeter。

每项收口包含代码/API、模板与主题、Gallery 主示例、组件规格或参考卡、专项测试和验证记录。最近批次的可复核证据包括：BottomSheet/Drawer 16/16、五控件相关回归 106/106、ArcWpf 解决方案构建 0 错误；更早批次的结果以对应日期验证记录为准。

“收口”表示实现和项目交付资料完成，不自动替代平台专项结论。实际窗口视觉、物理鼠标/触摸、IME、读屏、100/150/200% DPI、性能和逐帧观感在各组件记录中继续按“已验证/待验收/平台差异”区分。

后续新增控件应建立新的来源冻结、规格、Gallery、专项测试和验证记录，不回写历史批次的过程状态。
