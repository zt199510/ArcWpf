# ExpandingSearch 组件规格

状态：实现中。Arc `expanding-search` Registry 在 2026-10-31 当前环境无法访问，工作区无冻结副本；本版依据 Arc Search 的已冻结令牌与网页公开交互意图实现，来源缺口保留待补。

## API

`ExpandingSearch : Control`：`Query`、`Placeholder`、`IsExpanded`、`WidthCollapsed`、`WidthExpanded`、`ExpandOnFocus`、`SubmitCommand`、`SubmitCommandParameter`，以及 `Expanded`、`Collapsed`、`Submitted` 事件。

## 范围

- collapsed：40 DIP 圆形搜索图标按钮。
- expanded：40 DIP 高圆角输入面板，默认展开宽度 320 DIP。
- 输入、清除、Escape 清除/收起、Enter 提交、焦点归还。
- Full/Reduced/Off 宽度过渡；明暗主题动态资源。
- 移动端媒体查询、搜索结果面板和来源 Registry 细节待来源恢复后补齐。
