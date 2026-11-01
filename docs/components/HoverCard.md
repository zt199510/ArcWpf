# HoverCard 组件规格

状态：WPF fallback 实现，Arc Registry/CSS 当前网络不可访问，未宣称网页完全对齐。

复用 Tooltip/Popover 语义，支持 `CardContent`、`Side`、`OpenDelay`、`CloseDelay`、`IsOpen`；鼠标悬停延迟打开、离开延迟关闭、Escape 关闭和四向 Popup 放置。

2026-11-01 修正：独立 Popup 视觉树的鼠标进入会取消关闭计时，离开卡片才重新计时；键盘聚焦触发器立即打开，Escape、宿主失活和卸载关闭；每个 Side 提供反向碰撞候选。切换 Side 时保留公开 IsOpen。真实鼠标穿越与屏幕边缘位置仍待人工窗口验收。
