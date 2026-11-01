# Card / MetricCard 组件规格

状态：Arc Registry 已冻结；WPF 已覆盖 Card 的媒体、byline、状态与详情模态，以及 MetricCard 的来源布局和方向性数值过渡。共享元素 morph 与逐位滚轮仍未实现；真实视觉待验收。来源见[参考卡](../references/components/Card.md)。

Card 提供 Title、Description、Content、Footer 和语义 Surface/Border/Shadow；MetricCard 保留 Value、Trend、TrendIsPositive，并新增来源式 Label、NumericValue、Suffix、Context、Change。数值属性优先于旧字符串 Value；变化值优先于旧 Trend。

2026-11-01 修正：MetricCard 的正/负趋势分别使用成功/危险色，且保留 Content 与 Footer 宿主；负趋势窗口模板测试已通过。Arc 原站字体、色值和间距应按冻结 CSS 继续对齐。

MetricCard 来源对齐：26 DIP 圆角、24 DIP 内边距，顶部标签和变化徽标、主数值和上下文分层；实际宽度小于 380 DIP 时内边距改 16，变化徽标换到下一行。正负变化可由符号自动识别，旧 `TrendIsPositive=false` 仍可显式指定危险色。`DisplayLabel`/`DisplayValue`/`DisplayChange`、变化方向和紧凑状态为只读 DP；内容/页脚为空时不留空白。Full 数值变化使用方向性位移与淡入，Reduced/Off 即时；这是 WPF 适配，来源逐位数字滚轮和变化徽标宽度弹簧尚未实现。

Card 增加 `Media`/`MediaTemplate`、`Avatar`、`Meta`、`Status`、`Action`、`Details`、`IsOpen`（双向）、`DefaultOpen`，并保留 `Content`/`Footer`。`HasDetails`、`HasByline`、`IsPointerHover` 为只读状态。媒体位于表面顶部，正文 20 DIP 内边距，页脚为头像、身份/状态和独立操作三列；空槽不占高度。提供 Details 时标题及不含独立按钮的表面可以打开详情，`DialogHost` 承载原生模态/焦点限制/Escape/遮罩关闭与焦点归还。媒体数据配合 `MediaTemplate` 在卡片和详情各实例化一次；直接传入的 UIElement 仅显示在卡片，避免 WPF 重复父级。Full 悬停上移 2 DIP、媒体缩放 1.04，状态行淡入上移；Reduced/Off 无位移。Gallery 使用 WPF 绘制的订房界面预览，不是原站照片。Arc 的共享元素 morph、媒体细节和逐词状态滚动尚未实现；详情需祖先 `DialogHost` 是 WPF 适配前提。
