# BottomSheet 组件规格

状态：WPF fallback 实现，Arc Registry/CSS 当前网络不可访问，未宣称网页完全对齐。

`BottomSheet : Drawer` 复用 DrawerHost、遮罩、Escape、外部点击、焦点归还和 Bottom 方向动效。`SnapHeight` 默认 420 DIP，拒绝负数和非有限值，动态控制底部表面高度，并受 DrawerHost 实际高度约束；不改变整个模态层的尺寸。当前未实现来源未知的拖拽 detent 或 safe-area 行为。
