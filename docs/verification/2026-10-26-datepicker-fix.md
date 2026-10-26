# DatePicker 弹出与输入修复

用户反馈：DatePicker 没有弹出；截图可见双重占位和原生编辑框边框。用户随后明确保持日历网格，不改为 Date reel。

## 修复

- 核对 dotnet/wpf v10.0.0 的 DatePicker.OnApplyTemplate：基类将 PART_Popup.Child 替换为内部 Calendar。旧模板另建的 PART_Calendar 和页脚脱离实际显示树，旧测试只检查命名部件，无法证明功能。
- 在基类初始化后将同一个原生 Calendar 放回 PART_CalendarHost。BlackoutDates、键盘、选中事件与解析继续使用基类；Popup.IsOpen 交给基类管理，定位锚点改为字段 Surface。
- DatePickerTextBox 使用独立编辑宿主模板，取消原生提示和外层占位重叠；占位依据编辑框 Text，而非 SelectedDate，避免编辑中遮挡文字。
- Today/Clear 使用 SetCurrentValue 保留绑定；Today 对越界和 BlackoutDates 拒绝操作。
- Gallery 修复不存在的 StartDate 绑定源，增加实际双向绑定回显和 ShowToday。输入框按 Enter 提交，Alt+Down 为原生打开方式。

## 验证边界

最终执行 `dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --filter FullyQualifiedName~DatePickerTests`：依赖项目构建成功，7/7 通过（含已显示窗口中的打开、关闭后再打开与 Enter 提交）。未重跑全量；上次 221 项结果不代表此次修改后的全量结果。

新增已显示 Window 上的下拉按钮事件/Popup.IsOpen 集成检查、原生日历与 BlackoutDates 实例一致性、绑定保留、禁用今天、en-GB 解析和 DateValidationError 测试。它们属于 STA/Dispatcher 程序化集成，不是物理鼠标输入验收。

实际 Gallery 已启动并捕获窗口，但自动化依次返回 coordinate input geometry is unavailable、foreground window did not report a process id，重取窗口时句柄已消失。因此本次未完成实际鼠标、Alt+Down、Escape、跨 DPI 和读屏验收，也不认定完整视觉还原。日历内部目前保留系统 Calendar 外观，主题与动效仍有差异。
