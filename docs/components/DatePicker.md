# DatePicker 组件规格

## 身份与状态

- 组件名：DatePicker；用途：选择单个日期并保留原生日期输入语义；里程碑 M1。
- 状态：Calendar共享模板已接入，待实际窗口专项与视觉验收。
- 来源：Arc UI Date picker，采集批次 `2026-10-26-date-picker`，见[参考卡](../references/components/DatePicker.md)和快照目录。

## 范围与依据

首期必做：Arc 风格标签/说明、SelectedDate 双向绑定、DisplayDateStart/End、BlackoutDates、SelectedDateFormat、文化格式、原生日历弹出层、Today/清除入口的 API 预留、禁用/验证/键盘/焦点和主题模板、Gallery、专项测试。明确暂缓：日期分段滚动、spring 动效、JS 函数型 disabledDates、响应式 CSS 精确复制、实际窗口验收。

| 结论或参数 | 权威来源 | 证据等级 | 验证状态 |
|---|---|---|---|
| `label`、`description`、`placeholder`、`minDate`、`maxDate`、`locale`、`format`、`showToday` | Arc Markdown API | 文档确认 | 已固定 |
| Arc触发器键盘与WPF编辑行为分开：WPF输入框Enter提交，Alt+Down打开；日历Escape关闭、方向键/Home/End/PageUp/PageDown导航 | Arc Markdown；WPF DatePicker机制 | 文档确认 + WPF适配 | Enter/Popup程序化通过，真实键盘待验收 |
| 原生 `SelectedDate`、`DisplayDateStart/End`、`BlackoutDates` 和验证 | WPF API | 文档确认 | 程序化待测 |
| 44 DIP、18 DIP 圆角、320 DIP popup 上限 | Arc CSS token 作用层 | 暂定设计/WPF适配 | 视觉待验收 |

## WPF公开契约

继承 `System.Windows.Controls.DatePicker`。新增 `Label`、`Description`、`Placeholder`、`ShowToday` 依赖属性；SelectedDate 等原生属性保持 WPF 绑定语义。ShowToday 显示选择今天按钮；Today/Clear 使用 SetCurrentValue。标准部件为 `PART_TextBox`、`PART_Button`、`PART_Popup`；自有 `PART_CalendarHost` 承载基类创建的 Calendar（不是额外实例）。不接管 DataContext。输入框 Enter 提交，Alt+Down 打开；按钮沿用原生键盘激活。

## 状态与交互

| 状态/触发 | 外观 | 操作结果 | 焦点/键盘 | 数据与事件 |
|---|---|---|---|---|
| 未选中 | placeholder 显示 | 点击右侧按钮打开日历 | 输入框Enter提交、Alt+Down打开 | SelectedDate 为 null |
| 已选中 | 文化格式日期 | 打开当前日期所在月 | 日历导航遵循 WPF | SelectedDateChanged |
| 上下限/黑名单 | 不可选日期禁用 | 选择被拒绝 | 方向导航跳过/停留按 WPF | DisplayDateStart/End/BlackoutDates |
| Escape/失焦 | Popup 关闭 | 返回触发器焦点（WPF） | Escape 关闭 | 不改绑定 |
| Disabled/Validation | 令牌降低不透明度/错误边框由 WPF | 不可操作 | 原生 IsEnabled/Validation | 保留验证事件 |

## 视觉与动效

使用 ArcWpf.Brush.Surface/Foreground/BorderStrong/TextMuted、Body 字体；字段 44 DIP、圆角 18 DIP，标签和说明各 8 DIP 间隔。首版不添加滚动数字或 spring；Popup 使用 WPF 原生关闭和焦点行为，后续可在不改公开契约的情况下补充可取消动效。

## 验收场景

1. 绑定 SelectedDate 为 null/具体日期并更新，Text 使用当前文化格式。
2. DisplayDateStart/End 和 BlackoutDates 阻止越界/黑名单日期。
3. 模板包含标准部件，Gallery 展示标签、说明、已选、禁用和绑定场景。
4. 验证保留 WPF Validation.Error；AutomationProperties.Name 由 Label 提供。
5. 程序化检查模板测量、键盘路径可构造；实际窗口焦点、Popup、IME、DPI、读屏待验收。

## 实施与证据

2026-10-26 新增独立 [Calendar](Calendar.md) 后，DatePicker.CalendarStyle默认复用 `ArcWpf.Calendar.Style`。内部原生 Calendar 保留，设置 Stretch 消除框架初始化Left造成的紧凑宽度；Popup宽320、Padding16、内部日历实测286。Today位于CalendarItem标题栏，通过共享命令执行；PART_Clear位于分隔底栏。旧PART_Today不再属于DatePicker模板。当前12项Calendar/DatePicker专项与完整回归结果见[最新记录](../verification/2026-10-26-calendar.md)，下列221/221为此前历史结果。

2026-10-26 用户指出不弹出后继续修复，保持日历网格；此前 4 项测试不足以证明 Popup 和窗口输入，旧完成结论不代表视觉/交互验收。见[修复记录](../verification/2026-10-26-datepicker-fix.md)。

代码：`src/ArcWpf.Controls/DatePicker/DatePicker.cs`、`Themes/DatePickerTheme.xaml`；Gallery：`samples/ArcWpf.Gallery/Pages/DatePickerPage.*`；测试：`tests/ArcWpf.Tests/DatePickerTests.cs`。2026-10-26 使用 .NET SDK 10.0.402 执行 `dotnet build ArcWpf.slnx --no-restore`，0 错误；DatePicker 专项 4/4 通过；全量 ArcWpf.Tests 221/221 通过。未执行实际窗口、IME、DPI、读屏和性能验收。

## 完整度矩阵

| 交付项 | 范围 | 实现状态 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| API与绑定 | 必做 | 已实现 | DatePickerTests 7/7 | 实际窗口待验收 |
| 外形与对齐 | 必做 | 已实现 | Calendar布局测试、明暗离屏渲染 | 像素/字体待用户验收 |
| 内容与状态 | 必做 | 已实现 | CalendarTests 5/5 + DatePickerTests 7/7 | Today辅助技术待补 |
| 动效 | 暂缓 | 暂缓 | 规格记录 | rolling/spring 后续 |
| 输入/自动化/主题 | 必做 | 原生语义保留、共享模板已实现 | Enter提交、非法输入、主题切换程序化测试 | IME/读屏/实际键盘待验收 |
| 主示例/扩展示例 | 必做 | 已实现 | Gallery 页面 | 实际窗口待验收 |
