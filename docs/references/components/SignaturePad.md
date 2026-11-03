# SignaturePad 参考卡

- 页面：[https://uiarc.dev/components/signature-pad](https://uiarc.dev/components/signature-pad)
- 来源批次：`tests/Baselines/References/2026-11-03-signature-pad`（Arc Free，MIT）
- 复用方式：依据 Registry 源码改写为 WPF，不包含 React/Motion 运行时代码。
- TKCode.Arc：提交 `5680bf55414fff07398a9fecadc0b72c900fdaba`（MIT），仅作移植经验参考，未复制代码。

## 事实

- 源码确认：笔迹轮廓算法、墨色与线宽、回放、擦除、导出 SVG/PNG。
- 运行观察：用 Arc 页面实际绘制一笔，`StrokePath` 输出的路径字符串与 Arc 完全相等（测试 `DrawnStrokePathEqualsArcOutput`）。鼠标绘制后 pad 匹配 `:focus-visible`，轮廓 2px、偏移 2px、oklab(.24 0 0 / .72)。
- WPF适配：导出默认走 `SaveFileDialog`，可由 `Exporting` 事件接管。
- 待核对：Arc 实际使用的 Lucide 版本。
