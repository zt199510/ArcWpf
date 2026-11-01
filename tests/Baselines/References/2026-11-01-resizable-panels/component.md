# Resizable panels

> Trade space between panes by dragging the divider between them.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/resizable-panels
- Markdown: https://uiarc.dev/components/resizable-panels/markdown
- Registry item: https://uiarc.dev/r/resizable-panels.json
- Source file: `registry/components/resizable-panels/resizable-panels.tsx`
- Dependencies: motion, lucide-react
- Keywords: split view, resizable, panes, react resizable panels, resizable sidebar, draggable divider, split pane, collapsible panel, resizable layout

## When to use

- Editor, mail, or dashboard layouts where people trade space between side-by-side panes.
- Sidebars that should collapse by dragging or flicking toward the edge.
- Layouts whose pane sizes should persist between visits via onLayoutChange.

## When not to use

- Use drawer for a panel that slides over the page.
- Use tabs when only one pane needs to be visible at a time.
- Avoid it for vertical splits, since it is horizontal only.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/resizable-panels
pnpm dlx shadcn@latest add @uiarc/resizable-panels
yarn dlx shadcn@latest add @uiarc/resizable-panels
bunx --bun shadcn@latest add @uiarc/resizable-panels
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/resizable-panels.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/resizable-panels/resizable-panels.tsx`

   The source is in the registry item: https://uiarc.dev/r/resizable-panels.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ResizablePanel, ResizablePanels } from "@/components/arc/resizable-panels/resizable-panels";

export function MailLayout() {
  return (
    <div style={{ height: 480 }}>
      <ResizablePanels label="Mail" onLayoutChange={saveLayout}>
        <ResizablePanel id="folders" label="Folders" defaultSize={22} minSize={160} collapsible>
          <FolderList />
        </ResizablePanel>
        <ResizablePanel id="messages" label="Messages" defaultSize={78}>
          <MessageList />
        </ResizablePanel>
      </ResizablePanels>
    </div>
  );
}
```

## API reference

### ResizablePanels

A horizontal split view with draggable, keyboard-operable dividers between panes. Needs a definite height.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `children` (required) | `ReactNode` | – | ResizablePanel elements in order. The set of panes should stay stable. |
| `label` | `string` | – | aria-label for the group. |
| `onLayoutChange` | `(sizes: number[]) => void` | – | Called with each pane's share in percent after a resize settles. |
| `className` | `string` | – | Class on the group element. |

### ResizablePanel

Configuration for one pane. The group renders it.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `id` (required) | `string` | – | Stable id, used by the divider's aria-controls. |
| `label` (required) | `string` | – | Names the pane for its divider. |
| `defaultSize` (required) | `number` | – | Starting share of the group. Shares are relative and need not sum to 100. |
| `minSize` | `number` | `80` | Width in px where the pane starts to resist. Content clips and fades below it. |
| `maxSize` | `number` | – | Maximum width in px. |
| `collapsible` | `boolean` | – | Dragging well past the minimum or flicking toward the edge snaps the pane closed. |
| `defaultCollapsed` | `boolean` | – | Starts collapsed. Requires collapsible. |
| `className` | `string` | – | Class on the pane. |
| `children` | `ReactNode` | – | Pane content. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowLeft / ArrowRight | Moves the focused divider 16px; with Shift, 64px. Past a limit, collapses a collapsible pane. |
| Home / End | Moves the divider to its minimum or maximum. |
| Enter | Collapses or restores a collapsible neighbouring pane. |
| Escape | During a drag, returns the divider to where it started. |

## Accessibility

- Each divider is a focusable role="separator" with aria-orientation, aria-controls, aria-valuenow, min, and max in percent.
- aria-valuetext announces the share, or that a pane is hidden and Enter shows it.
- Collapsed panes are inert. The group has role="group"; pass label to name it.

## Motion

- Dividers follow the pointer 1:1, rubber-band past limits, and spring to rest; flicks toward an edge collapse a pane. Double-click resets the layout.
- Pane content fades as it is squeezed below its minimum and a restore tab pops in on a snappy spring.
- Reduced motion jumps panes to their targets and fades the restore tab.

## Responsive behavior

- Panes do not stack on narrow screens; on mobile, render a single pane or switch to tabs yourself.
- Dividers use pointer capture with touch-action none, so touch, pen, and mouse drags all work.
- The group needs a definite height from its parent.

## Performance

- A ResizeObserver on the group refits pane widths when the container resizes.
- Dragging sets widths directly and springs only on release or collapse; pane content is never re-rendered by the drag itself.

## Notes for AI

- Use for editor, mail, or dashboard layouts where people trade space between side-by-side panes. Horizontal only.
- Give the parent a definite height and persist onLayoutChange shares to restore via defaultSize.

## Related

- [Drawer](https://uiarc.dev/components/drawer/markdown): A temporary side surface for focused work.
- [Tree view](https://uiarc.dev/components/tree-view/markdown): Navigate nested folders and structured content.
- [Tabs](https://uiarc.dev/components/tabs/markdown): Switch between related content in the same context.

## Also in expand

- [Scroll area](https://uiarc.dev/components/scroll-area/markdown): A native scroll container with thin overlay scrollbars and edge fades that appear only when content overflows.
- [Accordion](https://uiarc.dev/components/accordion/markdown): Progressively reveal supporting information in place.
- [Expandable card](https://uiarc.dev/components/expandable-card/markdown): Give a dense card more room when requested.
- [Expandable text](https://uiarc.dev/components/expandable-text/markdown): Long text clamped to a few lines with a soft fade that glides open with Read more and closes with Show less, shown only when the text overflows.

## Guidance for AI tools

Resizable panels: Trade space between panes by dragging the divider between them. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
