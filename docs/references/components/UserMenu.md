# UserMenu 参考卡

- 来源：`https://uiarc.dev/r/user-menu.json`，采集日 2026-10-28。
- 冻结批次：[2026-10-28-user-menu](../../../tests/Baselines/References/2026-10-28-user-menu/)，含 Registry、motion、页面、Markdown、许可和 manifest。
- 主示例：头像触发账户菜单；身份头、账户入口、状态切换、主题切换、退出登录。
- 几何：桌面面板约 248 DIP，头像 34/48 DIP，菜单行 32 DIP；这些是 WPF 适配值，不声称与浏览器像素一致。
- 动效：来源确认面板缩放/淡入、行级 stagger、状态标记形状变化；当前 WPF 首版保留状态转换契约，Popup 动效和 Reduced/Off 真实窗口验证待完成。
