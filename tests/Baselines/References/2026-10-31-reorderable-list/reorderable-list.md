# Reorderable list

> Organize items with direct drag and drop.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/reorderable-list
- Markdown: https://uiarc.dev/components/reorderable-list/markdown
- Registry item: https://uiarc.dev/r/reorderable-list.json
- Source file: `registry/components/reorderable-list/reorderable-list.tsx`
- Dependencies: motion, lucide-react
- Keywords: data, interaction, react reorderable list, drag and drop list, sortable list, drag to reorder, keyboard reorder, framer motion reorder

## When to use

- Short user-ordered lists such as priorities or a playlist.
- Lists that must be reorderable by keyboard as well as by drag.

## When not to use

- Use sortable-data-table when order comes from a column.
- Use swipe-actions when rows need quick actions rather than reordering.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/reorderable-list
pnpm dlx shadcn@latest add @uiarc/reorderable-list
yarn dlx shadcn@latest add @uiarc/reorderable-list
bunx --bun shadcn@latest add @uiarc/reorderable-list
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/reorderable-list.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/reorderable-list/reorderable-list.tsx`

   The source is in the registry item: https://uiarc.dev/r/reorderable-list.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { ReorderableList } from "@/components/arc/reorderable-list/reorderable-list";

export function Priorities() {
  const [items, setItems] = useState([
    { id: "a", title: "Ship pricing page", detail: "Due Friday" },
    { id: "b", title: "Fix login redirect" },
    { id: "c", title: "Write changelog" },
  ]);
  return <ReorderableList items={items} onReorder={setItems} label="Priorities" />;
}
```

## API reference

### ReorderableList

A vertical list reordered by dragging a grip handle or with arrow keys.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `items` (required) | `{ id: string; title: string; detail?: string }[]` | – | Items in their current order. |
| `onReorder` (required) | `(items: ReorderableItem[]) => void` | – | Called with the new order on drag or keyboard move. |
| `label` | `string` | `"Reorder items"` | Accessible name for the list. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowUp / ArrowDown | On a handle, moves that item one place up or down. |

## Accessibility

- Each handle is a button labelled like "Reorder Ship pricing page. Position 1 of 3. Use arrow keys."
- Focus stays on the moved item's handle after a keyboard move.
- Only the handle starts a drag, so text in rows stays selectable.

## Motion

- Rows lift slightly on press and neighbors slide on a spring while dragging.
- The position number rolls in the direction the row moved.
- Reduced motion keeps pointer following but lands rows instantly without lift.

## Responsive behavior

- Only the 36px grip starts a drag and it sets touch-action none, so the rest of the row scrolls the page normally on touch.
- Rows fill the width and keep a 56px minimum height.

## Performance

- Built on motion Reorder with layout animation per row; not virtualized, so keep lists short.

## Notes for AI

- Use for short, user-ordered lists such as priorities or playlist order. Use sortable-data-table when order comes from a column.
- It is controlled: store items in state and persist the order from onReorder.

## Related

- [Sortable data table](https://uiarc.dev/components/sortable-data-table/markdown): Compare structured records with sortable columns.
- [Swipe actions](https://uiarc.dev/components/swipe-actions/markdown): Reveal row actions with a swipe, or from the same actions in a menu.
- [Onboarding checklist](https://uiarc.dev/components/onboarding-checklist/markdown): Guide a new workspace through setup, one step at a time.

## Also in tables

- [Tree table](https://uiarc.dev/components/tree-table/markdown): A table with nested rows, tri-state selection, hierarchy-preserving sort, and lazy-loaded children.
- [Tree view](https://uiarc.dev/components/tree-view/markdown): Navigate nested folders and structured content.
- [Filter toolbar](https://uiarc.dev/components/filter-toolbar/markdown): Keep collection filters close and easy to reset.
- [Code block](https://uiarc.dev/components/code-block/markdown): Present code with legible hierarchy and copy access.
- [JSON viewer](https://uiarc.dev/components/json-viewer/markdown): A collapsible JSON tree with search, paging for long arrays, and copy value or path.

## Guidance for AI tools

Reorderable list: Organize items with direct drag and drop. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
