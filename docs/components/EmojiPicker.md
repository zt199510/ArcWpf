# EmojiPicker

状态：WPF Popup、分类/肤色/搜索、Popular 默认项、可配置列数、键盘导航和有限滚动层已完成，已完成构建与 Gallery 启动检查；真实窗口碰撞、DPI 和主题切换仍待人工验收。

来源：Arc Pro 页面公开资料；Registry 返回 404，无 Pro 源码复用。默认目录由 Unicode `emoji-test.txt` 15.0（[Unicode Terms of Use](https://www.unicode.org/terms_of_use.html)，冻结副本见 `tests/Baselines/References/2026-11-02-emoji-picker/emoji-test-15.0.txt`）生成。当前为独立 WPF 实现，提供名称/英文别名搜索、Popup 网格、可绑定 emoji 集合、Popular 默认项、可配置 6–12 列、最近使用列表、分类筛选、六档肤色选择、键盘可达按钮、有限高度滚动、上下碰撞定位和 `EmojiSelected`/`SkinToneChanged` 事件。虚拟化、滚动同步分类指示器、完整肤色组合和本地持久化仍是与 Arc Pro 的差异；模板原生按钮已显式使用 WPF 类型，避免 ArcWpf 同名控件遮蔽交互。
