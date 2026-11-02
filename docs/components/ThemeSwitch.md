# ThemeSwitch

状态：已实现，专项 2/2 通过；真实窗口主题、DPI 和动效观感待验收。

来源：Arc 免费组件 [ThemeSwitch](https://uiarc.dev/components/theme-switch)，冻结批次 `tests/Baselines/References/2026-11-02-theme-switch/`。WPF 使用 `ThemeSwitchTheme`、`ThemeSwitchTheme` 和 `ThemeChanged` 事件；页面主题由宿主切换，控件只报告目标主题。

公开契约：`Theme`（Light/Dark）、`Variant`（Reveal/Eclipse/Split/Rise）、`Label`、`IconOnly`。Enter/Space 调用切换，`aria-pressed` 对应深色状态；Reduced/Off 取消图标过渡。WPF 与 Web 的差异是没有浏览器 View Transition，由宿主决定整页过渡。
