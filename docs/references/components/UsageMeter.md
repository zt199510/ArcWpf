# UsageMeter 参考卡

Arc 页面：`https://uiarc.dev/components/usage-meter`。2026-11-01 冻结源码与页面见 `tests/Baselines/References/2026-11-01-usage-meter/manifest.json`。来源结构为顶部标签/状态徽标、总量/说明、14px 轨道和可换行图例；分段色为 Accent 向 Surface 的 100/62/36/22% 混合，分段留 2px 间隙。`warnAt` 默认 .9，超过 limit 才进入 over。来源还包含滚动数字、超额遮纹与限额线、图例悬停/焦点预览、点击/触摸固定、方向键/Home/End/Escape 和状态播报。WPF 当前已覆盖结构、主要状态、逐位滚动、40% 可见阈值首次揭示与徽标宽度弹簧；数字羽化和物理输入观感仍有差异或待验收。
