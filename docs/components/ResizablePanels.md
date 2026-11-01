# ResizablePanels 组件规格

状态：Arc Registry 已冻结；当前 WPF 双栏实现覆盖拖拽、键盘调整、边界弹性、折叠/恢复和布局重置；来源的多栏集合与速度投影折叠仍未实现。

`ResizablePanels` 使用 WPF GridSplitter 提供左右内容、最小宽度、分栏比例属性和键盘/鼠标调整语义。

2026-11-01 修正：`SplitRatio` 驱动实际左右列宽；拖拽完成写回绑定。当前越界位移使用来源 `overshoot * 44 * .55 / (44 + .55 * overshoot)` 公式，拖动中保持橡皮筋位置，释放后由 OwnedMotion 0.26s 近似弹簧回零；Reduced/Off 即时回零。最小宽度之和超出容器时按比例缩小两边边界，内容不会溢出。

`CanCollapseLeft/Right`、双向 `IsLeftCollapsed/IsRightCollapsed`、`DefaultSplitRatio` 与 `Reset()` 提供双栏折叠/恢复。越界拖动达到 `max(min * .5, 40)` 阈值后折叠；折叠列宽归零、内容不命中、分隔条显示恢复按钮。Left/Right 每次移动 16 DIP，Shift 为 64 DIP；Home/End 到两侧边界，继续越界可折叠，Enter 折叠/恢复，双击恢复默认比例。连续按键以目标 `SplitRatio` 计算，避免在下一帧布局前丢步。当前收放列宽立即更新，未实现来源面板宽度弹簧和速度投影；多栏的 `ResizablePanel` 集合、逐分隔条 UIA 值与恢复动画仍待实现。
