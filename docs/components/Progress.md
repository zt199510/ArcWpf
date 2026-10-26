# Progress 组件规格

- 状态：首版已实现，程序化验证待完成，实际窗口视觉验收待做。
- 来源：Arc 免费 Progress；用途是显示已知任务完成比例。
- 必做：`Value`/`Max` 夹取、`Label`、`ShowValue`、完成态、禁用态、辅助技术文本、明暗主题和 Full/Reduced/Off 动效。

## API

`Progress` 继承 `Control`，公开 `Value`、`Max`、`Label`、`ShowValue` 和只读计算属性 `Percentage`。值范围按 `0..Max` 夹取，`Max<=0` 时显示 0%。

## 视觉与交互

线性进度条填充容器宽度，标签与百分比位于上方；100% 显示成功色和完成标记。控件不作为 live region，不接受键盘操作。

首版暂不包含不确定进度、圆环进度、分段进度和取消按钮。
