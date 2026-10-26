# Third-party notices

## Arc

ArcWpf.Core/ArcThemeResources.cs 的设计令牌及 ArcWpf.Controls/Button 的外观与动效参数参考并改编自 Arc 的 foundation.css、button.module.css、button.tsx。来源 URL、采集时间与哈希见 docs/references/2026-10-25-initial.manifest.json；冻结原文位于 tests/Baselines/References/2026-10-25-initial/arc。

以下保留原许可全文：
MIT License

Copyright (c) 2026 Elia Kuratli

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## TKCode.Arc

仅作实现经验参考，未引入它的程序集或直接移植其 Button 实现。仓库保存的参考快照保留原 MIT 许可，见 tests/Baselines/References/2026-10-25-initial/tkcode/LICENSE。

## 开发依赖与字体

测试使用 MSTest 4.0.2（MIT）；实际依赖与传递版本见测试项目 packages.lock.json。生产控件仅依赖 .NET/WPF，无第三方 UI 或动画包。
Segoe UI 与 Microsoft YaHei UI 通过系统字体名称引用，项目不包含或分发字体文件。

## Switch 补充

ArcWpf.Controls/Switch 的几何、状态和动效参数参考同批冻结的 Arc switch.tsx / switch.module.css。采用上文 Arc MIT 许可声明；WPF原生实现与临界阻尼计算为本项目适配代码，未引入Web动画依赖。

## Input 补充

ArcWpf.Controls/Input 的字段结构、尺寸和语义颜色参考冻结的 Arc input.tsx / input.module.css，适用上文 Arc MIT 许可。编辑、绑定、验证与自动化由WPF原生机制及本项目适配实现，未引入Web依赖。

## Popover 补充

Popover几何与动效参数来自初始冻结批次Arc popover.tsx/popover.module.css与foundation.css，适用上文Arc MIT许可。CSS ease-spring采样值用于WPF关键帧；采用Floating主阴影参数，浅色次层阴影暂未复现。未引入Radix或Web运行依赖。2026-10-25补采官网HTML仅作参考证据，不作为产品代码或素材分发。

## CheckBox 补充

CheckBox方框几何、勾号/横线的三点路径、颜色与动效参数参考Arc免费checkbox Registry组件及motion-tokens，按Arc MIT许可使用。冻结于项目采集批次2026-10-25-checkbox（非官方发行版本），完整文件与SHA-256见tests/Baselines/References/2026-10-25-checkbox/manifest.json；适用上文Arc MIT声明。WPF控件、动画调度及原生三态适配为项目实现，未引入Radix或Motion运行库。

## RadioButton / RadioGroup 补充

Arc免费radio-group Registry组件及共享motion-tokens用于几何、颜色、动效和主样本参考，适用上文Arc MIT许可。项目冻结批次2026-10-26-radio（非上游发行版本），文件/哈希/来源见tests/Baselines/References/2026-10-26-radio/manifest.json。未引入React/Motion运行库，原生WPF单选及分组行为、布局面板、动画调度为项目适配实现。

## Select 补充

Arc免费select Registry组件及motion-tokens用于结构、几何、文案和动画参数参考，适用上文Arc MIT许可。批次2026-10-26-select为项目采集标识，非官方版本；快照和哈希见tests/Baselines/References/2026-10-26-select/manifest.json。预览JS仅保留用于确认Team选项的公开片段所在文件，未引入产品运行。图标由项目自行绘制矢量路径，未打包Lucide库或资产；原生ComboBox适配与动画调度为项目代码。

## Tabs 补充

Arc免费tabs Registry的结构、几何、主预览文案及motion-tokens用于WPF复刻参考，适用上文Arc MIT许可。2026-10-26-tabs为项目采集批次而非官方版本；文件、来源与SHA-256见tests/Baselines/References/2026-10-26-tabs/manifest.json。采集HTML/CSS/JS仅供溯源，不作为产品运行依赖；WPF原生TabControl适配、内存退出图像、弹簧调度与箭头路径由项目实现，未引入React/Radix/Motion/Lucide运行库。

## Dialog 补充

Dialog几何、文案与动效参考Arc免费dialog Registry、公开预览JS及motion-tokens，适用上文Arc MIT许可。2026-10-26-dialog为项目采集批次，非官方版本；来源与哈希见tests/Baselines/References/2026-10-26-dialog/manifest.json。参考JS/CSS不作为产品运行依赖；DialogHost、WPF模态输入/焦点、动画适配和叉号路径为项目实现，未引入React/Radix/Motion/Lucide运行库。

## Tooltip 补充

