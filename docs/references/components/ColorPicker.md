# ColorPicker 参考卡

来源：Arc `https://uiarc.dev/components/color-picker`（Registry `https://uiarc.dev/r/color-picker.json`），2026-10-31 冻结于 `tests/Baselines/References/2026-10-31-color-picker/`（manifest.json 记录 SHA-256 与许可，许可见 `license.html`）。证据等级：源码与 CSS 直接读取（A）；主示例内容取自页面懒加载 chunk（A）；真实浏览器状态截图未采集（用户已确认不作为前置条件）。

- `registry/components/color-picker/color-picker.tsx` SHA-256 `125f022e7abc7abb…`
- `registry/components/color-picker/color-picker.module.css` SHA-256 `c5fd30e3c4c63819…`
- `registry/components/text-morph/text-morph.tsx` SHA-256 `24b6559369ab4e5c…`
- `registry/components/text-morph/text-morph.module.css` SHA-256 `bd51e5ba739e7b72…`

## 还原要点

- 触发器：44 高胶囊（内边距 6 16 6 6、圆角 22、边框 Border、静止阴影），32 棋盘格色块 + 名称 xs secondary + 十六进制 14/500。
- 面板：19rem（304）宽，从触发器盒子按裁剪框长成面板（open：morph .42 + bounce .1；close：physical .3），内容在进度 .35–.9 淡入并从 -10 落位；Escape/外部点击关闭；Done（Check）按钮。
- 选色：164 高、圆角 16 饱和度/亮度区域（色相 + 白/黑渐变）；22 拇指、3 像素白环，激活放大 1.2，跟随弹簧 .26（拖动 .1）；色相/透明度滑块 14 高，渐变停点对齐拇指行程端点。
- 输入：40 高、圆角 14 SurfaceMuted，格式按钮 Hex/RGB/HSL/OKLCH 循环，非法输入 [0,-5,4,-2,0] .32 抖动 + “Enter a hex, RGB, HSL, or OKLCH color”。
- 对比度：Aa 样例 36×28/圆角 9，比值弹簧跟随，“against background”，等级胶囊 AAA/AA/AA large/Fails（Contrast* 底色）。
- 色板：30 虚线添加按钮（Plus），已存色板 26 色块，当前色 2 像素环 + .78 缩放；Alt+方向键/拖动排序，Delete 删除，最多 7 个；scale .4 弹入。
- 主示例（chunk 63040）：最小高 460、19rem 锚点，Accent #2F6BFF，预设 cobalt/tomato/moss/saffron/plum。
