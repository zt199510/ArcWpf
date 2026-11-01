# MoneyInput 组件规格

状态：2026-10-31 按冻结 Arc 源码重建（取代此前网络受限时的 WPF fallback）。参考卡见 [references/components/MoneyInput.md](../references/components/MoneyInput.md)。

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

- 镜像字符列宽度依赖 Segoe UI 数字等宽；TextBox 与镜像对齐依赖 -2 内容偏移，需真实窗口确认。
- 字符列上下羽化以 OpacityMask 近似 CSS mask。
- 货币名称与符号取自内置 en-US 表（无 Intl.NumberFormat）。
- 菜单在窗口外点击时随窗口失活关闭。
