# ResizablePanels 验证记录（2026-11-01）

来源：`tests/Baselines/References/2026-11-01-resizable-panels/manifest.json`，已冻结 Arc Registry、CSS、Markdown、页面与动效令牌，采集日期 2026-11-01。

待运行：Controls/Gallery 构建、ResizablePanels 专项、拖拽、键盘、边界和实际窗口主题。

2026-11-01 补充：Windows STA 显示窗口中实测 600 DIP 容器的 0.3→0.7 比例变化和最小宽度；边界 DragDelta 启动回弹时钟，不改变最小列宽。ResizablePanels 专项 5/5 通过。真实物理鼠标拖拽与 Arc 来源节奏仍待验收。

后续窗口键盘回归：对聚焦的分隔条按 Right 后，实际左列宽和双向绑定 SplitRatio 同步增长；固定 `ResizeBehavior=PreviousAndNext`。补齐边界视觉偏移与 OwnedMotion 回弹。ResizablePanels 专项 6/6 通过。真实物理鼠标、触摸和 DPI 仍待验收。

来源双栏交互对齐：显示窗口中检查折叠左列宽归零、内容不命中、恢复按钮显隐和原比例保存；Home 后立即 Left 折叠、Enter 恢复、双击重置为默认比例。DragStarted/Delta/Completed 越界 60 DIP 时达到折叠阈值；未达阈值时分隔条拖动中橡皮筋偏移，释放后启动时钟并在 450ms 内回到 0 且清理时钟。206 DIP 容器中 160/160 最小宽度按比例成为 100/100。ResizablePanels 专项 9/9 通过。物理鼠标、笔/触摸、DPI 和多栏仍未验收或实现。

多栏集成回归：三栏窗口生成五个 Grid 列和两条分隔条；第一条方向键调整只改变前两栏，折叠/恢复和双击重置回默认份额。默认折叠恢复取配置份额；206 DIP 容器的三栏最小宽度按比例分摊。第二条 DragStarted/Delta/Completed 使中、右栏交换 40 DIP，左栏不变，`Shares` 合计 100 并触发事件。越界 100 DIP 时橡皮筋偏移并折叠；清空 Panels 后旧双栏模板内容恢复。Gallery 三栏 XAML 构建通过。ResizablePanels 专项 14/14 通过；物理输入、多 DPI、速度投影、宽度弹簧和 UIA RangeValue 待验收或实现。

模板生命周期复验：换空模板再恢复原模板时，同一 `ResizablePanel` 实例重新进入新 Grid，默认份额仍为 40/60；使用方给面板的 Visibility 与 IsHitTestVisible 值不被组控件覆盖，折叠只控制内部宿主。ResizablePanels 专项 15/15 通过。

释放投影补充：双栏和多栏共享最近 90ms 样本及 50ms 静止截止，投影距离仅用于折叠判定；确定性测试验证最近样本产生约 105.6 DIP 投影、静止后为 0。原拖拽/键盘/折叠窗口回归继续通过，ResizablePanels 专项 16/16。物理甩动、列宽弹簧和 UIA RangeValue 尚未验收或实现。
