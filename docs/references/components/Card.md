# Card 参考卡

Arc 页面：`https://uiarc.dev/components/card`、`metric-card`。2026-11-01 冻结源码与页面见 `tests/Baselines/References/2026-11-01-card/manifest.json` 和 `tests/Baselines/References/2026-11-01-metric-card/manifest.json`。Card 来源包含媒体、页脚状态与可展开详情；MetricCard 来源包含数字值、后缀、上下文、变化值及数字过渡。当前 WPF 模板仍需逐项对齐。

源码几何：Card 表面 `--radius-panel`、内容 `--space-5`、媒体最小高 112px、页脚 `--space-5` 间距；悬停上移 2px、媒体缩放 1.04。详情面板宽度上限 30rem，有 7px 背景模糊，卡片与面板的表面、媒体、标题和 byline 共享布局过渡。MetricCard 表面使用 `--radius-surface`、`--space-6` 内边距；小于 380px 时收紧为 `--space-4` 并使变化徽标换行。WPF 已采用对应 DIP 结构，但详情目前复用 Dialog 的淡入/缩放，没有来源共享布局 morph。
