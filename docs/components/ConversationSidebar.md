# ConversationSidebar

状态：待验收。会话列表：新建、搜索、分组、就地重命名、归档与恢复。事实见[参考卡](../references/components/ConversationSidebar.md)。

Arc 没有同名组件。这是 ArcWpf 的组合设计，视觉取自 Arc Pro 工作区侧栏和聊天历史菜单的公开显示结果，没有使用 Pro 源码；行为按用户需求定义，**没有原版可供比对**。

## 范围与 API

`ConversationItem`（`Id`、`Title`、`Group`、`IsArchived`）；`ConversationSidebar`：`Conversations`、`SelectedId`、`Query`；事件 `NewRequested`、`Selected`、`Renamed`、`Archived`、`Restored`。键盘：方向键、Home、End 移动，F2 重命名，Delete 归档，搜索框内 Down 进入列表、Esc 清空。

## 复用情况

复用 `SpringHeightBox`、`TurningChevron`、`OverlayScrollViewer`、`ThreadUi.PlainTextBox`。未用 `Search`（最小高 44，侧栏为 32）和 `InlineEdit`（自带保存流程和外框，行内放不下），均自建最小实现。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 归档 | 行高弹簧 snappy 加淡出；WPF适配 | `Archive` | 真实输入 side-archived；Delete 与按钮两种入口 | 无原版 |
| 归档区 | 高度弹簧，箭头翻转；WPF适配 | `SetArchiveOpen` | 真实输入 side-archopen | 无原版 |
| 新建 | 新行高度展开；WPF适配 | `Refresh` | 真实输入 side-new | 无原版 |
| 行操作 | 悬停或键盘焦点时显示；WPF适配 | `Actions` | 真实输入 hover actions=True | 无原版 |

## 已知差异

- 列表裁剪行，所以键盘焦点画在行内边，不用共享外环。
- 搜索只过滤标题；条目多时没有虚拟化，未测大列表性能。
- 归档选中会话时选中下一个；归档区内的条目不接管选中。
- 验证环境为 Wine，字体为替身；真实 Windows 未运行，见[验证记录](../verification/2026-11-04-batch12-replication.md)。
