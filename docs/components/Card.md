# Card / MetricCard 组件规格

状态：Arc Registry 已冻结；当前 WPF 仅覆盖基础卡片与指标卡片，来源中的详情展开、媒体、页脚状态及数字过渡尚未实现。来源见[参考卡](../references/components/Card.md)。

Card 提供 Title、Description、Content、Footer 和语义 Surface/Border/Shadow；MetricCard 保留 Value、Trend、TrendIsPositive，并新增来源式 Label、NumericValue、Suffix、Context、Change。数值属性优先于旧字符串 Value；变化值优先于旧 Trend。

2026-11-01 修正：MetricCard 的正/负趋势分别使用成功/危险色，且保留 Content 与 Footer 宿主；负趋势窗口模板测试已通过。Arc 原站字体、色值和间距应按冻结 CSS 继续对齐。

MetricCard 来源对齐：26 DIP 圆角、24 DIP 内边距，顶部标签和变化徽标、主数值和上下文分层；实际宽度小于 380 DIP 时内边距改 16，变化徽标换到下一行。正负变化可由符号自动识别，旧 `TrendIsPositive=false` 仍可显式指定危险色。`DisplayLabel`/`DisplayValue`/`DisplayChange`、变化方向和紧凑状态为只读 DP；内容/页脚为空时不留空白。Full 数值变化使用方向性位移与淡入，Reduced/Off 即时；这是 WPF 适配，来源逐位数字滚轮和变化徽标宽度弹簧尚未实现。Card 的媒体、byline、状态与详情弹窗也仍未实现。
