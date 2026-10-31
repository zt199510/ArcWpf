# DateRangePicker 组件规格

状态：已按 2026-10-30 冻结源码重做，构建通过；专项测试与实际窗口 **未运行/待验证**（Linux 环境）。事实见 [参考卡](../references/components/DateRangePicker.md)。

## API

`DateRangePicker : Control`：`StartDate`/`EndDate`（双向，仅 Apply 提交）、`Label`（`Date range`）、`Placeholder`（`Select dates`）、`MinimumDate`/`MaximumDate`、`FirstDayOfWeek`（Sunday/Monday）、`Months`（Auto/One/Two）、`Boundary`（FrameworkElement）、`IsOpen`、`Presets`（`DateRangePreset`，默认 8 项）、`RangeChanged`（`DateRangeChangedEventArgs`）。旧版 `BlackoutDates` 与 Clear 已移除（原版无此功能）。

## 范围矩阵

| 项 | 状态 |
|---|---|
| trigger（日历图标、按词滚动的值、chevron、hover/阴影） | 已实现 |
| surface 从 trigger 长成 panel（GROW .48 / SHRINK .38 物理弹簧，圆角 18→26，boundary 内 clamp） | 已实现 |
| 预设 rail（滑动高亮、active 规则）、compact 横向预设 | 已实现 |
| 双月/单月 auto 切换（712 阈值）、月份方向滑动 | 已实现 |
| anchor + hover 预览、范围条 STRETCH、端点 GLIDE、today 点、禁用日 | 已实现 |
| footer 摘要 + 天数、Cancel/Apply；Escape/外部点击/焦点离开关闭不提交 | 已实现 |
| 值 layoutId 从 trigger 飞到 footer | 已知差异：以 faceIn/quiet 交叉替代 |
| Gallery 主示例（stage 760、最近 7 天、maxDate 今天、note） | 已实现 |
| 实际窗口、键盘日期导航细节、明暗主题、多 DPI、Reduced/Off | 未运行/待验证 |
