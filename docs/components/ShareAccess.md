# ShareAccess

状态：待验收。文档分享面板：输入姓名或邮箱生成 chip 并按角色邀请，管理成员角色，切换通用访问。事实见[参考卡](../references/components/ShareAccess.md)，验证见[验证记录](../verification/2026-11-03-batch9-replication.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`Title`、`Members`（`IList<ShareAccessMember>`，每次变更整体替换）、`Directory`、`Access`（Restricted/Link）、`LinkRole`、`Link`、`SelfId`、`DefaultInviteRole`（Editor）、`InviteHandler`（`Func<IReadOnlyList<ShareAccessContact>, ShareRole, Task>`）；事件 `MembersChanged`、`AccessChanged`、`LinkRoleChanged`、`LinkCopied`、`Done`（订阅后才出现 Done 按钮）。

## 复用情况

复用 `Avatar`、`ArcDataGridIcon`、`SwapText`、`OdometerText`、`Announcer`、Popup 模式。任务点名的 `UserMenu`、`Popover`、`Select` **未直接复用**：角色菜单需要说明行、对勾、危险项、字母跳转和端对齐，用自有 Popup 实现。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 输入建议出现，高亮在行间滑动 | 淡入 + 缩放 .97→1；高亮 snappy 弹簧；运行观察 | `RenderOptions`、`MoveHighlight` | 真实键盘：sa-suggest、方向键 | 真实 Windows 未观察 |
| Chip 弹入 | 缩放 .8→1 snappy；运行观察 | `AddChip` | sa-chips | — |
| 邀请行、错误行展开 | smooth 弹簧高度；运行观察 | `Expand` | sa-invite | — |
| 成员增删 | 行高折叠；新成员强调色 1s 淡出；运行观察 | `Unfold`、`FoldRow` | sa-sent、sa-removed | — |
| 角色标签变化 | 文字纵向交替 + morph 宽度；运行观察 | `RoleTrigger.Set` | sa-removed 前后 | — |
| 通用访问切换 | 图标旋转换图、说明滚动、链接角色从右滑入；运行观察 | `SyncAccess` | sa-link | — |
| 复制反馈 | 三标签同宽叠放，1.8s 复位；运行观察 | `ShowCopy` | sa-copied、真实点击 | — |
| 减弱动效 | 直接到位；文档描述 | `各动画入口` | 未单独运行 | 待验证 |

## 已知差异

- 输入框 chip 换行时的 FLIP 过渡未实现，换行是瞬时的（差异）。
- Share 按钮填充色 Arc 为 `oklab(.32)`，Gallery 用 Button 默认前景色。
- Gallery 头像是代码绘制的渐变，不是 Arc 图片。
- ≤420 窄屏的紧凑布局只做了填满宽度，链接角色下移未实现（待核对）。
