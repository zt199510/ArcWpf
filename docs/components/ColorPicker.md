# ColorPicker 组件规格

状态：2026-10-31 按冻结 Arc 源码重建（取代此前网络受限时的 WPF fallback）。参考卡见 [references/components/ColorPicker.md](../references/components/ColorPicker.md)。

## 范围矩阵

| 项 | 状态 |
|---|---|
| 外形与尺寸（源 CSS） | 已实现 |
| 内部布局与状态 | 已实现 |
| 动效（Arc motion tokens，Full/Reduced/Off） | 已实现，弹簧为近似 |
| Gallery 主示例（源 demo 内容） | 已实现 |
| 构建（Linux，EnableWindowsTargeting） | 通过 |
| 单元测试 | 已编写，未运行/待验证（需 Windows） |
| 真实窗口视觉与交互 | 未运行/待验证（需 Windows） |

## 与源的差异

- WPF 无 EyeDropper API，取色按钮不显示（源同样在不支持时隐藏）。
- 格式切换的 TextMorph 字符变形以淡入+模糊近似。
- 浮层阴影以单层 DropShadowEffect 近似双层 drop-shadow。
