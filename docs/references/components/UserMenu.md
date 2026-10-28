# UserMenu 参考卡

- 来源：`https://uiarc.dev/r/user-menu.json`，采集日 2026-10-28。
- 冻结批次：[2026-10-28-user-menu](../../../tests/Baselines/References/2026-10-28-user-menu/)，含 Registry、motion、页面、Markdown、许可和 manifest。
- 主示例：头像触发账户菜单；身份头、账户入口、状态切换、主题切换、退出登录。
- 几何：桌面面板约 248 DIP，头像 34/48 DIP，菜单行 32 DIP；这些是 WPF 适配值，不声称与浏览器像素一致。
- 动效：来源确认面板缩放/淡入、行级 stagger、状态标记形状变化；当前 WPF 已实现桌面面板缩放/淡入、单层高亮、上下翻转和主题组选择状态；Reduced/Off 及最终像素比例仍需真实窗口验证。

## 视觉复刻基线（源码确认）

- Trigger：40px 高，默认头像 32px，姓名变体右侧保留 10px 内边距；打开/悬停使用 SurfaceMuted 背景，不缩放触发器。
- 面板：248px 宽，4px 内边距，10px 圆角，面板使用 SurfaceRaised、Border 与 Floating shadow。
- 行：32px 高，8px 左右内边距；菜单 hover/键盘焦点由同一个 6px 圆角 highlight 层呈现。
- 状态/主题：两组均为 36px 行高，组内 28px 分段按钮；状态只有传入 status 或回调时出现，主题默认出现。
- Popup：桌面面板以头像为变换原点，空间不足时翻转到触发器上方；外部点击关闭。
- 主题：来源组件只发出 theme change 回调；WPF Gallery 负责把 Light/Dark 应用到 ArcThemeResources，System 保持宿主选择。

