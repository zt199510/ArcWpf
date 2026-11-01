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
