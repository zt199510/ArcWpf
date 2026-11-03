# Reactions

状态：实现完成，真实窗口视觉和 Popover/键盘验收待执行。

Arc Free 来源批次为 `2026-11-03-reactions`。WPF 控件支持 `Options`、`Values`、`Label`，reaction pill 显示 id/计数并切换当前用户的 `Reacted` 状态；添加按钮打开 Popup picker，支持 Escape、方向键、Home/End 和 Enter/Space。`ValueChanged` 提供变化后的列表与动作详情。

当前 WPF 适配保留 Arc 的交互结构和主题层级，reaction icon/label 会进入 pill，picker 会按工作区上下边界选择展开方向并在关闭时归还 Add 按钮焦点；计数使用可观察模型并提供方向性滚动/回弹，picker 提供选中、悬停和按下反馈，且共享高亮层跟随鼠标/键盘焦点以 spring 移动。Add 按钮会在打开时切换为关闭符号并同步自动化名称。完整 Popover 碰撞动效仍待后续视觉收口。
