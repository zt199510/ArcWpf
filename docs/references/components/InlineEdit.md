# InlineEdit 参考卡

- 来源：`https://uiarc.dev/r/inline-edit.json`，采集日 2026-10-29。
- 冻结批次：[2026-10-29-inline-edit](../../../tests/Baselines/References/2026-10-29-inline-edit/)，含 Registry、CSS、Markdown、页面、动效令牌、许可和 manifest。
- 主示例（页面 HTML 确认）：Project settings 面板；Project name 为 title `Atlas web app`，Description 为 multiline body `Offline-first field notes for the Atlas survey team. Drafts sync the moment a connection returns.`；底部 Switch “Fail the next save” 与 “Last edited by Emma Collins on Mon 15 Sep”。
- 几何（源码确认）：title text-xl 22px/500、行高 round(22×1.3, 2px)=28、padding 3/8；body 14px/400、行高 21、padding 5/8；显示态和编辑态共用边框与度量；frame 贴合文字，动作槽在 frame 右缘外 6px；行尾预留 66px（多行 38px）；radius-control 18px，焦点环 transparent。
- 状态机：idle → editing → saving → saved/failed；失败恢复 committed value；外部 value 在非编辑/非 saving 状态同步。
- 键盘：显示态 Enter/Space 进入编辑；编辑态 Enter 保存，multiline 的 Shift+Enter 换行，Escape 取消；外部失焦提交。
- 动效：文字 blur/y 交叉切换，编辑框/多行高度和输入宽度使用 spring；saved 勾号绘制，failed 消息零高度展开。
