# CookieConsent 参考卡

- 页面：[Arc Cookie consent](https://uiarc.dev/components/cookie-consent)
- Registry：[cookie-consent.json](https://uiarc.dev/r/cookie-consent.json)
- 采集批次：`tests/Baselines/References/2026-11-03-cookie-consent`
- 复用方式：行为和结构参考；未复制 React/Motion 运行时代码。

来源确认三种模式、四类 cookie、必需项 Always on、Accept/Reject/Save、Escape 关闭、重新打开和版本化存储。WPF 差异见 `docs/components/CookieConsent.md`。

TKCode.Arc 参考：提交 `5680bf55414fff07398a9fecadc0b72c900fdaba`（2026-11-03，MIT），Avalonia 实现位于 `src/TKCode.Arc/Messages/CookieConsent/`。只用其测试清单核对行为覆盖，未复制代码；与 Arc 不同的扩展不视为要求。证据等级：参数来自冻结的 Arc Registry 源码，几何与颜色来自 uiarc.dev 实测。
