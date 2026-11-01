# TagInput 参考卡

来源：Arc `https://uiarc.dev/components/tag-input`（Registry `https://uiarc.dev/r/tag-input.json`），2026-10-31 冻结于 `tests/Baselines/References/2026-10-31-tag-input/`（manifest.json 记录 SHA-256 与许可，许可见 `license.html`）。证据等级：源码与 CSS 直接读取（A）；主示例内容取自页面懒加载 chunk（A）；真实浏览器状态截图未采集（用户已确认不作为前置条件）。

- `registry/components/tag-input/tag-input.tsx` SHA-256 `c17d9903f365a290…`
- `registry/components/tag-input/tag-input.module.css` SHA-256 `d6b84d67e6ff5626…`

## 还原要点

- 外形：外壳圆角 18，边框 Border（悬停 BorderStrong、聚焦 Foreground），内容区最小 42、内边距 7；标签胶囊带 22/圆角 11 的删除按钮（X 14，按下 .96）。
- 交互：Enter/逗号添加，重复（忽略大小写）时已有标签 1.06 脉冲；Backspace 两次删除最后一项（第一次选中）；左右键在标签间移动；失焦添加草稿。
- 动效：新标签 scale .9 + 模糊 4 以 morph 弹入；离开淡出；其余标签 FLIP 滑动；外壳高度随换行弹簧变化；选中环（Accent/AccentSubtle）在标签间 morph 滑动。
- 主示例：420 宽，“Project tags”，[release, web]，占位“Add a tag”，描述“Press Enter to add a tag. Press Backspace twice to remove the last one.”
