# Reactions 参考卡

- 页面：[Arc Reactions](https://uiarc.dev/components/reactions)
- Registry：[reactions.json](https://uiarc.dev/r/reactions.json)
- 采集批次：`tests/Baselines/References/2026-11-03-reactions`
- 复用方式：行为和结构参考；不复制 React/Motion/ Radix 运行时代码。

来源确认 reaction pill、计数、个人 reacted 状态、添加按钮、Popover picker、键盘循环、Escape 焦点归还和计数滚动。WPF 实现将复用 EmojiPicker、Popover、Button 和 OwnedMotion。

TKCode.Arc 参考：提交 `5680bf55414fff07398a9fecadc0b72c900fdaba`（2026-11-03，MIT），Avalonia 实现位于 `src/TKCode.Arc/Gestures/Reactions/`。只用其测试清单核对行为覆盖，未复制代码；与 Arc 不同的扩展不视为要求。证据等级：参数来自冻结的 Arc Registry 源码，几何与颜色来自 uiarc.dev 实测。
