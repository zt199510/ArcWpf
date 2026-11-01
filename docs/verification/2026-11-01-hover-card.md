# HoverCard 验证记录（2026-11-01）

来源：`tests/Baselines/References/2026-11-01-hover-card/manifest.json`，已冻结 Arc Registry、CSS、Markdown、页面与动效令牌，采集日期 2026-11-01。

待运行：Controls/Gallery 构建、HoverCard 专项、悬停延迟、Escape、四向放置、外部点击和主题。

2026-11-01 补充：显示窗口中打开 Popup，模拟触发器离开后进入卡片，关闭计时被取消；切换 Side 后 Popup 与 IsOpen 保持打开。按 Arc 冻结参数补齐 open 500ms、close 140ms、warm intent 80ms、warm window 300ms、296 DIP 卡片宽度、四向碰撞和键盘/触摸路径。HoverCard 专项 7/7 通过。真实鼠标穿越、碰撞坐标和视觉仍待人工验收。
