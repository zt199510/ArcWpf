# Tree table

> A table with nested rows, tri-state selection, hierarchy-preserving sort, and lazy-loaded children.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/tree-table
- Markdown: https://uiarc.dev/components/tree-table/markdown
- Registry item: https://uiarc.dev/r/tree-table.json
- Source file: `registry/components/tree-table/tree-table.tsx`
- Dependencies: motion, lucide-react, @radix-ui/react-checkbox
- Keywords: data, new, react tree table, treegrid, nested table rows, expandable table, hierarchical data table, tri-state checkbox tree, lazy load tree rows, file tree table

## When to use

- File or folder listings with size, owner, and modified columns.
- Budgets, org charts, or accounts where totals roll up through nested rows.
- Large trees whose deeper levels should load from the server on demand.

## When not to use

- Use tree-view for navigation trees without columns.
- Use data-grid or sortable-data-table for flat records.
- Use accordion when each item opens into free form content rather than child rows.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/tree-table
pnpm dlx shadcn@latest add @uiarc/tree-table
yarn dlx shadcn@latest add @uiarc/tree-table
bunx --bun shadcn@latest add @uiarc/tree-table
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/tree-table.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react @radix-ui/react-checkbox
```

2. Copy the source into your project. Main file: `registry/components/tree-table/tree-table.tsx`

   The source is in the registry item: https://uiarc.dev/r/tree-table.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { TreeTable } from "@/components/arc/tree-table/tree-table";
import type { TreeTableColumn, TreeTableRow } from "@/components/arc/tree-table/tree-table";

type Item = { name: string; size: number; owner: string };

const columns: TreeTableColumn<Item>[] = [
  { id: "name", header: "Name", cell: item => item.name, sortValue: item => item.name, width: "minmax(0, 2fr)" },
  { id: "size", header: "Size", cell: item => `${item.size} MB`, sortValue: item => item.size, align: "end", width: "96px" },
  { id: "owner", header: "Owner", cell: item => item.owner, hideBelow: 520 },
];

export function Files({ rows }: { rows: TreeTableRow<Item>[] }) {
  return (
    <TreeTable
      label="Files"
      rows={rows}
      columns={columns}
      selectable
      rowLabel={item => item.name}
      defaultExpanded={["design"]}
      loadChildren={row => fetchChildren(row.id)}
      maxHeight={480}
    />
  );
}
```

## API reference

### TreeTable

A table whose rows nest, with spring expand and collapse, indent guides, tri-state selection, hierarchy safe sorting, and lazy children.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `rows` (required) | `TreeTableRow<T>[]` | – | Top level rows: { id, data, children?, hasChildren? }. |
| `columns` (required) | `TreeTableColumn<T>[]` | – | Columns: { id, header, cell(data, context), sortValue?, width?, align?, hideBelow? }. The first shown column holds the tree. |
| `label` (required) | `string` | – | Accessible name of the table. |
| `expanded` | `string[]` | – | Controlled ids of expanded rows. |
| `defaultExpanded` | `string[]` | `[]` | Rows expanded at first when uncontrolled. |
| `onExpandedChange` | `(ids: string[]) => void` | – | Called when rows expand or collapse. |
| `selectable` | `boolean` | `false` | Adds a checkbox column. Checking a parent checks everything under it. |
| `selected` | `string[]` | – | Controlled ids of selected rows. |
| `defaultSelected` | `string[]` | `[]` | Rows selected at first when uncontrolled. |
| `onSelectedChange` | `(ids: string[]) => void` | – | Called with the new selection. Parents are added or removed to match their children. |
| `sort` | `TreeTableSort \| null` | – | Controlled sort: { columnId, direction: "ascending" \| "descending" }. |
| `defaultSort` | `TreeTableSort \| null` | `null` | Starting sort when uncontrolled. |
| `onSortChange` | `(sort: TreeTableSort \| null) => void` | – | Called as a header cycles ascending, descending, and off. |
| `loadChildren` | `(row: TreeTableRow<T>) => Promise<TreeTableRow<T>[]>` | – | Loads children of a row with hasChildren on first expand. A rejection shows a retry row. |
| `rowLabel` | `(data: T) => string` | – | Names a row for its checkbox, such as the file or team name. |
| `onRowActivate` | `(row: TreeTableRow<T>) => void` | – | Called when a row without children is clicked or gets Enter. Parent rows toggle instead. |
| `maxHeight` | `number \| string` | – | Caps the height; the header stays pinned while the body scrolls. |
| `emptyState` | `ReactNode` | `"Nothing to show"` | Shown when there are no rows. |
| `className` | `string` | – | Class on the root. |
| `ref` | `Ref<HTMLDivElement>` | – | The root element. |

