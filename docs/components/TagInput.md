# TagInput 组件规格

状态：2026-10-31 按冻结 Arc 源码重建（取代此前网络受限时的 WPF fallback）。参考卡见 [references/components/TagInput.md](../references/components/TagInput.md)。

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

- WrapPanel 无 flex-grow，输入框固定 100 宽。
- 选中环位于胶囊之上（半透明）。
