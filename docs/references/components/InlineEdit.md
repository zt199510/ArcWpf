# InlineEdit 参考卡

- 来源：`https://uiarc.dev/r/inline-edit.json`，采集日 2026-10-29。
- 冻结批次：[2026-10-29-inline-edit](../../../tests/Baselines/References/2026-10-29-inline-edit/)，含 Registry、CSS、Markdown、页面、动效令牌、许可和 manifest。
- 主示例：Project name 点击后原位变成输入框；Enter 保存，Escape 取消；异步保存显示 saving，成功显示勾号，失败回滚并提供重试。
- 几何：title 20px/1.3/500，body 14px/1.5/400；显示态和编辑态共用 padding、字体、行高和透明边框；右侧动作槽预留约 66px。
- 状态机：idle → editing → saving → saved/failed；失败恢复 committed value；外部 value 在非编辑/非 saving 状态同步。
- 键盘：显示态 Enter/Space 进入编辑；编辑态 Enter 保存，multiline 的 Shift+Enter 换行，Escape 取消；外部失焦提交。
- 动效：文字 blur/y 交叉切换，编辑框/多行高度和输入宽度使用 spring；saved 勾号绘制，failed 消息零高度展开。
