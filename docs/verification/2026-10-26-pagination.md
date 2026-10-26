# Pagination 实现与验证

范围见[规格](../components/Pagination.md)，来源见[参考卡](../references/components/Pagination.md)。本次实现完成；实际窗口已完成部分操作与静态观察，专项边界仍保留。

## 最终构建

- .NET SDK10.0.402，net10.0-windows，Windows build26200。
- 输出：D:/AI/ArcWpf/artifacts/pagination-final/Debug/net10.0-windows/ArcWpf.Gallery.exe。
- ArcWpf.Controls.dll生成时间：2026-10-26T12:57:57.811525Z；SHA-256：f117e3915ec97f2df1345d9967eeb0cbe05949a9eb5d39d275e5a7e45199ddf3。
- 构建清单：artifacts/pagination-verification/build.json。实际窗口ID28772666，CUA清单确认进程路径为pagination-final输出。最初pagination输出窗口被最小化，未将其作为最终验收证据。
- 最终构建无错误，12项既有编译/测试分析器警告。所有控件代码修改均在该构建之前，之后仅更新文档。

## 实现

Pagination : Control；Page默认双向绑定、PageCount、Label、Command、PageChanged及Origin。按钮保持WPF Button原生鼠标、键盘、Invoke行为，当前页通过ItemStatus暴露。模板支持可选mark/退出层；4 DIP间距的PaginationPanel按可用宽度换行。只保留最多五个活动页码，退场副本不参与布局与命中。

Shared mark、保留页位移、新页进入、旧页淡出/位移、颜色、禁用淡化和箭头按压/释放逐项实现。OwnedMotion仅作用于私有属性；位置spring为采样近似，不保速度。Reduced/Off直接终态。完整参数及适配差异在规格中逐项列出。

## 程序化验证

命令：`dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --no-restore -p:BaseOutputPath=D:/AI/ArcWpf/artifacts/pagination-final/ --filter "FullyQualifiedName~PaginationTests|FullyQualifiedName~ReopenDuringExitInvalidatesOldCompletion|FullyQualifiedName~OwnedMotionReversalAndCancellationKeepDurableBase|FullyQualifiedName~SelectionGlidesAndRetargetsFromCurrentPositionThenStops" --logger "trx;LogFileName=pagination-final.trx" --results-directory artifacts/pagination-verification`。

Pagination专项7/7通过：

1. 空态、负页数、夹取基值恢复、int.MaxValue。
2. 双向绑定保留、事件旧新值/Origin、重复选择抑制、Command及CanExecute。
3. 五页窗口、原生Invoke、当前页ItemStatus、首尾禁用。
4. 真实WPF时钟推进、反向与连续翻页、退出层清空、终态mark对齐、主题刷新不闪回。
5. 单行宽276 DIP；窄宽116 DIP换为三行、高116 DIP；按钮36×36、坐标在容器内；恢复宽度直接对齐。容差.1 DIP属本项目测试，不是原站像素承诺。
6. Reduced/Off、隐藏、卸载重载、换模板清理。
7. 原生Button处理合成Enter、箭头焦点保留、被移除页的焦点恢复、当前页前景及颜色时钟清理。

初次实现回归发现内部Button未取得主题Style，以及重挂旧Button导致退场时钟中止、退出层残留；修复样式资源作用域，并采用独立无交互退场副本。保留退出层终态断言。中间帧等待使用有界Dispatcher轮询，要求真实坐标处于起止之间；不以Off模式代替。

全量测试在上一pagination构建运行245项，242通过、3失败（artifacts/pagination-verification/full.trx）：Dialog的ReopenDuringExitInvalidatesOldCompletion、OwnedMotionReversalAndCancellationKeepDurableBase、Calendar的SelectionGlidesAndRetargetsFromCurrentPositionThenStops。最终构建单独复跑这三项和Pagination七项，总计8/10：Calendar通过，Dialog与OwnedMotion仍失败。没有修改这些既有测试/实现，也没有宣称全量通过。最终新增测试和箭头释放时长调整后未再次全量运行。

## 实际Windows窗口

最终版本986×753像素窗口，系统DPI未独立核实。通过CUA Raise、导航滚动及真实坐标点击；截图在会话工具输出中，未保存为长期基准。

- 默认page1/count12，显示1–5，上一页禁用；主行外框与文本无裁剪。
- 点击5：页码变为3–7，mark对齐5，绑定说明变为第5页。
- 点击下一页后按Enter：第7页，焦点仍为Next page；空格进一步到第8页。
- Shift+Tab回到Page10，动画结束后焦点框对齐该按钮；鼠标主题按钮点击后焦点框消失。
- 116 DIP窄容器三行可见，mark对齐第二行的6；单页与禁用示例可见。
- 切换暗色主题后当前页、箭头、禁用与窄容器样式均正确刷新，无整框跳动或退出数字残留。

这些观察证明当前操作和终态，不构成每项动画与浏览器逐帧一致性的结论。

## 未验收与暂缓

未验收：100/150/200%多DPI、跨屏、读屏、完整Tab循环、实际动态页数归零、按住箭头的逐帧过程、与原版同环境动画对照和性能。Full/Reduced/Off有程序化覆盖，实际Reduced/Off观感待验收。

暂缓：省略号、首尾快捷按钮、每页大小、总条数、异步加载/服务器请求、触摸、RTL专项；沿用已冻结范围。无自动提交或推送。


## 用户反馈

2026-10-26用户反馈“没问题上传代码”，确认当前Pagination版本使用结果，并授权提交推送。本反馈不推断多DPI、读屏、性能或全量测试通过；此前“不自动提交”描述为当时状态，本次依明确授权上传。
