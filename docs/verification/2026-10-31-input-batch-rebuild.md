# 输入类 8 控件源码复刻验证记录（2026-10-31）

范围：PasswordStrength、MoneyInput、PhoneInput、TagInput、ShortcutRecorder、ChipGroup、Rating、ColorPicker。按 2026-10-31 冻结的 Arc Registry 源码（`tests/Baselines/References/2026-10-31-<slug>/`）重建，取代此前网络受限时的 WPF fallback。API 有破坏性调整，见各组件规格。

环境：Linux（无 WPF 运行时），.NET SDK 10.0.402，`-p:EnableWindowsTargeting=true`。

## 实际结果

| 项 | 结果 |
|---|---|
| `dotnet build src/ArcWpf.Controls` | 通过，0 错误 0 警告 |
| `dotnet build samples/ArcWpf.Gallery` | 通过，0 错误 0 警告 |
| `dotnet build tests/ArcWpf.Tests` | 通过，0 错误（仅既有无关警告） |
| 纯逻辑抽查（MoneyMath / PhoneNumbers / ColorMath，临时控制台） | 通过；HSL 文本往返有 ±1 舍入差，与源一致，测试已排除 |

## 未运行/待验证

原因：Linux 环境无法启动 WPF 窗口或运行依赖 Dispatcher 的测试。

- 8 个控件的专项单元测试（已重写并编译通过）。
- 真实窗口外形、内部布局、状态与动效对照（Full/Reduced/Off），Light/Dark 主题。
- 键盘、IME、撤销（MoneyInput/PhoneInput 格式化）、读屏。
- Popup 定位与外部点击关闭（MoneyInput 货币菜单、PhoneInput 国家选择、ColorPicker 面板）。
- MoneyInput 透明 TextBox 与镜像字符列的像素对齐。

## 已知差异

见各组件规格“与源的差异”：Segoe UI 回退、无字距；弹簧为近似；PhoneInput 旗帜以 ISO 字母徽章代替；ColorPicker 无取色器、TextMorph 以淡入近似；TagInput 输入框固定宽度。

## 人工测试缺陷修复（2026-10-31）

用户人工测试反馈 5 项缺陷。逐项对照冻结源码与模板定位，结论为静态分析推定的根因（Linux 无法复现，证据等级：源码确认 + 代码审阅）。

| 控件 | 现象 | 根因 | 修复 |
|---|---|---|---|
| PasswordStrength | 输入框样式异常 | 模板以 `DynamicResource` 引用 PasswordInput 主题文件中的字符串键编辑器样式；主题字典的字符串键运行时不可解析，编辑器回落为 WPF 默认带边框的 PasswordBox/TextBox | 在 PasswordStrengthTheme 内定义 `ArcWpf.PasswordStrength.HiddenEditor/VisibleEditor` 并用 `StaticResource` |
| MoneyInput | 切换货币无效/不稳定 | 窗口级 `PreviewMouseDown` 外部点击检测未排除 Popup 内容；Popup 内的按下事件也经逻辑树路由到窗口，点选项时菜单先被关闭 | 新增 `Outside.IsWithin`（FieldKit），按事件源向上查找触发器与 Popup 内容；与源 `menuRef.contains(target)` 一致 |
| PhoneInput | 切换国家无效 | 同上，按下行时列表先收起，MouseUp 不再落在行上 | 同上，对照源 `rootRef.contains(target)` |
| ColorPicker | 选颜色时面板自动关闭 | 同上，面板内任何按下都被当成外部点击 | 同上，对照源 document `pointerdown` 外部判断 |
| TagInput | 输入标签卡顿/崩溃 | 光环 `TransformGroup` 写在模板中，模板 Freezable 被冻结，选中标签（如空输入时 Backspace）对其动画抛 `InvalidOperationException`；另外每次变更都把聚焦的输入框移出再加回面板 | 光环变换在 `OnApplyTemplate` 中由代码创建；Sync 只在输入框之前插入/移动标签，输入框不再离开面板，并对重复标签去重 |

### 实际结果

| 项 | 结果 |
|---|---|
| `dotnet build src/ArcWpf.Controls` | 通过，0 错误（2 条 Rating/ReorderableList 既有 CS0108 警告） |
| `dotnet build samples/ArcWpf.Gallery` | 通过，0 错误 |
| `dotnet build tests/ArcWpf.Tests` | 通过，0 错误 |

新增回归测试（已编译，未运行）：`PasswordStrengthTests.EditorsUseTheBareTemplate`、`TagInputTests.PickingATagMovesAnAnimatableRing`、`TagInputTests.AddingKeepsTheEditorInPlace`、`ColorPickerTests.PressInsidePopupContentIsNotOutside`。

### 未运行/待验证

原因同上：Linux 无 WPF 运行时。5 项均需在 Windows 上人工复测：PasswordStrength 输入框外观（Light/Dark）；MoneyInput 鼠标与键盘切换货币；PhoneInput 鼠标选择国家；ColorPicker 面板内拖动/点选后保持打开、外部点击关闭；TagInput 连续添加标签保持焦点、Backspace 选中与删除。

## MoneyInput 第二轮：下拉框打不开（2026-10-31）

用户复测：外部点击修复后仍无法打开货币菜单。第一轮只修了"菜单打开后点选项被提前关闭"，未检查打开路径本身，属误判范围。

根因：`OnApplyTemplate` 在子按钮 `PART_CurrencyButton` 应用自身模板之前调用 `Template.FindName("Surface"/"Code")`，返回 null。Surface 无背景导致按钮只有 chevron 细线可命中，货币代码文字为空。同类控件（ButtonGroup、Search、ExpandingSearch）均先调用 `ApplyTemplate()`。

修复：查找前调用 `_currencyButton.ApplyTemplate()`；模板中 Surface 默认 `Background="Transparent"` 作为兜底。新增 `MoneyInputTests.CurrencyButtonIsReadyAfterTemplate`（已编译，未运行）。三个项目构建 0 错误。Windows 人工复测：待验证。
