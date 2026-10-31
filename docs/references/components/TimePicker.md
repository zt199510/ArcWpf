# TimePicker 参考卡

- 来源：`https://uiarc.dev/r/time-picker.json`，采集日 2026-10-30。冻结批次：[2026-10-30-time-picker](../../../tests/Baselines/References/2026-10-30-time-picker/)（tsx `0acb7e62…`，css `571d5dd0…`）。
- 主示例（源站 forms demo chunk 确认）：label `Review time`、value `09:30`、minuteStep 15、description `Local time for the review.`，默认 12h 显示（`9:30 AM`）。展示容器宽度待核对。
- 几何（源码确认）：label 14/500，字段 gap 8；trigger 最小高 44、radius 18、padding 0 12、gap 9，clock-3 16px muted、chevron 16px muted；hover/按下/展开 border-strong + surface-muted；disabled .5。menu 在 trigger 下 6px，同宽，max-height 250，radius 26，padding 5，surface-raised + floating；option 高 36、radius 21、padding 0 12；active surface-muted（80ms）；选中项右侧 6px foreground 圆点。
- 状态/键盘：Enter/Space 打开或选中 active；ArrowDown/Up 先打开，打开后循环移动；Escape 关闭；外部 pointerdown 关闭。打开时 active=选中项（无则 0）并在首帧居中；之后仅在越界时滚动菜单自身。
- 动效：值滚动（较晚时间从下方 .35em 入、blur 4→0、.24 enter；旧值向上 .3em、blur 2、.16 standard，方向相反时镜像）；chevron 180° 于 --duration-spring(.58) + ease-spring；menu 从 y -6、scale .97、opacity 0 以 snappy spring 入场（opacity .16 enter），退场 .12 到 y -4、scale .98。Reduced：值交叉淡入 .12，menu 仅透明度。
