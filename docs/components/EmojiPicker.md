# EmojiPicker

状态：WPF Popup、分类/肤色/搜索、Popular 默认项、稳定的 `😀` 触发入口、COLR 彩色字形绘制（默认 Segoe UI Emoji，可由 `EmojiFontFamily` 替换）、可配置列数、键盘导航和有限滚动层已完成，已完成构建与 Gallery 启动检查，彩色绘制只用 Twemoji 离屏验证过（见 [验证记录](../verification/2026-11-02-next-controls-pixel.md)）；Segoe UI Emoji 实际效果、真实窗口碰撞、DPI 和主题切换仍待人工验收。

像素对照：2026-11-02 已按线上页面重做，结果与已知差异见 [记录](../verification/2026-11-02-next-controls-pixel.md)；离屏对比已完成，真实窗口未验证。

来源：Arc Pro 页面公开资料；Registry 返回 404，无 Pro 源码复用。默认目录由 Unicode `emoji-test.txt` 15.0（[Unicode Terms of Use](https://www.unicode.org/terms_of_use.html)，冻结副本见 `tests/Baselines/References/2026-11-02-emoji-picker/emoji-test-15.0.txt`）生成，排列顺序和分组按公开页面显示调整。彩色字形由 `ColorEmojiFont.cs` 自行解析字体表绘制，未复用外部代码。当前为独立 WPF 实现，提供名称/英文别名搜索、Popup 网格、可绑定 emoji 集合、Popular 默认项、可配置 6–12 列、最近使用列表、分类筛选、六档肤色选择、键盘可达按钮、有限高度滚动、上下碰撞定位和 `EmojiSelected`/`SkinToneChanged` 事件。完整肤色组合和本地持久化仍是与 Arc Pro 的差异；模板原生按钮已显式使用 WPF 类型，避免 ArcWpf 同名控件遮蔽交互。
