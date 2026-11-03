# RadioCards 参考卡

- 页面：[Arc Radio cards](https://uiarc.dev/components/radio-cards)
- Registry：[radio-cards.json](https://uiarc.dev/r/radio-cards.json)
- 采集批次：`tests/Baselines/References/2026-11-03-radio-cards`
- 复用方式：行为与结构参考；未复制 React/Motion 运行时代码。

来源确认：radiogroup/radio 语义、单一 Tab 停靠点、方向键循环、Home/End、禁用原因、网格/列表布局、选中环和指示点。WPF 差异及未验证项目见 `docs/components/RadioCards.md`。

TKCode.Arc 参考：提交 `5680bf55414fff07398a9fecadc0b72c900fdaba`（2026-11-03，MIT），Avalonia 实现位于 `src/TKCode.Arc/Toggles/RadioCards/`。只用其测试清单核对行为覆盖，未复制代码；与 Arc 不同的扩展不视为要求。证据等级：参数来自冻结的 Arc Registry 源码，几何与颜色来自 uiarc.dev 实测。
