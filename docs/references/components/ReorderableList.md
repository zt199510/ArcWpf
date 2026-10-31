# ReorderableList 参考卡

- 来源：`https://uiarc.dev/r/reorderable-list.json`，采集日 2026-10-31（7e8103f 提交时 Registry 被拒，本次可访问）。冻结批次：[2026-10-31-reorderable-list](../../../tests/Baselines/References/2026-10-31-reorderable-list/)（tsx `3521ec8b…`，css `a09dd01e…`）。
- 主示例（源站 data preview chunk 与 CSS 确认）：容器 min(100%, 460px)；label `Launch checklist`；items `Clarify onboarding / Product brief`、`Review empty states / Design system`、`Ship the update / Release checklist`。
- 几何（源码确认）：list grid gap 8；row min-height 56、1px border、radius-control 18、padding 7 16 7 7、surface、resting 阴影、gap 12；::after floating 阴影层 opacity 0→1；handle 40 方块、radius 11、surface-muted、muted grip-vertical 16，抬起时 border/foreground，hover foreground；copy：title 14/500 省略、detail 12 muted、gap 2；position 右侧 12 muted 等宽数字 `01`。
- 行为：只能从把手拖动（无动量，松手回槽）；把手 ArrowUp/Down 与邻居交换并保持焦点；把手名称 `Reorder {title}. Position {n} of {count}. Use arrow keys.`；按下即抬起，松开在原位则落下；键盘移动后抬起 260ms。
- 动效：抬起 scale 1.02（snappy），阴影层 .16 入 / .24 出；其他行 layout 平移 smooth；松手 bounceStiffness 520 / damping 40；序号按移动方向 ±.5em 滚动（.24 enter，旧值 .12 standard，blur 2）。Reduced：不缩放，落槽立即，序号直接换文字。
