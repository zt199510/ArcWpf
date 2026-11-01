# PasswordStrength 参考卡

来源：Arc `https://uiarc.dev/components/password-strength`（Registry `https://uiarc.dev/r/password-strength.json`），2026-10-31 冻结于 `tests/Baselines/References/2026-10-31-password-strength/`（manifest.json 记录 SHA-256 与许可，许可见 `license.html`）。证据等级：源码与 CSS 直接读取（A）；主示例内容取自页面懒加载 chunk（A）；真实浏览器状态截图未采集（用户已确认不作为前置条件）。

- `registry/components/password-strength/password-strength.tsx` SHA-256 `8271b05ab8102aaf…`
- `registry/components/password-strength/password-strength.module.css` SHA-256 `57080bc46186fa86…`

## 还原要点

- 外形：标签 14/500，下距 8；外壳 44 高、圆角 18、内边距 0 5 0 16，边框 BorderStrong（悬停 TextMuted、聚焦 Foreground、错误 Danger，.16 standard）；显示/隐藏按钮 34×34、圆角 13。
- 强度：四段 4 高胶囊、间距 5，填充在各自轨道内从 -101% 滑入（smooth 弹簧，点亮逐段延迟 .05，熄灭 .03）；色调 1=Danger、2=Warning、3–4=Success；右侧 72 宽强度词按升降方向滚动（.3em，模糊 4，.24 enter；退出 .16）。
- 规则：默认长度≥12（剩余“N more”逐位翻动）、大小写、数字、符号；对勾圈 16，描边 1.25，底色 PasswordRuleFill 从 .5 缩放弹入（snappy），勾线 .24 enter 绘制（延迟 +.06），多条变化按 .05 交错。
- 评分：空=0；码点数 < 8 → 1 “Too short”；否则 round(满足数/总数×4) 夹到 1..4 → Weak/Fair/Good/Strong。
- 错误：新错误出现时外壳以速度 -240、刚度 900、阻尼 57 的弹簧侧向抖动一次；错误行高度 smooth 弹簧展开。
- 主示例（chunk 67556）：360 宽表单，“New password”；Suggest one 循环三条口令并以 42 ms/字输入（减弱动效时直接填入）；Save（ActionButton，900 ms）在强度 < Good 时拦截并给出三种错误文案。
