# HoldToConfirm 参考卡

- 来源：`https://uiarc.dev/r/hold-to-confirm.json`，采集日 2026-10-29。
- 冻结批次：[2026-10-29-hold-to-confirm](../../../tests/Baselines/References/2026-10-29-hold-to-confirm/)，含 Registry、CSS、Markdown、页面、动效令牌、许可和 manifest。
- 主示例（页面 HTML 确认）：accent tone 的 `Hold to delete`，下方提示 `Hold for 1.2 seconds, or hold Space`；完成文案在演示中待核对（文档 Usage 为 `Deleted`）。文档 API 表把 tone 默认写成 danger，与源码默认 accent 冲突，以源码为准。
- 结构：44px 最小高度的胶囊按钮；图标 18px，文本与图标成一体；填充层从左向右覆盖，填充层同时绘制反色图标/文字。
- 状态机：Idle → Holding（按住线性填充）→ Confirmed（完成回调）；Holding 提前松开或移出 24 DIP 取消并弹簧回退；Confirmed 由外部 `Confirmed=false` 重置。
- 键盘：Space/Enter 按住推进，释放提前回退；失焦取消。来源支持 pointer capture、touch vibration 和 aria hint；WPF 版本以鼠标捕获、键盘消息和 AutomationPeer 映射。
- 动效：按压约 .97，填充线性，回退使用 smooth spring，确认文案/图标与宽度使用 WPF 适配过渡；Reduced/Off 直接到正确终态。
