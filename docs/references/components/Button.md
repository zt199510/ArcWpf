# Button 参考基准

状态：冻结源码可追溯；主示例采用此前页面读取记录，原站HTML/运行录屏未持久化，不能声明实时部署同版。

来源：[原页](https://uiarc.dev/components/button)、[批次及哈希](../2026-10-25-initial.manifest.json)、[源码](../../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/button/button.tsx)、[CSS](../../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/button/button.module.css)。许可随冻结Arc目录保存。

| 对象 | 基准/证据 | 当前适配 |
|---|---|---|
| 变体 | 源码primary/secondary/ghost/danger | 四种已实现 |
| 主示例文字 | 此前页面读取：Save changes / Secondary / Ghost / Delete | Gallery为中文对应示例；不假称同文案对照已完成 |
| 初始状态 | 普通可用态；加载/禁用另列（源码） | Gallery包含独立状态组 |
| 尺寸 | 源码令牌映射36/44/50 DIP；圆角18；padding12/16/20 | 16px根字号→DIP为项目映射 |
| 内容 | 原页包含图标组合、复制/加载；源码识别label变化 | 矢量图标/图文组合已加入；复杂内容引用检测为WPF适配 |
| 内部对齐 | 文字/图标中心，加载保持内容占位（CSS） | 图标不可用“+”字符代替；字体仍为系统回退 |
| 动效 | 按下120ms；snappy .26；morph .42（源码） | 临界阻尼近似；文字切换/宽度/颜色已实现；主按钮透明度/阴影等仍即时 |
| 容器 | 原站预览外壳精确尺寸未冻结 | 当前Gallery分组为项目测试布局，不属于控件样式 |

用户在动效补齐后明确“我测试没问题”；范围未逐项确认，后续共用布局调整不能自动视为同次验收。证据入口：[Button记录](../../verification/2026-10-25-button.md)、[规格矩阵](../../components/Button.md)。尚无已批准的像素回归截图。

下一次修改Button主预览时，建立与原页同文案的独立对照区；本次规范收口不重做已确认示例。
