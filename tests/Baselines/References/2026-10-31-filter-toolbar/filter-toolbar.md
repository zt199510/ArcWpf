# Filter toolbar

> Keep collection filters close and easy to reset.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/filter-toolbar
- Markdown: https://uiarc.dev/components/filter-toolbar/markdown
- Registry item: https://uiarc.dev/r/filter-toolbar.json
- Source file: `registry/components/filter-toolbar/filter-toolbar.tsx`
- Dependencies: motion, lucide-react
- Keywords: data, filter, react filter toolbar, filter chips, add filter menu, faceted filters, filter bar, removable chips, table filters

## When to use

- Faceted filtering above tables and lists, with removable chips.
- Adding filters through a two step field then value menu.
- Views that pair with sortable-data-table and pagination for a full list page.

## When not to use

- Use chip-group for a fixed set of toggles.
- Use search-field for free text search.
- Use multi-select when one field takes several values in a form.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/filter-toolbar
pnpm dlx shadcn@latest add @uiarc/filter-toolbar
yarn dlx shadcn@latest add @uiarc/filter-toolbar
bunx --bun shadcn@latest add @uiarc/filter-toolbar
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/filter-toolbar.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/filter-toolbar/filter-toolbar.tsx`

   The source is in the registry item: https://uiarc.dev/r/filter-toolbar.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { FilterToolbar, type FilterChip } from "@/components/arc/filter-toolbar/filter-toolbar";

const fields = [
  { id: "status", label: "Status", options: ["Open", "Closed"] },
  { id: "owner", label: "Owner", options: ["Maya", "Leo"] },
];

export function IssueFilters() {
  const [filters, setFilters] = useState<FilterChip[]>([]);
  return (
    <FilterToolbar
      filters={filters}
      onRemove={id => setFilters(f => f.filter(x => x.id !== id))}
      onClearAll={() => setFilters([])}
      addFilter={{ fields, onAdd: chip => setFilters(f => [...f.filter(x => x.id !== chip.id), chip]) }}
    />
  );
}
```

## API reference

### FilterToolbar

A row of removable filter chips with Clear all and an optional Add filter menu.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `filters` (required) | `{ id: string; label: string; value?: string }[]` | – | Applied filters, shown as chips. |
| `onRemove` (required) | `(id: string) => void` | – | Called when a chip's remove button is pressed. |
| `onClearAll` | `() => void` | – | Called by Clear all, which appears while any filter is applied. |
| `addFilter` | `{ fields: FilterField[]; onAdd: (filter: FilterChip, field: FilterField) => void; label?: string; align?: "start" \| "end" }` | – | Adds an Add filter trigger that opens a two step field and value menu. |
| `children` | `ReactNode` | – | Extra actions next to the Add filter trigger. |
| `label` | `string` | `"Active filters"` | Accessible name for the toolbar group. |

### FilterMenu

The Add filter trigger and its two step menu, usable on its own.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `fields` (required) | `{ id: string; label: string; icon?: ReactNode; options: (string \| { value: string; label?: string; hint?: string \| number; icon?: ReactNode })[] }[]` | – | Filterable fields and their values. |
| `onSelect` (required) | `(filter: FilterChip, field: FilterField) => void` | – | Receives a chip whose id is the field id, so a second pick for a field replaces the first. |
| `active` | `FilterChip[]` | `[]` | Applied filters, used to mark current values. |
| `label` | `string` | `"Add filter"` | Trigger text. |
| `align` | `"start" \| "end"` | `"end"` | Trigger edge the panel lines up with. Flips when there is no room. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowDown / ArrowUp | On the trigger, opens the menu at the first or last item; inside, moves between items. |
| Home / End | Moves to the first or last menu item. |
| ArrowRight / ArrowLeft | Drills into a field's values, or goes back to fields. |
| Letters | Typeahead jumps to the matching item. |
| Escape | Closes the menu and returns focus to the trigger. |

## Accessibility

- The toolbar is role="group"; the menu panel is a labelled role="dialog" holding a role="menu" of checkable items.
- Remove buttons are labelled like "Remove Status: Open", and removing a chip moves focus to its neighbor or the Add filter trigger.
- Additions and removals are announced through a role="status" region.

## Motion

- Chips open and close their slot on a spring so neighbors glide; values morph in place.
- The Add filter trigger morphs into the menu surface and steps slide between fields and values.
- Reduced motion replaces the morphs and slides with instant changes and short fades.

## Responsive behavior

- Below 520px the toolbar stacks into a column with actions aligned to the end.
- The menu panel is min(16rem, 100vw minus a gutter) wide, flips its edge when there is no room, and its list caps at min(20rem, 55vh).
- Hover highlights apply only on hover-capable fine pointers.

## Performance

- Several ResizeObservers measure chips and the menu for morphs; menu options are not virtualized, so keep value lists short.

## Notes for AI

- Use above tables and lists for faceted filtering. Use chip-group for a fixed set of toggles and search-field for free text.
- Keep one chip per field: onAdd receives a chip keyed by field id, so replace any existing chip with that id.
- Pair with sortable-data-table and pagination for a full list view.

## Related

- [Sortable data table](https://uiarc.dev/components/sortable-data-table/markdown): Compare structured records with sortable columns.
- [Chip group](https://uiarc.dev/components/chip-group/markdown): Filter by a few facets with chips that morph as you pick them.
- [Search field](https://uiarc.dev/components/search-field/markdown): A recognizable search entry point with clear affordances.
- [Multi-select](https://uiarc.dev/components/multi-select/markdown): Select several values while keeping the field readable.
- [Pagination](https://uiarc.dev/components/pagination/markdown): Move through a long collection with clear bounds.

## Also in tables

- [Tree table](https://uiarc.dev/components/tree-table/markdown): A table with nested rows, tri-state selection, hierarchy-preserving sort, and lazy-loaded children.
- [Tree view](https://uiarc.dev/components/tree-view/markdown): Navigate nested folders and structured content.
- [Reorderable list](https://uiarc.dev/components/reorderable-list/markdown): Organize items with direct drag and drop.
- [Code block](https://uiarc.dev/components/code-block/markdown): Present code with legible hierarchy and copy access.
- [JSON viewer](https://uiarc.dev/components/json-viewer/markdown): A collapsible JSON tree with search, paging for long arrays, and copy value or path.

## Guidance for AI tools

Filter toolbar: Keep collection filters close and easy to reset. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
