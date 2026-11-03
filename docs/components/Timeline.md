# Timeline

状态：WPF 分组、排序、展开、键盘导航、新事件动效和新事件滚顶已完成；节点 tone 视觉已补齐，真实窗口滚动、DPI、主题和 sticky 日标题仍待人工验收。

像素对照：2026-11-02 已按线上页面重做，结果与已知差异见 [记录](../verification/2026-11-02-next-controls-pixel.md)；离屏对比已完成，真实窗口未验证。

来源：`tests/Baselines/References/2026-11-02-timeline/`。`TimelineEvent` 映射 Arc 的时间、标题、actor、meta、detail 和 tone；控件提供按日分组、最新事件排序、滚动、节点线、相对时间/本地化完整时间、有限高度、`DefaultExpanded`、`ScrollToNew`、详情 Expander 和方向键/Home/End 导航，且事件属性变化可实时更新绑定。节点会按 `neutral`、`success`、`danger` 使用对应语义颜色和浅色背景；控件模板现在会为渲染后新增的事件执行从顶部滑入和淡入，初始数据不播放该进入动画；`ScrollToNew` 默认开启，新事件加入已渲染控件后滚动至顶部；日标题的真实 sticky 行为仍需窗口复验。
