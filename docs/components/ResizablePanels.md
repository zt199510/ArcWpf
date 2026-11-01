# ResizablePanels 组件规格

状态：Arc Registry 已冻结；当前 WPF 双栏实现只覆盖基础拖拽、键盘调整与边界视觉回弹，来源的多栏、可折叠面板、恢复标签和布局重置尚未实现。

`ResizablePanels` 使用 WPF GridSplitter 提供左右内容、最小宽度、分栏比例属性和键盘/鼠标调整语义。

2026-11-01 修正：`SplitRatio` 现在驱动实际左右列宽；拖拽完成写回绑定，最小宽度限制内的越界拖动只偏移分隔条视觉并由 Full 动效回弹，Reduced/Off 不播放回弹。窗口测量与边界时钟测试见验证记录。来源弹性公式及折叠行为仍待移植。

后续复验固定 GridSplitter 为 `PreviousAndNext` 列调整；Left/Right 每次移动 10 DIP，Home/End 到两侧边界，并将键盘结果写回 SplitRatio。键盘步长 10 DIP 为当前 WPF 适配项，非 Arc 已确认值。