### collectExpandableIds

Returns the ids of every row whose children are known, for an external expand all.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `rows` (required) | `TreeTableRow<T>[]` | – | The tree to walk. |
| `loaded` | `Record<string, TreeTableRow<T>[]>` | – | Children loaded so far, keyed by parent id. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowDown / ArrowUp | Moves between visible rows. |
| ArrowRight | Expands a row, or moves to its first child when already open. |
| ArrowLeft | Collapses a row, or moves to its parent. |
| Home / End | Moves to the first or last visible row. |
| PageDown / PageUp | Moves ten rows. |
| * | Expands every sibling of the focused row. |
| Space | Toggles selection when selectable. |
| Enter | Toggles a parent, retries a failed load, or activates a leaf row. |
| Cmd/Ctrl+A | Selects or clears all rows when selectable. |

## Accessibility

- Follows the treegrid pattern: rows carry aria-level, aria-posinset, aria-setsize, and aria-expanded, with a single roving tabindex.
- Sortable headers expose aria-sort; checkboxes are named from rowLabel and show the indeterminate state for partly selected parents.
- Loading, load errors, sorting, and expand all are announced through a polite live region.
- If the focused row disappears (its parent collapsed), focus moves to the nearest visible ancestor.

## Motion

- Rows open and close on a height spring with a short stagger per sibling; sorting moves rows with a position layout animation.
- The chevron rotates on a snappy spring and the indent guide of the hovered or focused branch lights up.
- Reduced motion removes the height spring, stagger, and rotation; rows appear with an instant fade.

## Responsive behavior

- A ResizeObserver on the root hides any column whose hideBelow is wider than the table.
- Column widths are grid tracks, so minmax tracks shrink and the tree column truncates instead of overflowing.
- Hover styles apply only on hover-capable fine pointers; touch taps toggle and activate rows directly.

## Performance

- Every visible row renders and animates; it is not virtualized, so keep expanded trees to a few hundred rows.
- Selection normalization walks the whole known tree on each change; very large preloaded trees should load lazily.

## Notes for AI

- Use when hierarchical rows also need columns: file sizes, budgets by team, nested tasks. For a plain nested list with no columns use tree-view.
- Give every row a stable id. For lazy data, set hasChildren and leave children undefined, then pass loadChildren.
- Sort by giving columns a sortValue; siblings sort within their parent so the tree never breaks.
- Use hideBelow on secondary columns so narrow containers keep the tree column readable.

## Related

- [Tree view](https://uiarc.dev/components/tree-view/markdown): Navigate nested folders and structured content.
- [Data grid](https://uiarc.dev/components/data-grid/markdown): A spreadsheet grid with range selection, inline editing, a fill handle, and animated sorting.
- [Sortable data table](https://uiarc.dev/components/sortable-data-table/markdown): Compare structured records with sortable columns.
- [Checkbox](https://uiarc.dev/components/checkbox/markdown): A binary choice with a precise, legible state.

## Also in tables

- [Reorderable list](https://uiarc.dev/components/reorderable-list/markdown): Organize items with direct drag and drop.
- [Filter toolbar](https://uiarc.dev/components/filter-toolbar/markdown): Keep collection filters close and easy to reset.
- [Code block](https://uiarc.dev/components/code-block/markdown): Present code with legible hierarchy and copy access.
- [JSON viewer](https://uiarc.dev/components/json-viewer/markdown): A collapsible JSON tree with search, paging for long arrays, and copy value or path.

## Guidance for AI tools

Tree table: A table with nested rows, tri-state selection, hierarchy-preserving sort, and lazy-loaded children. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
