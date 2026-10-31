# TimePicker 组件规格

状态：已按 2026-10-30 冻结源码重做，构建通过；专项测试与实际窗口 **未运行/待验证**（Linux 环境）。事实见 [参考卡](../references/components/TimePicker.md)。

## API

`TimePicker : Control`：`SelectedTime`（`TimeSpan?`，默认 09:00）、`Label`、`Description`、`Placeholder`（`Select a time`）、`MinuteStep`（1/5/10/15/30，默认 15，其他值抛异常）、`Format`（`TwelveHour` 默认 / `TwentyFourHour`）、`IsOpen`、`SelectedTimeChanged`。旧 `MinuteIncrement` 更名为 `MinuteStep`。

## 范围矩阵

| 项 | 状态 |
|---|---|
| 44 trigger（clock 图标、值、chevron 180° 旋转） | 已实现 |
| 值滚动（方向、blur、Reduced 交叉淡入） | 已实现 |
| 菜单 250 高、radius 26、选中点、active 高亮；打开时居中选中项 | 已实现 |
| 键盘 Enter/Space/Up/Down/Escape 与循环 | 已实现 |
| 菜单入场弹簧 / 退场 .12 | 已实现；外部点击关闭（Popup StaysOpen=False）无退场动画，已知差异 |
| Gallery 主示例 `Review time` 09:30 | 已实现；展示宽度 320 待核对 |
| 实际窗口、明暗主题、多 DPI、Reduced/Off | 未运行/待验证 |
