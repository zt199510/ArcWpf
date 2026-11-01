# Stepper 参考卡

Arc 页面：`https://uiarc.dev/components/stepper`。2026-11-01 冻结 Registry、CSS、Markdown、页面与动效令牌，文件及 SHA-256 见 `tests/Baselines/References/2026-11-01-stepper/manifest.json`。

源码事实：`current` 从 0 到步骤数，等于步骤数时全部完成；只有已完成步骤可回选。错误信息取代说明并将图标改为警示态。支持横向/纵向、`details=all/current`、紧凑态与 30rem 容器响应式折叠；横向连接线逐项填充，当前标记带外环。方向键移动可达步骤的焦点，Home/End 跳到首项/当前项，Enter/Space 选择已完成项。以上为来源事实，不代表 WPF 均已实现。
