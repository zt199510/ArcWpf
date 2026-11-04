# 2026-11-03 第八批复刻验证

状态：**待验收**。范围：ScrubInput、ElasticSlider、RulerSlider、Sparkline、Gauge。

## 环境

Linux + Wine + Xvfb 离屏渲染，字体为 Liberation 改名替身（字宽、字重、纵向 1px 差异属环境差异），不是真实 Windows 窗口。

## 构建与测试

- `dotnet build` 库、Gallery、测试项目：0 错误。
- 专项测试：`--TestCaseFilter` 只选 `GaugeTests|SparklineTests|ElasticSliderTests|RulerSliderTests|ScrubInputTests`，**64 通过，0 失败**。
- 全量测试：**未运行**（按用户要求只测本次编辑的控件）。

## 像素对比（差值 >32 的像素占比）

| 状态 | 占比 | 状态 | 占比 |
|---|---|---|---|
| gauge | 4.26% | elastic-slider | 3.08% |
| g-high | 4.87% | es-drag | 3.52% |
| g-mid | 3.90% | es-over | 3.74% |
| g-low | 2.81% | es-max | 4.96%（含 Arc 焦点环） |
| sparkline | 2.64% | es-key | 3.90% |
| sp-1 | 4.39%（含 Arc 焦点环） | ruler-slider | 1.80% |
| sp-2 | 4.83%（含 Arc 焦点环） | rs-drag | 3.71%（含 Arc 焦点环） |
| sp-scrub | 2.19% | rs-after | 3.07%（含 Arc 焦点环） |
| scrub-input（遮蔽插图） | 0.88% | rs-end | 2.98%（含 Arc 焦点环） |
| si-invalid（遮蔽插图） | 1.46% | si-unit（遮蔽插图） | 2.03% |
| si-rot（遮蔽插图） | 1.99% | | |

si-invalid、si-unit、si-rot 的操作序列与 Arc 探测不完全相同（X、W/H 值不同），占比只作布局参考。对比图在 `artifacts/batch8-2026-11-03/`（不入库）。

## 动画中间帧

Wine 下定时等待严重超时（30ms 等待实测数百毫秒以上），实时采样抓不到真实时序，因此**未取得可信的实时中间帧**。弹簧和惯性轨迹由专项测试按固定步长驱动验证：回弹穿过静止位、甩动落点、拉伸上限、游标落点、数字列开合。

## 对比中发现并修复

- Gauge 读数方块把说明行顶低 17.6 DIP：改为叠放不占高度。
- ElasticSlider 无图标时仍保留图标边距，轨道窄 24 DIP（专项测试发现）。
- RulerSlider 刻度差 1px；溢出内容使边缘淡出范围变形：取整改为向上半舍入，绘制裁剪到视口。
- 示例页说明行、Reset frame、尺寸胶囊间距修正到 Arc 几何。

## 未运行/待验证

- 真实 Windows 窗口、触控、高 DPI、性能：未运行，环境没有。
- 帧率：Wine 下无意义，未给数字。Wine 下曾观察到动画结束后 CPU 持续偏高（10 次 30ms 等待：空窗口约 0.6s / CPU 1s，Gauge 约 7s / CPU 12s，关窗后不降）。排查结果：Gauge 落定后逐帧循环已停止；不含任何项目代码的探针（一个 Rectangle，在 `CompositionTarget.Rendering` 中改 40 次宽度后退订）同样出现（5.1s / CPU 9.4s），用 `DoubleAnimation` 改宽度则不出现（0.6s / CPU 0.8s）。因此判断为 Wine 下逐帧改动可见内容后的环境现象，不是这批控件的循环泄漏；Windows 上是否存在待真实窗口确认。
- ScrubInput 指针锁定替身光标、横向滚轮消息、减弱动效分支：未实际运行。
- Gauge 阈值边界 30/70：待核对。
- Arc 鼠标按下时的焦点环：WPF 只在键盘时显示，见各规格已知差异。
