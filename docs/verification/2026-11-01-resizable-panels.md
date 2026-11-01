# ResizablePanels 验证记录（2026-11-01）

来源：`tests/Baselines/References/2026-11-01-resizable-panels/manifest.json`，已冻结 Arc Registry、CSS、Markdown、页面与动效令牌，采集日期 2026-11-01。

待运行：Controls/Gallery 构建、ResizablePanels 专项、拖拽、键盘、边界和实际窗口主题。

2026-11-01 补充：Windows STA 显示窗口中实测 600 DIP 容器的 0.3→0.7 比例变化和最小宽度；边界 DragDelta 启动回弹时钟，不改变最小列宽。ResizablePanels 专项 5/5 通过。真实物理鼠标拖拽与 Arc 来源节奏仍待验收。

后续窗口键盘回归：对聚焦的分隔条按 Right 后，实际左列宽和双向绑定 SplitRatio 同步增长；固定 `ResizeBehavior=PreviousAndNext`。补齐边界视觉偏移与 OwnedMotion 回弹。ResizablePanels 专项 6/6 通过。真实物理鼠标、触摸和 DPI 仍待验收。

来源双栏交互对齐：显示窗口中检查折叠左列宽归零、内容不命中、恢复按钮显隐和原比例保存；Home 后立即 Left 折叠、Enter 恢复、双击重置为默认比例。DragStarted/Delta/Completed 越界 60 DIP 时达到折叠阈值；未达阈值时分隔条拖动中橡皮筋偏移，释放后启动时钟并在 450ms 内回到 0 且清理时钟。206 DIP 容器中 160/160 最小宽度按比例成为 100/100。ResizablePanels 专项 9/9 通过。物理鼠标、笔/触摸、DPI 和多栏仍未验收或实现。
