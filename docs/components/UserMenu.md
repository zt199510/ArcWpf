# UserMenu

UserMenu 是由头像触发的账户菜单，基于 WPF `Control`、`Popup`、`Avatar`、`Button` 与 `RadioButton` 实现。公开契约包括 `User`、`Items`、`Status`、`Theme`、`IsOpen`、`ShowName`、`ShowStatus`、`ShowTheme`、`Align`、`SignOutCommand`、`SignOutAsync`、`SignOutKeys` 与只读 `IsSigningOut`；`ItemInvoked`、`StatusChanged`、`ThemeChanged` 提供消费方事件。

## 范围矩阵（2026-10-28 第三轮重写）

| 交付项 | 本次范围 | 实现状态 | 证据/剩余差异 |
|---|---|---|---|
| Trigger：40px 胶囊、32px 头像、姓名+chevron、hover/open/pressed 底色 | 必做 | 已实现，待窗口验收 | 底色用两层透明度过渡 160ms；chevron 打开旋转 180° |
| 面板：248px、4px 内边距、10px 圆角、Border、浮动阴影 | 必做 | 已实现，待窗口验收 | 外框 48px 透明边距承载阴影（同 Select） |
| 身份头：34px 头像、姓名 13 Medium、plan 徽标、邮箱 12 muted | 必做 | 已实现，待窗口验收 | 姓名 MaxWidth 150 为 WPF 适配（CSS 用 flex 收缩） |
| 账户行、快捷键 kbd、命令 | 必做 | 已实现，待窗口验收 | `Keys`/`SignOutKeys` 以空格分隔各键 |
| Status/Theme 分段切换（28px 段、26px 轨道、滑动 thumb） | 必做 | 已实现，待窗口验收 | 状态仅 `ShowStatus=True` 时出现；主题默认出现 |
| PresenceDot 形状：实心/横杠/空心，颜色交叉淡化 | 必做 | 已实现，待窗口验收 | Offline 仅用于外部显示，切换器只有三项 |
| 单一 highlight 跟随指针与键盘，经过切换行时释放 | 必做 | 已实现，待窗口验收 | snappy 弹簧滑动，高度同步 32↔36 |
| 键盘：Enter/Space/↓ 打开到首行，↑ 到末行，↑↓ 循环，Home/End，←→ 切换，typeahead，Esc/Tab 关闭并归还焦点 | 必做 | 已实现，待窗口验收 | 指针打开时只聚焦面板、不显示 highlight |
| 外部点击/窗口失活关闭；上下翻转；Start/Center/End 对齐 | 必做 | 已实现，待窗口验收 | 变换原点为头像中心 |
| 动效：面板 .94→1 弹簧 + 160ms 淡入；行 30ms 起 16ms 间隔 3px 上浮；退出 120ms 淡出 .97 | 必做 | 已实现，待窗口验收 | Reduced：120ms 淡入淡出；Off：无动画 |
| 异步退出：spinner + “Signing out”，任务结束后关闭 | 必做 | 已实现，待窗口验收 | 文字上浮无 blur（WPF 未加 BlurEffect） |
| 段/图标颜色过渡（CSS transition color） | 必做 | 静态切换 | 已知差异：未逐帧过渡 |
| 移动端 bottom sheet、拖拽关闭 | 明确暂缓 | 未实现 | 桌面 WPF 无 640px 断点场景 |

来源冻结于 `tests/Baselines/References/2026-10-28-user-menu/`。Arc Registry 源码确认结构、参数和交互；来源源码与本实现不直接复用，许可页已归档。图标为 lucide 路径（ISC 许可，见 THIRD-PARTY-NOTICES）。
