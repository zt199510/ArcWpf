# ScrollArea 组件规格

状态：已按 2026-10-30 冻结源码重做，构建通过；专项测试与实际窗口 **未运行/待验证**（Linux 环境）。事实见 [参考卡](../references/components/ScrollArea.md)。

## API

`ScrollArea : ContentControl`：`Orientation`（Vertical/Horizontal/Both）、`Fade`（28）、`Scrollbars`（Auto/Always）、`HideDelay`（900ms）、`WheelToHorizontal`、`Label`（视口 AutomationProperties.Name）、`CanContentScroll`（默认 false），只读 `Edges`（`ScrollAreaEdges`）与 `EdgesChanged` 路由事件，`Viewport`（内部 ScrollViewer）。

## 范围矩阵

| 项 | 状态 |
|---|---|
| 原生 ScrollViewer + 自绘 11 DIP 轨道、5→7 pill、最小 thumb 28 | 已实现 |
| 边缘渐隐 OpacityMask（随边缘状态出现/消失） | 已实现 |
| Auto 显隐（滚动/悬停/拖动，.16 入 / .48 出）、Always | 已实现 |
| thumb 拖动、轨道点击 90% 翻页（smooth 弹簧，非 Full 时立即） | 已实现 |
| Horizontal 下滚轮转横移，到边缘交给父级 | 已实现 |
| CSS scroll-snap | 已知差异：未实现；滚轮步长为 WPF 默认 48 DIP/格 |
| Gallery 主示例 Activity / Rooms | 已实现；头像、照片以占位色块替代 |
| 实际窗口、触控板、明暗主题、多 DPI | 未运行/待验证 |
