# 第六批复刻验证：FloatingButtonGroup / ExpandingButtonGroup / CardStack / LiquidTabBar / CoverFlow

日期：2026-11-03
状态：五项均为**待验收**。

## 验证环境

Linux + Wine 9 + Xvfb，离屏渲染，字体为改名的 Liberation（无 Medium 字重，字宽与 Arc 不同）。结果不等于真实窗口验收。

## 构建与测试

- `tests/ArcWpf.Tests`、`samples/ArcWpf.Gallery` 构建：0 错误。
- 只运行本批测试（按用户要求）：`FloatingButtonGroupTests`、`ExpandingButtonGroupTests`、`CardStackTests`、`LiquidTabBarTests`、`CoverFlowTests`：49 项通过，0 项失败。
- 完整测试套件：未运行。

## 像素对比

Arc 为 uiarc.dev 公开页面截图，WPF 为 Gallery 离屏渲染。对比图和数据在 `artifacts/batch6-2026-11-03/`（未入库）。未做平移对齐和遮罩，表中是差异 >32 的像素比例。

| 状态 | 差异 >32 |
|---|---:|
| fbg-stage | 0.91% |
| fbg-hover | 0.93% |
| fbg-present | 0.99% |
| ebg-rest | 3.89% |
| ebg-forward | 3.93% |
| ebg-delete | 3.47% |
| cs-rest | 5.77% |
| cs-drag | 7.56% |
| cs-empty | 2.62% |
| ltb-inbox | 2.12% |
| ltb-all | 2.83% |
| ltb-icons | 1.77% |

差异主要来自替身字体的字宽和字重，数字不作通过结论。cs-drag 的拖动是一步到位，Arc 截图是分 6 步移动，两者姿态不保证完全相同。

CoverFlow 不计差异率：Gallery 用原创绘制的图，与 Arc 的照片不可比。几何靠 `ProjectedBoxesMatchTheBrowsersRects` 对照浏览器矩形（六组，容差 .05），外观靠并排目视。

## 对比中发现并修复

- 四个新增图标（reply、bookmark-check、pencil-line、sun）的路径拼接错误，子路径的相对 `m` 接到了前一段终点上，图标变形。
- 文字固定行高等于字号时偏上 1–2px；FBG、EBG、CardStack 按钮改用自然行高。
- CardStack 主按钮同色描边留下浅色接缝；空状态按钮应为高 36；Gallery 只有 4 张卡，Arc 是 6 张。
- CoverFlow：`SelectedIndex` 在项目到达前被钳成 0；倒影渐变被拉伸到整张镜像；距离 ≥3 的卡片应隐藏，z 和遮罩应封顶；缺卡面内描边。
- CoverFlow 从 Viewport3D 改为软件透视重采样，原因见[参考卡](../references/components/CoverFlow.md)。

## 未运行/待验证

- 真实 Windows 窗口：未运行，本环境没有。动效手感、Inter/Segoe 下的真实字宽字重都依赖它。
- 动效未与 Arc 逐帧比对；LiquidTabBar、CoverFlow 的时序参数是拟合或推测。Xvfb 下渲染帧稀疏，动画中途的画面不可信。
- 真实指针拖动、触控：未运行，只用测试钩子驱动。
- CoverFlow 帧耗时：未测量，高 DPI 下需关注。多 DPI 与暗色主题重新烘焙未渲染验证。
- CoverFlow 拖动中画面（cf-drag）已渲染，未与 Arc 同参数对比；其他舞台宽度未核对。
- 暗色主题：五个控件均未做渲染对比。

## 许可

LiquidTabBar、CoverFlow 是 Arc Pro 组件，开源分发前需要项目负责人决定，见 `THIRD-PARTY-NOTICES.md`。
