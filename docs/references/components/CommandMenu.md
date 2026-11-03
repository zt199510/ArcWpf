# CommandMenu 参考卡

- 页面：[https://uiarc.dev/components/blocks/command-palette](https://uiarc.dev/components/blocks/command-palette)
- 来源批次：`tests/Baselines/References/2026-11-03-command-palette`（Arc Free，MIT）
- 复用方式：依据 Registry 源码改写为 WPF，不包含 React/Motion 运行时代码。
- TKCode.Arc：提交 `5680bf55414fff07398a9fecadc0b72c900fdaba`（MIT），仅作移植经验参考，未复制代码。

## 事实

- 源码确认：搜索打分与词边界、分组、键盘导航、清除按钮进退动画、480px/340px 断点（按视口宽度）。
- 运行观察：面板 440×398，搜索行 54，列表上限 342，空状态列表高 147.39。默认截图无滚动条；关闭 `--hide-scrollbars` 时 Arc 显示浏览器滚动条且不占宽度。
- WPF适配：对应 Arc 的 `command-palette` 块，内联渲染；断点取所在 Window 的宽度；滚动条用 `OverlayScrollViewer`。
- 待核对：Arc 实际使用的 Lucide 版本。
