# Sortable data table

> Compare structured records with sortable columns.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/sortable-data-table
- Markdown: https://uiarc.dev/components/sortable-data-table/markdown
- Registry item: https://uiarc.dev/r/sortable-data-table.json
- Source file: `registry/components/sortable-data-table/sortable-data-table.tsx`
- Dependencies: motion, lucide-react
- Keywords: data, table, react data table, sortable table, table with row selection, animated table sort, responsive table, checkbox table

## When to use

- Tabular records people sort and select, such as projects, invoices, or users.
- Tables that need Shift-click range selection and a count line with Clear.

## When not to use

- Use data-grid when people edit cells like a spreadsheet.
- Use timeline for chronological activity.
- Use reorderable-list when people set the order by hand.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/sortable-data-table
pnpm dlx shadcn@latest add @uiarc/sortable-data-table
yarn dlx shadcn@latest add @uiarc/sortable-data-table
bunx --bun shadcn@latest add @uiarc/sortable-data-table
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/sortable-data-table.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/sortable-data-table/sortable-data-table.tsx`

   The source is in the registry item: https://uiarc.dev/r/sortable-data-table.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { SortableDataTable } from "@/components/arc/sortable-data-table/sortable-data-table";

const projects = [
  { id: "p1", name: "Harbour", owner: "Maya", budget: 42000 },
  { id: "p2", name: "Atlas", owner: "Leo", budget: 18500 },
];

export function Projects() {
  return (
    <SortableDataTable
      rows={projects}
      rowKey="id"
      caption="Projects"
      columns={[{ key: "name", label: "Name" }, { key: "owner", label: "Owner" }, { key: "budget", label: "Budget" }]}
      defaultSort={{ key: "name", direction: "asc" }}
      selectable
      itemName={{ one: "project", other: "projects" }}
    />
  );
}
```

## API reference

### SortableDataTable

A generic table with sortable columns, optional row selection, and rows that glide when re-sorted.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `rows` (required) | `T[]` | – | Row objects. |
| `columns` (required) | `{ key: string; label: string; sortable?: boolean; render?: (value: unknown, row: T) => ReactNode; numeric?: boolean; width?: number \| string }[]` | – | Column definitions. Columns are sortable unless sortable is false; numeric is detected when every value is a number. |
| `rowKey` (required) | `keyof T \| ((row: T) => string)` | – | Stable key per row. |
| `caption` | `string` | `"Data table"` | Table caption, used as its accessible name. |
| `emptyMessage` | `string` | `"No rows to show"` | Shown when rows is empty. |
| `defaultSort` | `{ key: string; direction: "asc" \| "desc" }` | – | Sort applied on first render. |
| `onSortChange` | `(sort: SortState) => void` | – | Called when a header is pressed. |
| `selectable` | `boolean` | `false` | Adds a checkbox column, row click selection, and a count line with Clear. |
| `selectedKeys` | `string[]` | – | Controlled selection. |
| `defaultSelectedKeys` | `string[]` | – | Initial selection when uncontrolled. |
| `onSelectionChange` | `(keys: string[]) => void` | – | Called with the new selection. |
| `itemName` | `{ one: string; other: string }` | `{ one: "row", other: "rows" }` | Noun for the count line, as in "6 projects". |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowLeft / ArrowRight | Moves between sortable headers. |
| ArrowUp / ArrowDown | Moves between row checkboxes; down from Select all enters the rows. |
| Home / End | Jumps to the first or last header or row checkbox. |
| Enter / Space | Sorts by a header or toggles a checkbox. |
| Shift + click | Selects a range of rows. |
| Escape | Clears the selection. |

## Accessibility

- A native table with caption, scoped headers, and aria-sort on sortable columns.
- Sort buttons are labelled like "Sort by Budget, currently ascending"; the select all checkbox shows a mixed state.
- Sort and selection changes are announced through a role="status" region.

## Motion

- Rows glide to their new positions on a spring when the sort changes; the sort arrow flips.
- The selection count rolls and Clear fades in with a short blur.
- Reduced motion reorders instantly and fades the count.

## Responsive behavior

- Wider layouts scroll horizontally inside the table instead of the page.
- Below 620px each row folds into two lines, and the header becomes a scrolling strip of sort buttons with Select all pinned.
- Hover row fills apply only on hover-capable fine pointers.

## Performance

- Rows are not virtualized and each uses position layout animation for re-sorts; paginate long lists with pagination.
- Sorting runs client side over the rows you pass.

## Notes for AI

- Use for tabular records people sort and select. Use timeline for chronological activity and a plain list for simple items.
- Sorting is client side over the given rows; for server sorting, control defaultSort per fetch and page with pagination.
- Use render for badges, avatars, or formatted numbers inside cells.

## Related

- [Filter toolbar](https://uiarc.dev/components/filter-toolbar/markdown): Keep collection filters close and easy to reset.
- [Pagination](https://uiarc.dev/components/pagination/markdown): Move through a long collection with clear bounds.
- [Badge](https://uiarc.dev/components/badge/markdown): A small label for status, category, or metadata.
- [Empty state](https://uiarc.dev/components/empty-state/markdown): A useful next step when there is nothing to show yet.
- [Checkbox](https://uiarc.dev/components/checkbox/markdown): A binary choice with a precise, legible state.

## Also in tables

- [Tree table](https://uiarc.dev/components/tree-table/markdown): A table with nested rows, tri-state selection, hierarchy-preserving sort, and lazy-loaded children.
- [Tree view](https://uiarc.dev/components/tree-view/markdown): Navigate nested folders and structured content.
- [Reorderable list](https://uiarc.dev/components/reorderable-list/markdown): Organize items with direct drag and drop.
- [Code block](https://uiarc.dev/components/code-block/markdown): Present code with legible hierarchy and copy access.
- [JSON viewer](https://uiarc.dev/components/json-viewer/markdown): A collapsible JSON tree with search, paging for long arrays, and copy value or path.

## Guidance for AI tools

Sortable data table: Compare structured records with sortable columns. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
