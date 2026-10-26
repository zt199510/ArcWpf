# Slider 参考卡

证据：2026-10-26项目采集批次（非官方版本），[manifest](../../../tests/Baselines/References/2026-10-26-slider/manifest.json)。Arc [文档](https://uiarc.dev/components/slider/markdown)、免费Registry、CSS、Foundation、Motion、静态页面与许可页已冻结。WPF Slider.cs参考dotnet/wpf v10.0.0（MIT）。没有操作网站；静态HTML不证明运行中视觉。

## 来源与布局映射

- 源码确认：整体grid，header的label/readout同基线、gap16；body前后装饰可选，间距12；control命中行44高。轨道6高、两端内缩11，thumb22圆形，因此端点仍在控件盒内。标签不计入44高。
- 源码确认：轨道control-track，填充control-fill；暗色neutral填充62% OKLCH，避免白thumb与填充混同；thumb白色/暗色97%，三层阴影。WPF新加ControlFill和SliderThumb阴影令牌，只由Slider使用；三层影简化为单DropShadowEffect加半像素边框。
- 源码确认：bubble位于thumb顶端上8，minheight28，左右12，pill，前景/背景反色；相对整个控制行夹紧，不计入布局高度；拖动或键盘焦点显示，释放后700ms保留。
- 源码确认：marks内部4圆点；label字号12，端点文字不越界，活动标签与hover颜色160ms。WPF用原生Button供点击标记，装饰性读数不重复暴露UIA。
- 静态页面确认：单滑块例Volume，60%，0..100，0/25/50/75/100百分比marks，前部静音按钮。文档Usage另为双滑块Price；本次明确单滑块，采用Volume作为对照主例。Gallery宽400为项目展示约束，不冒充原站容器。

## 动效合同

| 触发 | 来源参数 | WPF映射 |
|---|---|---|
| 外部值/键盘/点击轨道 | snappy .26，物理omega=2π/(duration×1.2)，临界阻尼 | 内部位置spring保留当前速度，值本身即时更新 |
| 拖动 | 指针1:1，步长仅影响公开值，超界rubber最多9px | 鼠标捕获，原始位置与已提交值分离；填充按可见位置同帧夹紧 |
| 释放 | 6样本、末样本60ms内，速度×.099投射；step像素≥12才最多带一格；回弹保速 | 相同阈值/算法意图，WPF Dispatcher rendering实际时间推进 |
| 边界按键 | morph .42，注入150px/s速度，归位 | 内部位置strain，不改变Value |
| thumb拖动 | scale1→1.16 snappy .26 | 私有ScaleTransform，退出回1 |
| bubble | .6/y6/blur2→1/y0/blur0，.26；opacity/filter160；退出.8/y4/blur2/120 | 独立overlay，退出完成后隐藏，重入打断 |
| 数字轮 | 0..9循环，按值变化方向滚动；y=offset×1.05em，opacity按距离，blur最多3 | 按数位key保留wheel，物理spring；前后缀独立 |
| 数位增删 | 宽/scale/opacity morph .42进入，y .26，blur4→0/240；退出宽scale .4、y160、opacity/blur120 | 独立槽动画，保留退出槽，最终移除 |
| thumb阴影/mark文字 | CSS160ms | 私有阴影/文字画刷，主题与hover可刷新 |

Reduced：位置即时、无动量/strain/数字滚动/scale/blur；bubble/新字符短淡化120。Off全部即时。WPF颜色/阴影与CSS oklab/多影不是像素等效；slot几何过渡沿用项目采样曲线，位置与数字轮采用可保速的解析临界阻尼。无帧率承诺。
