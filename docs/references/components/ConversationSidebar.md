# ConversationSidebar 参考卡

## 来源与固定范围

- **Arc 没有同名组件**。视觉取自 https://uiarc.dev/components/blocks/sidebar 的 workspace 变体（Arc Pro）和 ai-chat 历史菜单（Arc Pro）的公开显示结果；行为由用户需求定义。2026-11-04 采集，未获取 Pro 源码。
- SHA-256：sidebar `markdown.txt` fbd4d67e715b840ccc453963e369d281e4a84fefd7f93fb072fb5ea154e067d6，`page.html` 7a0a400e3e673201666c815afd2a2a4197acbf0ddba42a479d31ae171ba8499c；ai-chat `markdown.txt` 3765ba0f7ec252d0731453a3851078ed40f8113d533fd6a6d276c211e3944ab3，`page.html` 9d6be1a7f12688a20ac2144f60ba8678fb4eba0c9d6ceb85ac08b9f66b76620a。
- 批次：2026-11-04 第十二批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 宽度 | 272（Gallery 示例）；内容宽 246 | 运行观察 | 一致 |
| 行 | 高 32，圆角 8，14 字号；选中行加深 | 运行观察 | 一致 |
| 分组标题 | 12 字号、中等字重、弱色 | 运行观察 | 一致 |
| 搜索框 | 高 32，图标 16 | 运行观察 | 一致 |
| 重命名、归档、恢复 | 未找到对应的 Arc 同名组件行为 | WPF适配 | 组合设计，无原版可比 |

## WPF差异和回归入口

差异见[组件规格](../../components/ConversationSidebar.md)。回归：`tests/ArcWpf.Tests/ConversationSidebarTests.cs`，Gallery 页 `ConversationSidebarPage`。
