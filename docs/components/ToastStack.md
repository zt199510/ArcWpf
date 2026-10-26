# ToastStack 通知堆栈规格

状态：已实现并完成程序化验证，待实际窗口验收。

`ToastStack : ItemsControl` 用于管理多个短暂、非阻塞通知。它把最新通知放在前面，折叠时让后续通知露出，悬停或键盘焦点时展开并暂停计时。

## API

- `Show(ToastStackOptions)` / `Show(title, description, type, duration)`：创建通知并返回 ID。
- `Update(id, ToastStackPatch)`：更新已有通知，重置其默认计时。
- `Dismiss(id?)`：关闭指定通知，省略 ID 时关闭全部。
- `Duration`：基础时长，默认 5000ms；0 或负数表示不自动关闭。
- `Limit`：队列上限，默认 12。
- `VisibleToasts`：同时显示数量，默认 3。
- `Position`、`IsContained`、`IsExpanded`、`Hotkey`、`Label`：布局、交互和无障碍设置。
- `ToastStackType`：Success、Info、Warning、Error、Loading。
- `ToastStackOptions` 支持 ID、标题、说明、动作文案、命令和自定义时长。

## 视觉与交互

通知卡片默认宽度416 DIP、圆角26、内边距 16/12/12/12。折叠堆叠露出14 DIP，缩放步长5%；展开时卡片间距12 DIP。悬停或焦点会展开堆栈并暂停计时，Alt+T 将焦点移入第一个通知。loading 永不自动关闭，warning/error 使用基础时长的1.6倍。

首期暂不实现 Arc 的右滑拖拽投掷、复杂内容高度弹簧、全局通知服务和系统级窗口宿主；这些不影响队列、更新、关闭和主题使用。
