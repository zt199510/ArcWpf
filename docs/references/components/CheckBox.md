# CheckBox 参考基准

批次：2026-10-25-checkbox（项目采集批次，非官方版本）。[快照与哈希](../../../tests/Baselines/References/2026-10-25-checkbox/manifest.json)。[原页](https://uiarc.dev/components/checkbox)。Registry meta.tier=free；许可页确认免费组件MIT；原MIT声明保留在初始Arc快照中。

| 项目 | 已确认依据 | WPF采用方式 |
|---|---|---|
| 主样本 | page.html：Include archived projects；Show projects that are no longer active.；checked=true | 同文案、默认选中；动态状态提示仅在交互后显示 |
| 方框 | checkbox.module.css：18×18，描边1，圆角5 | 固定DIP几何，不缩放文字和点击区 |
| 点击区与内容 | 44×44；copy左间距1，上偏移(44−19.6)/2=12.2；label14/500/1.4，description12/1.4；gap2 | 第一行19.6、说明16.8，文本起点x45，多行换行 |
| 路径 | TSX check M4.25 9.25 L7.25 12.25 L13.75 5.75；dash M4.75 9 L9 9 L13.25 9；stroke1.75圆端/圆接 | 同坐标三点插值，按弧长逐段绘制；保持18×18坐标不按包围盒拉伸 |
| 颜色 | surface、border-strong、control-on、control-glyph；hover未选border=text-muted | 复用主题，补ControlGlyph白/暗色.19；现有ControlThumbOn数值相同，但新增独立语义键，允许强调色覆盖而不耦合Switch滑块 |
| 禁用 | box透明度.5；文字不降透明度 | 仅44×44区降低透明度；整控件禁止操作 |
| 动效 | fill scale.6→1及pathLength0→1 snappy.26；check↔dash morph.42；opacity选中120ms/取消160ms | 临界阻尼适配，保当前值/速度；无Web求解器相同时间保证 |
| 按压 | 仅方框scale.95，120ms standard；释放580ms CSS spring | 方框独立变换；不缩放内容 |
| Reduced | 所有变化即时、无按压缩放 | Reduced与Off立即收敛 |
| 展示容器 | preview最大620、grid gap16；surface居中；status min18 | Gallery沿用现有背景容器；不是网页外壳逐像素复制 |

资料为静态源码/HTML/CSS证据，没有实际操作或截图。共享运动参数与当前批次一起冻结；颜色采用现有项目语义资源。TKCode未作为本次实现依据。

