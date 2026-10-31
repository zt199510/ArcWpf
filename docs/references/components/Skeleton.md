# Skeleton 参考卡

- 来源：`https://uiarc.dev/r/skeleton.json`，采集日 2026-10-31（ff86a58 提交时 Registry 被拒，本次可访问）。冻结批次：[2026-10-31-skeleton](../../../tests/Baselines/References/2026-10-31-skeleton/)（tsx `73b97715…`，css `ba4b3fa1…`）。
- 主示例（源站 preview chunk 与 CSS 确认）：卡片 min(100%, 380)、1px border、radius 16、padding 20、surface，内部 grid gap 16；Skeleton label `Loading project summary`、lines 3、avatar，1600ms 后 loading=false 显示 Emma Collins 头像（44）+ `Quarterly launch`（14/500）+ `Emma shared the final interaction pass. Three files are ready for review.`（14 secondary，gap 4，padding-top 2）；footer 上边线 border-subtle、padding-top 12、两端对齐：状态 12 muted（`Loading project summary` / `Updated just now`，滚动文字）与 quiet 按钮 `Reload`（重新加载 1600ms）。
- 几何（源码确认）：root flex、gap 16、顶部对齐；avatar control-height-md 44 圆；lines grid gap 12、padding-top 4；line 高 12、宽 100%；首行 48% 宽 16 高，末行 72%；圆角 radius-control（被高度截成胶囊）；颜色 color-mix(oklab, border-strong 38%, surface)；lines 取整并限制 1–6。
- 行为：无 children 时只渲染占位（role=status、aria-busy、aria-label）；有 children 时 loading 控制占位/内容切换，外层 aria-busy。
- 动效：每块 opacity 1→.52→1、1.8s ease-in-out、按序延迟 90ms（有头像时头像为 0）、共 11 次后静止；切换时占位 .16 淡出，内容 opacity 0、y 4 以 .24 enter 进入；回到加载时内容 .16 淡出、占位 .16 standard 淡入；HeightFrame smooth spring。Reduced：无脉动，.12 淡入淡出，无高度动画。
