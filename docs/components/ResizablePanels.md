# ResizablePanels 组件规格

状态：Arc Registry 已冻结；WPF 已提供兼容的双栏属性模式和来源式多栏集合模式。拖拽、键盘、边界弹性、释放速度投影、折叠/恢复及重置已实现；面板宽度弹簧和完整 UIA 数值仍有差异。

`ResizablePanels` 使用 WPF GridSplitter 提供左右内容、最小宽度、分栏比例属性和键盘/鼠标调整语义。新增 `Panels : ObservableCollection<ResizablePanel>`；非空时进入多栏模式，旧 `LeftContent`/`RightContent` 与 `SplitRatio` 在集合清空后恢复。`ResizablePanel : ContentControl` 提供 Id、Label、DefaultSize、MinSize（默认 80 DIP）、MaxSize、Collapsible、DefaultCollapsed、双向 IsCollapsed。`Label` 命名组，`Shares` 返回百分比，`LayoutChanged` 在操作后发布份额。

2026-11-01 修正：`SplitRatio` 驱动实际左右列宽；拖拽完成写回绑定。当前越界位移使用来源 `overshoot * 44 * .55 / (44 + .55 * overshoot)` 公式，拖动中保持橡皮筋位置，释放后由 OwnedMotion 0.26s 近似弹簧回零；Reduced/Off 即时回零。最小宽度之和超出容器时按比例缩小两边边界，内容不会溢出。

`CanCollapseLeft/Right`、双向 `IsLeftCollapsed/IsRightCollapsed`、`DefaultSplitRatio` 与 `Reset()` 提供双栏折叠/恢复。越界拖动达到 `max(min * .5, 40)` 阈值后折叠；折叠列宽归零、内容不命中、分隔条显示恢复按钮。Left/Right 每次移动 16 DIP，Shift 为 64 DIP；Home/End 到两侧边界，继续越界可折叠，Enter 折叠/恢复，双击恢复默认比例。连续按键以目标 `SplitRatio` 计算，避免在下一帧布局前丢步。

多栏模式由每个面板的 DefaultSize 归一化为份额，在 Grid 中为每对相邻面板生成分隔条与恢复按钮；拖拽只更新相邻两栏，结束后发布 `Shares`。默认折叠面板恢复其配置份额；窗口宽度不足全部 MinSize 时按比例缩小下限，面板内容裁剪于本栏。动态分隔条复用双栏模板的 1 DIP 线与 6 DIP 命中区。释放时取最近 90ms 的拖拽样本，末次样本超过 50ms 则不投影；按来源 `velocity / 1000 * .99 / (1 - .99)` 仅决定折叠，不推动普通分栏位置。当前收放列宽立即更新；来源的收放宽度弹簧、触摸捕获实测和逐分隔条 UIA RangeValue 仍未实现或验收。

2026-11-01 布局对照：见[验证记录](../verification/2026-11-01-layout-alignment.md)。程序化检查与离屏渲染已有结果，真实窗口待复验。
