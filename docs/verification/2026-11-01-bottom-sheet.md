# BottomSheet 验证记录（2026-11-01）

来源 Registry 当前网络访问被拒绝，未伪造网页参数。

待运行：Controls/Gallery 构建、BottomSheet 专项、遮罩、Escape、焦点、底部动效和真实窗口主题。

后续窗口布局回归：BottomSheet 继承 Drawer 模板和动效，`SnapHeight` 现在实际绑定到内部表面；在 600 DIP DrawerHost 中测得 420→300 DIP 动态高度，宿主缩至 220 DIP 时表面被限制为 220 DIP，Off 关闭立即释放模态。BottomSheet 专项 4/4 通过。原独立主题样式的跨资源字典 `StaticResource` 运行时解析错误已移除。物理鼠标、Escape、主题、多 DPI 和来源未知的 detent/safe-area 仍待验收。
