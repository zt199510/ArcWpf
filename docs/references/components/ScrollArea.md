# ScrollArea 参考卡

- 来源：`https://uiarc.dev/r/scroll-area.json`，采集日 2026-10-30。冻结批次：[2026-10-30-scroll-area](../../../tests/Baselines/References/2026-10-30-scroll-area/)（tsx `82c53c56…`，css `904fdc63…`）。
- 主示例（页面 HTML 与 demo chunk 确认）：720px 内两块 panel（gap 16，auto-fit min 300）。Activity：header 52 高、padding 8 10 8 18，标题 14/500，顶部有内容被滚过时出现 `Back to top` pill（32 高、surface-muted、12/500、arrow-up）；列表区 300 高，12 条（头像 32 圆 + 名字 500 + 动作 secondary + 时间 12 muted；行 padding 8 10、radius 18、hover surface-muted）。Rooms：horizontal、fade 36、snap x mandatory；6 张卡 188 宽、4:5 照片 radius 16、名称 500、备注 12 secondary；header 右侧两个 32px 圆形箭头按 left/right 边缘启用，点击滚动一张卡 + 12。
- 几何（源码确认）：轨道 11px 宽（x 轴 11 高），y 轨 top/bottom 3、right 1；both 时 y 轨 bottom 13、x 轨 right 13；pill 5px，hover/拖动 7px（中心不变），颜色 foreground 30% → 46%；thumb 最小 28；fade 默认 28。
- 行为：原生滚动；auto 模式在滚动、拖动、悬停时显示，always 在溢出时始终显示；hideDelay 900ms；拖动 thumb 按比例映射；点击轨道朝指针翻页 90% 视口（平滑滚动）；horizontal 下纵向滚轮转为横移（仍有空间时，Ctrl 除外）；label 使视口成为命名 region；onEdgeChange 在边缘状态变化时回调。
- 动效：轨道 opacity 出现 .16、消失 considered .48（ease-standard）；pill 宽度/颜色 .16。Reduced：无过渡、翻页立即。
