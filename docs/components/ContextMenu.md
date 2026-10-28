# ContextMenu 组件规格

状态：实现完成，程序化验证 3/3 通过，实际窗口待验收。

## 来源

Arc 官方 Context menu 页面：<https://uiarc.dev/components/context-menu>（2026-10-28 读取）。用途是对宿主区域执行右键/键盘上下文操作；行为和视觉仅作参考，不复用上游源码。

## API

`ContextMenu : Control`：

- `ContextMenu.SetMenu(DependencyObject, ContextMenu)`：把菜单附着到宿主。
- `Items`、`Label`、`IsOpen`：菜单数据、辅助名称和受控状态；MenuItem 支持图标、危险、禁用和 checked 勾号。
- `OpenFor(FrameworkElement)` / `OpenAtMouse()`：键盘宿主或鼠标位置打开。
- `Opened`、`Closed`、`ItemInvoked`：生命周期和条目事件。

## 交互

宿主右键打开并在鼠标位置呈现；Shift+F10 在宿主下方呈现。菜单打开后焦点进入条目列表；ArrowUp/Down、Home/End、Enter/Space、Escape 保留 WPF 键盘语义，禁用条目跳过且不可调用，外部点击关闭并归还焦点。

## 视觉与动效

Popup 复用 Menu 的 Surface、内缩共享 highlight、条目间距、危险色和圆角；Reduced/Off 保留状态但去除空间运动。ContextMenu 不显示触发器按钮，菜单由宿主和鼠标/键盘路径打开。

## WPF 适配差异

浏览器 contextmenu 事件映射为宿主 `PreviewMouseRightButtonUp` 和 `Shift+F10`；鼠标定位使用 WPF `Popup PlacementMode.MousePoint`，键盘路径使用宿主相对位置。右键系统菜单和原生 `System.Windows.Controls.ContextMenu` 不被替换，ArcWpf API 使用独立命名空间类型。


## Arc 对齐补充

Popup 最大高度为 300 DIP，超过时使用内部滚动；打开时使用 Surface 缩放/透明度进入动效，共享 highlight 负责鼠标/键盘移动，checked 条目显示勾号，ContextMenu 键与 Shift+F10 都可打开。
