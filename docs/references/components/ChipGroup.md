# ChipGroup 参考卡

来源：Arc `https://uiarc.dev/components/chip-group`（Registry `https://uiarc.dev/r/chip-group.json`），2026-10-31 冻结于 `tests/Baselines/References/2026-10-31-chip-group/`（manifest.json 记录 SHA-256 与许可，许可见 `license.html`）。证据等级：源码与 CSS 直接读取（A）；主示例内容取自页面懒加载 chunk（A）；真实浏览器状态截图未采集（用户已确认不作为前置条件）。

- `registry/components/chip-group/chip-group.tsx` SHA-256 `18bd79ee819c6546…`
- `registry/components/chip-group/chip-group.module.css` SHA-256 `cc8c6c63756b0dee…`

## 还原要点

- 外形：胶囊间距 8；选中边框/底色 ChipSelected*，悬停 ChipSelectedHover*；左侧 18 槽位绘制对勾（描边 2.5，路径长 23.35），按下 .97。
- 行为：多选/单选（Multiple），MaxVisible 折叠为“+N more”，展开后按阅读顺序交错（.035，上限 .3）；选中项在折叠时保持可见；方向键/Home/End 环绕，漫游 Tab 停留。
- 动效：表面颜色 morph 弹簧滞后，对勾缩放/透明/模糊，离开标签淡出，其余 FLIP 滑动，容器高度 morph。
- 主示例（chunk 87053）：36rem 宽 Topics（11 项，初始 Motion，MaxVisible 6），24 条资源前 3 条 + “+N more”，空态“No topic selected”，底部 Match 分段控件 + Clear filters。
