# Card / MetricCard 组件规格

状态：Arc Registry 已冻结；当前 WPF 仅覆盖基础卡片与指标卡片，来源中的详情展开、媒体、页脚状态及数字过渡尚未实现。来源见[参考卡](../references/components/Card.md)。

Card 提供 Title、Description、Content、Footer 和语义 Surface/Border/Shadow；MetricCard 扩展 Value、Trend、TrendIsPositive。

2026-11-01 修正：MetricCard 的正/负趋势分别使用成功/危险色，且保留 Content 与 Footer 宿主；负趋势窗口模板测试已通过。Arc 原站字体、色值和间距应按冻结 CSS 继续对齐。
