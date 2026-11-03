# OnboardingChecklist 参考卡

- 页面：[https://uiarc.dev/components/onboarding-checklist](https://uiarc.dev/components/onboarding-checklist)
- 来源批次：`tests/Baselines/References/2026-11-03-onboarding-checklist`（Arc Free，MIT）
- 复用方式：依据 Registry 源码改写为 WPF，不包含 React/Motion 运行时代码。
- TKCode.Arc：提交 `5680bf55414fff07398a9fecadc0b72c900fdaba`（MIT），仅作移植经验参考，未复制代码。

## 事实

- 源码确认：状态流转、720ms 停留后折叠（SETTLE_MS）、面板交给下一未完成项、完成行展开、错误显示、环形进度与数字滚动。
- 运行观察：默认 412.6 高，完成态 193 高，隐藏态 130 高。
- WPF适配：折叠用 `SpringDriver`，弹簧 bounce 0。
- 待核对：Arc 实际使用的 Lucide 版本。
