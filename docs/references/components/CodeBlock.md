# CodeBlock 参考卡

Arc 页面：`https://uiarc.dev/components/code-block`。2026-11-02 冻结 Registry、CSS、Markdown、页面、动效令牌和许可证；文件及 SHA-256 见 `tests/Baselines/References/2026-11-02-code-block/manifest.json`。组件为 Free/MIT。

源码确认：公开 API 只有 `code`、`filename`、`language`（默认 `tsx`）和 `maxLines`。组件外壳为全宽、最大宽度受父级约束的表面；头部最小高 52px，左侧文件图标/文件名/语言标签，右侧复用 CopyButton。代码区域使用等宽字体、`white-space: pre`，在块内部横向滚动；`maxLines` 时底部显示 Show all/Show fewer toggle。

高亮是轻量正则 token 化，不依赖完整语言解析器。来源角色包括 comment、string、number、keyword、type、function、property、tag、punctuation；语言只影响少数 token 判断。文件名和折叠文案使用替换动效，代码内容更新淡入上移，折叠高度使用 smooth spring，Chevron 旋转；Reduced 直接落点。

键盘：Tab 依次可到复制按钮、源码区域和展开按钮；源码区域可获得焦点并保留选择/复制，Enter/Space 触发按钮。小于 420px 时头部左右留白、代码字号和复制按钮形态收紧，复制按钮变为图标优先。

WPF 适配：`RichTextBox` 只读承载可选择源码，行号作为同一 FlowDocument 行内前缀随横向滚动；复制按钮复用 ArcWpf `CopyButton`。WPF 的文本选择复制与来源源码复制分开：CopyButton 始终复制纯 `Code`，用户在源码区选择时保留原生编辑器复制语义。代码高亮仅是视觉 token，不改变文案或可访问文本。
