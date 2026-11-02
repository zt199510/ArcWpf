# EmojiPicker

状态：实现中，待真实窗口视觉验收。

来源：Arc Pro 页面公开资料；Registry 返回 404，无 Pro 源码复用。默认目录由 Unicode `emoji-test.txt` 15.0（[Unicode Terms of Use](https://www.unicode.org/terms_of_use.html)，冻结副本见 `tests/Baselines/References/2026-11-02-emoji-picker/emoji-test-15.0.txt`）生成。当前为独立 WPF 实现，提供名称/英文别名搜索、Popup 网格、可绑定 emoji 集合、最近使用列表、分类筛选、六档肤色选择、键盘可达按钮、上下碰撞定位和 `EmojiSelected`/`SkinToneChanged` 事件。虚拟化、滚动同步分类指示器、完整肤色组合和本地持久化仍是与 Arc Pro 的差异。