Tooltip几何、延迟、主预览文案与动效参数参考Arc免费tooltip Registry、公开预览JS、foundation及motion-tokens，适用上文Arc MIT声明。2026-10-26-tooltip为项目采集批次，非官方版本；文件、来源、共享资产复用和SHA-256见tests/Baselines/References/2026-10-26-tooltip/manifest.json。WPF Popup包装、共享warm状态、定位与动画调度为项目实现；未引入React/Radix/Motion运行依赖。单层Raised阴影代替原版浅色双层阴影。

## Toast补充

Toast结构、尺寸、文案、自动关闭与动效参考Arc免费toast Registry、公开预览JS、foundation及motion-tokens，适用上文Arc MIT声明。2026-10-26-toast为项目采集批次，文件/来源/SHA-256见tests/Baselines/References/2026-10-26-toast/manifest.json。WPF ToastHost队列和通知生命周期为项目实现，未引入React/Motion/Lucide运行依赖；勾号为自绘矢量。

## Textarea 补充

Textarea 的结构、最小高度、圆角、字段文案和动效范围参考 Arc 免费 Textarea Registry 及 motion-tokens，适用上文 Arc MIT 声明。2026-10-26-textarea 为项目采集批次；文件、来源和 SHA-256 见 `tests/Baselines/References/2026-10-26-textarea/manifest.json`。WPF 原生多行编辑、绑定、IME、验证和自动化行为由项目实现，未引入 React/Motion 运行依赖。

## Alert 补充

Alert 的结构、语气、图标尺寸、关闭行为和动效参数参考 Arc 免费 Alert Registry 及 motion-tokens，适用上文 Arc MIT 声明。2026-10-26-alert 为项目采集批次；文件、来源和 SHA-256 见 `tests/Baselines/References/2026-10-26-alert/manifest.json`。WPF 原生 ContentControl、主题适配、自动化和动效为项目实现，未引入 React/Motion/Lucide 运行依赖。

## ToastStack 补充

ToastStack 的队列、类型、堆叠几何、关闭和动效参数参考 Arc 免费 ToastStack Registry、CSS 和 motion-tokens，适用上文 Arc MIT 声明。2026-10-26-toast-stack 为项目采集批次；文件、来源和 SHA-256 见 `tests/Baselines/References/2026-10-26-toast-stack/manifest.json`。WPF 原生 ItemsControl、计时、堆叠面板、命令和主题适配为项目实现，未引入 React/Motion/Lucide 运行依赖。

## Drawer 补充

Drawer 的边缘面板结构、几何、模态交互和动效参数参考 Arc 免费 Drawer Registry、Markdown 页面和 motion-tokens，适用上文 Arc MIT 声明。2026-10-26-drawer 为项目采集批次；文件、来源和 SHA-256 见 `tests/Baselines/References/2026-10-26-drawer/manifest.json`。WPF 原生 DrawerHost、ContentControl、焦点保护和动画清理为项目实现；未引入 React/Radix/Motion/Lucide 运行依赖，参考源码与 CSS 未复制进产品。

## Accordion 补充

Accordion 的单开结构、FAQ 文案、Chevron 几何和高度动效参考 Arc 免费 Accordion Registry、Markdown 页面及 motion-tokens，适用上文 Arc MIT 声明。2026-10-26-accordion 为项目采集批次；文件、来源和 SHA-256 见 `tests/Baselines/References/2026-10-26-accordion/manifest.json`。WPF 原生 ItemsControl、HeaderedContentControl、键盘导航和 ExpandCollapse Provider 为项目实现；未引入 React/Radix/Motion/Lucide 运行依赖，参考源码与 CSS 未复制进产品。

## ExpandableCard 补充

ExpandableCard 的摘要卡结构、宽度变化、Chevron 几何和展开动效参考 Arc 免费 ExpandableCard Registry、Markdown 页面及 motion-tokens，适用上文 Arc MIT 声明。2026-10-26-expandable-card 为项目采集批次；文件、来源和 SHA-256 见 `tests/Baselines/References/2026-10-26-expandable-card/manifest.json`。WPF 原生 ContentControl、ToggleButton、ExpandCollapse Provider 和动效适配为项目实现；未引入 React/Radix/Motion/Lucide 运行依赖，参考源码与 CSS 未复制进产品。

## Menu 补充

Menu 的短命令列表、Popup 锚定、条目状态和键盘交互参考 Arc 免费 Dropdown Menu Registry、Markdown 页面及 motion-tokens，适用上文 Arc MIT 声明。2026-10-26-menu 为项目采集批次；文件、来源和 SHA-256 见 `tests/Baselines/References/2026-10-26-menu/manifest.json`。WPF Popup、Button、ListBox、ICommand 和自动化语义为项目实现；未引入 React/Radix/Motion/Lucide 运行依赖。
