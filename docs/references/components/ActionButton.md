# ActionButton 来源参考卡

- 来源页面：<https://uiarc.dev/components/action-button>
- 读取日期：2026-10-28
- 来源状态：Free
- 关键 API：`label`、`pendingLabel`、`successLabel`、`resetAfterMs`、`onAction`、`onActionError`
- 关键行为：异步操作期间保持焦点并阻止重复激活；成功显示成功态并可延迟复位；失败恢复 Idle。
- 关键动效：标签变化伴随短 blur/位移，宽度 spring；箭头离场、勾号绘制；Reduced motion 直接切换。
- 许可边界：仅参考公开文档、行为和视觉；未复制上游源码或资源。
