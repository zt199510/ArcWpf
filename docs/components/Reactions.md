# Reactions

状态：按 Arc 源码重写，离屏对比通过；真实窗口、Popup 焦点和键盘验收待执行。

Arc Free 来源批次为 `2026-11-03-reactions`。WPF 控件支持 `Options`、`Values`、`Label`，reaction pill 显示 id/计数并切换当前用户的 `Reacted` 状态；添加按钮打开 Popup picker，支持 Escape、方向键、Home/End 和 Enter/Space。`ValueChanged` 提供变化后的列表与动作详情。

当前 WPF 适配保留 Arc 的交互结构和主题层级，reaction icon/label 会进入 pill，picker 会按工作区上下边界选择展开方向并在关闭时归还 Add 按钮焦点；计数使用可观察模型并提供方向性滚动/回弹，picker 提供选中、悬停和按下反馈，且共享高亮层跟随鼠标/键盘焦点以 spring 移动。Add 按钮会在打开时切换为关闭符号并同步自动化名称。完整 Popover 碰撞动效仍待后续视觉收口。

## 2026-11-03 复刻审计后的实现

本节取代上文与它冲突的旧说明。此前的实现距 Arc 很远，测试通过并不代表复刻达标，详见 [验证记录](../verification/2026-11-03-replica-batch-audit.md)。

`Reaction.Icon`、`ReactionOption.Icon` 接受 Lucide 名称或元素。胶囊布局由 `ReactionsBar` 负责，位置变化以 morph 弹簧滑动，高度变化有 700ms 的弹簧窗口；图标脉冲是 stiffness 420、damping 39 的冲量，连点会叠加。选择器用 Popup，尺寸 262×76。模板只保留 `PART_Bar`。

已知近似：胶囊入场、图标切换和选择器错峰用 OwnedMotion 的采样弹簧（.36s 窗口），不是物理弹簧。
