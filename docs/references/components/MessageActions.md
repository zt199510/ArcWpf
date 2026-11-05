# MessageActions 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/response-feedback、https://uiarc.dev/components/blocks/ai-chat（均为 Arc Pro，无公开版本号），2026-11-04 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。参考文件在 `work/refs/batch12/`、`work/refs/batch11/` 不入库。
- SHA-256：response-feedback `markdown.txt` 016fcb334ee3d0b22deea9ac0ffc3c91214b9ec7318d2bcef9a438f67c38e759，`page.html` 910576f24cdc5e63f4f743b7a7678e1f0a94c696aa13b6d3576e1a0b22fddb94；ai-chat `markdown.txt` 3765ba0f7ec252d0731453a3851078ed40f8113d533fd6a6d276c211e3944ab3，`page.html` 9d6be1a7f12688a20ac2144f60ba8678fb4eba0c9d6ceb85ac08b9f66b76620a。
- 批次：2026-11-04 第十二批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 操作按钮 | 36×36，全圆角，间距 2，图标 16 | 运行观察 | 一致 |
| 拇指 | 实心图形 160ms 淡入叠加，按下 .94 | 文档描述 | 一致 |
| 面板 | 圆角 20，内边距 12；原因 28 高胶囊；备注 72 高；Submit 选中后可用 | 运行观察 | 一致 |
| 感谢 | 对勾 280ms 描线，默认停留 1600ms 后折叠 | 文档描述 | 一致 |
| 停止、引用 | Arc 行内没有这两项 | 待核对 | 用户要求的扩展，WPF 适配 |

## WPF差异和回归入口

差异见[组件规格](../../components/MessageActions.md)。回归：`tests/ArcWpf.Tests/MessageActionsTests.cs`，Gallery 页 `MessageActionsPage`。
