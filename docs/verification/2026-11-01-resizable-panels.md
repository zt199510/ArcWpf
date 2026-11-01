# ResizablePanels 验证记录（2026-11-01）

来源 Registry 当前网络访问被拒绝，未伪造网页参数。

待运行：Controls/Gallery 构建、ResizablePanels 专项、拖拽、键盘、边界和实际窗口主题。

2026-11-01 补充：Windows STA 显示窗口中实测 600 DIP 容器的 0.3→0.7 比例变化和最小宽度；边界 DragDelta 启动回弹时钟，不改变最小列宽。ResizablePanels 专项 5/5 通过。真实物理鼠标拖拽与 Arc 来源节奏仍待验收。

后续窗口键盘回归：对聚焦的分隔条按 Right 后，实际左列宽和双向绑定 SplitRatio 同步增长；固定 `ResizeBehavior=PreviousAndNext`。专项 6/6 通过。真实物理鼠标、触摸和 DPI 仍待验收。
