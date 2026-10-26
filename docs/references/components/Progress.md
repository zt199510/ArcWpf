# Progress 参考卡

- 来源页面：[Arc Progress](https://uiarc.dev/components/progress/markdown)
- Registry：[progress.json](https://uiarc.dev/r/progress.json)
- 许可：来源标记 Free；Arc 许可页说明 Free 组件使用 MIT。仅参考行为和视觉，不复制上游源码。
- 主示例：`Uploading report.pdf`，68%，显示百分比；另有 100% 完成和 0% 初始状态。

Arc 规格确认：值按 `0..max` 夹取；`showValue` 显示百分比并在完成时显示勾；角色为 progressbar，提供 min/max/now/text；单一平滑动效同时驱动填充和数值，Reduced 立即到位。
