# SwipeActions 参考卡

- 页面：[Arc Swipe actions](https://uiarc.dev/components/swipe-actions)
- Registry：[swipe-actions.json](https://uiarc.dev/r/swipe-actions.json)
- 采集批次：`tests/Baselines/References/2026-11-03-swipe-actions`
- 复用方式：交互结构参考；未复制 React/Motion/Radix 运行时代码。

来源确认水平手势锁定、leading/trailing 操作、full swipe、Escape/More 键盘替代和一次只展开一行。WPF 差异见 `docs/components/SwipeActions.md`。

TKCode.Arc 参考：提交 `5680bf55414fff07398a9fecadc0b72c900fdaba`（2026-11-03，MIT），Avalonia 实现位于 `src/TKCode.Arc/Gestures/SwipeActions/`。只用其测试清单核对行为覆盖，未复制代码；与 Arc 不同的扩展不视为要求。证据等级：参数来自冻结的 Arc Registry 源码，几何与颜色来自 uiarc.dev 实测。
