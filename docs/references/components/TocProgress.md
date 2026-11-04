# TocProgress 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/toc-progress（Arc Pro，Registry 返回 404，无公开版本号），2026-11-03 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。
- 参考文件不入库，放在 `work/refs/batch9/toc-progress/`。SHA-256：`markdown.txt` 0ff3cb84b1b4dec49001b27ad1bfa379ac6b68d2e3d92751188246e1e3c38fce；`page.html` da61ec4ec5fb64e8df3d007e0b0548e1064f53209cc344ee9fa3be02d4fddd2b。
- 批次：2026-11-03 第九批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 几何 | 缩进轨道 5+12(l-1)，转弯 ±7，轨道 1.5，读过线 2，圆点 r3.5，镂空 r5.5 | 运行观察 | 一致 |
| 主示例 | 目录卡 521.6，圆角 26；文章卡高 420 | 运行观察 | 一致；文章正文为本项目原创 |
| 剩余时间 | ceil(R×(1-进度))；仅到底部才显示 Finished | 文档描述与运行观察 | 边界有偏差，待核对（Arc 在 1600 高度下 600/900/1200/1400/1500 处跳变） |

## WPF差异和回归入口

差异见[组件规格](../../components/TocProgress.md)。回归：`tests/ArcWpf.Tests/TocProgressTests.cs`，Gallery 页 `TocProgressPage`。
