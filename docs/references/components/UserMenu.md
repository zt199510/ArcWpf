# UserMenu 参考卡

- 来源：`https://uiarc.dev/r/user-menu.json`，采集日 2026-10-28。
- 冻结批次：[2026-10-28-user-menu](../../../tests/Baselines/References/2026-10-28-user-menu/)，含 Registry、motion、页面、Markdown、许可和 manifest。
- 主示例：头像触发账户菜单；身份头、账户入口、状态切换、主题切换、退出登录。
- 几何（源码确认）：面板 248px（CSS 15.5rem；Markdown“17.5rem”与 CSS 不符，以 CSS 为准），trigger 40px，头像 32/34px，菜单行 32px，切换行 36px。WPF 以 DIP 对应，不声称与浏览器像素一致。
- 动效（源码确认）：面板 scale .94→1 弹簧 visualDuration .3 + 透明度 160ms；行 delayChildren .03、stagger .016、y 3→0 200ms；退出 120ms scale .97；highlight snappy(.26)；thumb/状态形状 --ease-spring 580ms；Reduced 只保留 120ms 淡入淡出。WPF 映射见组件规格。
- 主示例（页面 HTML 确认）：Emma Collins，`showName`，无状态点。邮箱、plan 与菜单项取自 Usage 示例（Pro；Profile/Settings ⌘,/Billing），邮箱为 Gallery 自拟。
- 颜色：menu-line=foreground 9%、highlight=6%、track=5%、plan=4%；pressed=surface-muted 混 foreground 8%；暗色 thumb=surface-raised 混 foreground 20%。--focus-ring 为 transparent，故无焦点环。

## 视觉复刻基线（源码确认）

- Trigger：40px 高，默认头像 32px，姓名变体右侧保留 10px 内边距；打开/悬停使用 SurfaceMuted 背景，不缩放触发器。
- 面板：248px 宽，4px 内边距，10px 圆角，面板使用 SurfaceRaised、Border 与 Floating shadow。
- 行：32px 高，8px 左右内边距；菜单 hover/键盘焦点由同一个 6px 圆角 highlight 层呈现。
- 状态/主题：两组均为 36px 行高，组内 28px 分段按钮；状态只有传入 status 或回调时出现，主题默认出现。
- Popup：桌面面板以头像为变换原点，空间不足时翻转到触发器上方；外部点击关闭。
- 主题：来源组件只发出 theme change 回调；WPF Gallery 负责把 Light/Dark 应用到 ArcThemeResources，System 保持宿主选择。

