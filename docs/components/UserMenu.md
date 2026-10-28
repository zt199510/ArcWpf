# UserMenu

UserMenu 是由头像触发的账户菜单，基于 WPF `Control`、`Popup` 和 `Avatar` 实现。公开契约包括 `User`、`Items`、`Status`、`Theme`、`IsOpen`、`ShowName` 与 `ShowTheme`；`ItemInvoked`、`StatusChanged`、`ThemeChanged` 提供消费方事件。

## 范围矩阵

| 交付项 | 实现状态 | 证据/限制 |
|---|---|---|
| 头像、姓名、邮箱、计划 | 已实现 | Avatar 模板与 UserMenuTheme |
| Available/Busy/Away/Offline | 已实现 | 仅 `ShowStatus=True` 时显示，状态点使用实心/横杠/空心形状 |
| 菜单项、快捷键文本、命令 | 已实现 | `UserMenuItem` 与 `Invoke` |
| Light/Dark/System | 已实现 | 主题选择事件；应用切换由宿主决定 |
| Popup、外部点击、Escape、Tab、焦点返回 | 已实现 | Custom placement、窗口预览点击、菜单键盘路由 |
| 打开/关闭动效 | 已实现 | OwnedMotion：透明度 + 缩放，ArcMotion Full/Reduced/Off 分支 |
| 移动端 bottom sheet | 暂缓 | WPF 桌面 Popup 不复制 Web 响应式断点 |

来源冻结于 `tests/Baselines/References/2026-10-28-user-menu/`。Arc Registry 源码确认结构和交互意图；WPF 布局采用 Avatar + Popup + ItemsControl 的原生映射。来源源码与本实现不直接复用，许可页已归档。

