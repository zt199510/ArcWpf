# Rating 参考卡

来源：Arc `https://uiarc.dev/components/rating`（Registry `https://uiarc.dev/r/rating.json`），2026-10-31 冻结于 `tests/Baselines/References/2026-10-31-rating/`（manifest.json 记录 SHA-256 与许可，许可见 `license.html`）。证据等级：源码与 CSS 直接读取（A）；主示例内容取自页面懒加载 chunk（A）；真实浏览器状态截图未采集（用户已确认不作为前置条件）。

- `registry/components/rating/rating.tsx` SHA-256 `d55bd061628a4e80…`
- `registry/components/rating/rating.module.css` SHA-256 `f62d971f0e4a5fc9…`

## 还原要点

- 外形：星形 lucide Star，基础/保留（RatingKept）/填充三层，标签 500 下距 6，数值文字右侧 8，描述 12 muted。
- 交互：悬停预览、点击提交、再次点击同值清除（AllowClear）、按住拖动刷选、半星（AllowHalf）；方向键/Home/End/数字/Backspace/Delete。
- 动效：填充按扫掠交错 snappy 弹簧，按下 .86 下压，提交时波浪式弹跳（间隔 .26 s），数值文字按方向滚动。
- 主示例（chunk 14932）：380 宽卡片，Emma Collins 交接 + “How was the handoff?”，240 ms 后按评分显示感谢或备注视图。
