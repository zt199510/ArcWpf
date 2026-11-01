# HoverCard 验证记录（2026-11-01）

来源：`tests/Baselines/References/2026-11-01-hover-card/manifest.json`，已冻结 Arc Registry、CSS、Markdown、页面与动效令牌，采集日期 2026-11-01。

待运行：Controls/Gallery 构建、HoverCard 专项、悬停延迟、Escape、四向放置、外部点击和主题。

2026-11-01 补充：显示窗口中打开 Popup，模拟触发器离开后进入卡片，关闭计时被取消；切换 Side 后 Popup 与 IsOpen 保持打开。按 Arc 冻结参数补齐 open 500ms、close 140ms、warm intent 80ms、warm window 300ms、296 DIP 卡片宽度、四向碰撞和键盘/触摸路径。HoverCard 专项 4/4 通过。真实鼠标穿越、碰撞坐标和视觉仍待人工验收。

后续审计：WPF 在 TouchUp 后可能合成 MouseUp/Click；触摸切换后的本轮合成点击现被抑制，避免关闭后立即重开。精确类名过滤的五组件专项合计 26/26 通过；原宽泛过滤器的 33 项还包含 7 项 ExpandableCardTests。物理触摸路径尚未实际操作，不能据此标为触摸验收通过。

放置与焦点对齐：Popup 改用共享 PopoverPositioner/多显示器工作区求解；显示窗口靠近底边时 ActualSide 翻为 Top、实际卡片边界保留 12 DIP，2000 DIP 长内容进入内部滚动。Owner 移回上方后翻回 Bottom；窗口外部按钮 PreviewMouseDown 即时关闭且原焦点保留。阴影外框参与跨缝进入/离开，原回归继续通过。Full 打开中切 Off/Reduced 后空间时钟停止，Escape 关闭后直到触发器离开不再排队打开。HoverCardProfile 四行按 35ms 错峰，Dispatcher 推进后透明度/位移终态与时钟清理通过；Popup 动效级别和 DataContext 继承已测。HoverCard 专项 9/9 通过，物理输入、多 DPI 与逐帧视觉仍未人工验收。
