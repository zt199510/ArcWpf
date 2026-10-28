# DropdownMenu 规格

状态：规格已按 Arc 页面重新核对；代码正在修正，待程序化与实际窗口验收。来源见[参考卡](../references/components/Menu.md)。

2026-10-27：修复触发器点击不弹出（部件类型被项目 `Button` 遮蔽）与 Popup 盖住触发器的偏移，见[修复记录](../verification/2026-10-27-menu-trigger-fix.md)。

本项目公开类型暂命名为 `Menu`，它对应 Arc 的 **Dropdown menu**，不是 WPF 原生 `System.Windows.Controls.Menu`，也不扩展为 ContextMenu、UserMenu 或子菜单。

## Arc 原始契约

- `label` 是必填触发器文字；`items` 是必填短列表；`icon` 是可选触发器前置图标。
- 条目字段是 `label`、`onSelect`、`disabled`、`icon`、`destructive`、`separatorBefore`。
- 触发器最小高度来自 `control-height-sm`，标签在变化时做文字进入和宽度弹簧；标签过长在可用宽度内省略。
- 菜单由触发器锚定，`sideOffset=6`、`align=end`、`collisionPadding=12`，碰撞时翻转；菜单最小宽度为 `12rem`，最大宽度受可用空间限制。
- 菜单开关、条目高亮、条目进入/退出和减少动效均属于组件行为，不是可省略的装饰。

## WPF 公开适配

- `Label`、`Icon`、`Items`、`IsOpen`：对应触发器、前置图标、数据条目和受控打开状态。
- `MenuItem.Command` / `CommandParameter`：WPF 对 `onSelect` 的命令映射；`ItemInvoked` 提供带类型的调用事件。
- `IsEnabled`、`IsDestructive`、`SeparatorBefore`、`InputGestureText`：前几项对应 Arc 条目状态，快捷键文本是 WPF 展示扩展。
- Popup 只在打开时呈现内容；关闭由外部点击、Escape 或条目调用触发，并把焦点还给触发器。

## 键盘与可访问性

触发器支持 Enter、Space、ArrowDown 打开；菜单支持 ArrowUp/ArrowDown 循环、Home/End、typeahead、Enter/Space 调用和 Escape 关闭。禁用项不能被选中或调用。触发器和条目保留 Button/ListBox 的原生输入与自动化语义，图标和共享高亮不参与名称。

## 视觉与动效验收

需要验证：触发器图标、标签、Chevron 的同一行对齐；菜单右边缘与触发器对齐、6 DIP 间距、12 DIP 碰撞内边距；面板 SurfaceRaised、Border、Panel 圆角和 Floating 阴影；条目图标/文字/快捷键列、分隔线和危险颜色。

Full 下菜单从触发器附近缩放淡入，条目短暂 stagger 淡入，共享 highlight 在鼠标移动时以 snappy spring 滑动、键盘移动时跳转；Reduced 去掉空间运动并保留短淡入。中途关闭或重开必须从当前视觉值继续，不遗留 Popup 或动画时钟。

## 明确不属于本组件

本规格不包含右键 ContextMenu、账户 UserMenu、子菜单、移动端底部 sheet、默认命令或搜索命令面板。实际 Popup、焦点、DPI、读屏和视觉结果仍需窗口验收。


