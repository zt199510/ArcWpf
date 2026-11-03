# Carousel 参考卡

- 页面：[Arc Carousel](https://uiarc.dev/components/carousel)
- Registry：[carousel.json](https://uiarc.dev/r/carousel.json)
- 采集批次：`tests/Baselines/References/2026-11-03-carousel`
- 许可：以采集批次 `license.txt` 为准；本实现仅行为参考，不复制 React 或 Motion 运行时代码。

主示例是 Harbour suites 三项短内容。来源确认的关键结构为居中滑轨、相邻项缩放/变暗、拖拽/甩动、方向键、指示点、可选自动播放与悬停/焦点暂停。WPF 适配项和未验证边界记录在 `docs/components/Carousel.md`。

TKCode.Arc 参考：提交 `5680bf55414fff07398a9fecadc0b72c900fdaba`（2026-11-03，MIT），Avalonia 实现位于 `src/TKCode.Arc/Media/Carousel/`。只用其测试清单核对行为覆盖，未复制代码；与 Arc 不同的扩展不视为要求。证据等级：参数来自冻结的 Arc Registry 源码，几何与颜色来自 uiarc.dev 实测。
