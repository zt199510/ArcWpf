# 剩余控件批次收口验证（2026-10-27）

范围：TreeView、Breadcrumb、Menu、NumberInput、Progress、Pagination、Calendar/DatePicker、List、Search。

## 实现与同步

- 代码已存在并完成本批次收口；Gallery 页面已接入对应主示例和边界状态。
- TreeView 规格从“待实现”更新为“实现完成，程序化验证通过，实际窗口待验收”；其余规格状态同步为实现/程序化通过。
- 修正共享 `OwnedMotion`：动画完成时提交最终基值后清除时钟，避免颜色、位置在完成帧后残留中间值。
- Search 清除退场与颜色补间增加生命周期兜底收口，卸载、隐藏和快速反向不会留下可见按钮或颜色动画。

## 程序化验证

命令：`dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --no-restore --filter "FullyQualifiedName~TreeViewTests|FullyQualifiedName~BreadcrumbTests|FullyQualifiedName~MenuTests|FullyQualifiedName~NumberInputTests|FullyQualifiedName~ProgressTests|FullyQualifiedName~PaginationTests|FullyQualifiedName~CalendarTests|FullyQualifiedName~DatePickerTests|FullyQualifiedName~ListTests|FullyQualifiedName~SearchTests"`。

结果：构建成功；上述专项最终 49/49 通过（Search 7/7，其他控件专项合计42项）。保留既有编译器和 MSTest 分析器警告。

## 实际窗口与未验收

本批次未新增真实窗口截图、读屏、IME、100/150/200% DPI 或性能证据；各控件仍标记“实际窗口待验收”。滚动型 TreeView/List、Popup 型 Menu/DatePicker、输入型 NumberInput/Search 的人工命中与焦点步骤需在 Gallery 最终构建中复验。Full/Reduced/Off 已由专项程序化路径覆盖，实际动效观感仍待用户验收。

## 全量回归边界

未将全量测试失败归因于本批次：当前全量仍包含 Avatar 网络缓存、AvatarGroup 浮点布局、Badge/Calendar/ComboBox 历史动效以及 Gallery 导航计数等既有失败；详情见本次命令输出。该记录只对上述 10 组专项结果负责。

## 用户验收

2026-10-28 用户确认本批次控件已完成手动验收；此前列出的实际窗口待验收项更新为用户手动验收通过。IME、读屏、多 DPI 和性能仍按用户未明确覆盖的范围保留为未测。
