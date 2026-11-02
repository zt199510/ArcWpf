# Timeline

状态：实现中，待真实窗口视觉验收。

来源：`tests/Baselines/References/2026-11-02-timeline/`。`TimelineEvent` 映射 Arc 的时间、标题、actor、meta、detail 和 tone；控件提供按日分组、最新事件排序、滚动、节点线、相对时间/本地化完整时间、有限高度、`DefaultExpanded`、详情 Expander 和方向键/Home/End 导航，且事件属性变化可实时更新绑定。控件模板现在会为渲染后新增的事件执行从顶部滑入和淡入，初始数据不播放该进入动画；日标题的真实 sticky 行为仍需窗口复验。
