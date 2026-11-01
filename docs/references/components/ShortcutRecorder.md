# ShortcutRecorder 参考卡

来源：Arc `https://uiarc.dev/components/shortcut-recorder`（Registry `https://uiarc.dev/r/shortcut-recorder.json`），2026-10-31 冻结于 `tests/Baselines/References/2026-10-31-shortcut-recorder/`（manifest.json 记录 SHA-256 与许可，许可见 `license.html`）。证据等级：源码与 CSS 直接读取（A）；主示例内容取自页面懒加载 chunk（A）；真实浏览器状态截图未采集（用户已确认不作为前置条件）。

- `registry/components/shortcut-recorder/shortcut-recorder.tsx` SHA-256 `e9941d875579297a…`
- `registry/components/shortcut-recorder/shortcut-recorder.module.css` SHA-256 `a658a5ccd0d5c3d3…`

## 还原要点

- 模型：“mod+shift+k” 字符串，mod 在 Mac 为 ⌘、其他平台为 Ctrl；物理键识别、规范化顺序 mod/ctrl/alt/shift/meta；键帽 Mac ⌃⌥⇧⌘，其他 Ctrl/Alt/Shift/Win；系统保留组合 12 项。
- 键帽：26 高、最小 26、圆角 8、底边 2、SurfaceRaised、xs 500；按下下沉 1 像素、底边 1、KbdPressedBorder/AccentSubtle/AccentStrong（90 ms）；冲突 KbdWarning*；小号 22/7。
- 录制器：44 高、圆角 18；录制时 Accent 边框 + RecorderSurface、7 像素呼吸点（1.4 s）；键帽 scale .72、y 4、模糊 2 snappy 弹入，录制完成交错 .035，结果亮起 700 ms；重置（RotateCcw）/清除（X）32、圆角 13，scale .6 弹入。
- 消息：“Esc to cancel, Backspace to clear”；缺修饰键提示“Include Ctrl or Alt”/“Include ⌘, ⌃, or ⌥”；冲突“Already used by / Reserved for <名称>”+ Use anyway / Cancel。
- ShortcutList：36 高搜索行（计数、清除），≥520 宽两列分组；行 36 高、分隔细线，按住修饰键时不相关行降到 .38，组合全部按下的行点亮 AccentSubtle。
- 主示例（chunk 72304）：300 | 1fr 面板；Shortcuts + Key style（Mac/Windows），三个可编辑录制器（Open command menu mod+k、Create issue alt+n、Toggle sidebar mod+.），运行提示行，右侧 Workspace/Navigation/Editing 速查表。
