# Command palette

> A complete keyboard driven action surface with search, grouped results, and shortcuts.

- Type: Block
- Page: https://uiarc.dev/components/blocks/command-palette
- Markdown: https://uiarc.dev/components/blocks/command-palette/markdown

- Access: Free, open source
- Registry id: `command-palette`
- Source file: `registry/components/command-palette/command-palette.tsx`
- Built from: Search, Keyboard navigation, Action list
- Keywords: react command palette, cmd k menu, command menu, spotlight search, cmdk alternative, keyboard command palette, quick actions

Use it for product wide actions and navigation. Provide meaningful groups, icons, keyboard shortcuts, and handlers.

## When to use

- App-wide command search opened with Cmd or Ctrl plus K.
- Jumping to pages, creating items, and running actions from one input.
- Inline search panels that group results with shortcuts.

## When not to use

- Use combobox to pick a value inside a form.
- Use dropdown-menu for a short list of actions.
- Use expanding-search or search-field to search page content.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/command-palette
pnpm dlx shadcn@latest add @uiarc/command-palette
yarn dlx shadcn@latest add @uiarc/command-palette
bunx --bun shadcn@latest add @uiarc/command-palette
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/command-palette.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/command-palette/command-palette.tsx`

   The source is in the registry item: https://uiarc.dev/r/command-palette.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { CommandPalette, type CommandItem } from "@/components/arc/command-palette/command-palette";

const items: CommandItem[] = [
  { id: "new", label: "New project", group: "Create", shortcut: "N" },
  { id: "invite", label: "Invite teammate", group: "Team", keywords: ["member"] },
  { id: "theme", label: "Toggle theme", description: "Switch light and dark" },
];

export function Palette({ onClose }: { onClose: () => void }) {
  return <CommandPalette items={items} onSelect={item => run(item.id)} onClose={onClose} />;
}
```

## Examples

### Inside a dialog

```tsx
<Dialog open={open} onOpenChange={setOpen}>
  <DialogContent title="Command palette">
    <CommandPalette
      items={items}
      onSelect={item => { run(item.id); setOpen(false); }}
      onClose={() => setOpen(false)}
    />
  </DialogContent>
</Dialog>
```

## API reference

### CommandPalette

An inline search-and-run panel: a combobox input over grouped, filtered results with one highlight that follows the active row.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `items` (required) | `CommandItem[]` | – | Commands to search. Every word of the query must match: anywhere in the label, at a word start in the description or keywords, or as letters in order across the label ("shpr" finds Share project). Label prefixes rank first, groups follow their best match, and the matched letters render in medium weight. |
| `placeholder` | `string` | `"Search commands"` | Input placeholder. |
| `onSelect` | `(item: CommandItem) => void` | – | Called on click or Enter. The query then clears and focus returns to the input. |
| `onClose` | `() => void` | – | Called on Escape with an empty query. Also renders an Esc close button. |
| `label` | `string` | `"Command palette"` | Visually hidden label for the input. |
| `autoFocus` | `boolean` | `false` | Focuses the search input on mount, for a palette that opens on demand. |

### CommandItem

Item type: { id: string; label: string; description?: string; group?: string; keywords?: string[]; icon?: ReactNode; shortcut?: string }. Items without a group land under Actions. A shortcut such as "⇧ N" or "Shift+N" renders one keycap per key.

No props.

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Cmd/Ctrl + K | Focuses the search input from anywhere on the page. |
| ArrowDown / ArrowUp | Moves the active result. |
| Home / End | Jumps to the first or last result. |
| Enter | Runs the active result. The first result is active as soon as results show. |
| Escape | Clears the query, or calls onClose when it is already empty. |

## Accessibility

- The input is a combobox with aria-controls and aria-activedescendant pointing at the listbox option, so focus never leaves the input.
- Results are role=option buttons with aria-selected, grouped in labelled role=group containers.
- A polite live region announces the result count, or No results, as the query changes.
- It renders inline, not in a modal. Put it in a dialog yourself if it should trap focus and close on outside click.

## Motion

- The panel rises in on a spring; the results frame springs to the list height as the query filters.
- Remaining rows glide into place when the shift is small and snap when it is a long jump; the highlight follows the active row on the snappy spring for pointer and arrow keys.
- Reduced motion removes the entrance, glides, and highlight travel.

## Responsive behavior

- It fills its container width; below 480px shortcut hints are hidden and padding tightens.
- Below 340px the footer drops its last hint.
- Results scroll inside a 342px maximum height, and long labels ellipsize.

## Performance

- Filtering is a simple substring match over label, description, group, and keywords on every keystroke.
- Rows are not virtualized and animate with layout; keep lists to a few hundred items or filter on the server.

## Notes for AI

- Use for app-wide command search. For picking a value inside a form use combobox; for a small list of actions use dropdown-menu.
- Wrap it in dialog and pass onClose to close the dialog; map onSelect to your router or action handlers by item.id.

## Related

- [Dialog](https://uiarc.dev/components/dialog/markdown): A focused surface for decisions that need attention.
- [Combobox](https://uiarc.dev/components/combobox/markdown): Search and select from a list without leaving the field.
- [Dropdown menu](https://uiarc.dev/components/dropdown-menu/markdown): A focused list of actions anchored to a trigger.
- [Expanding search](https://uiarc.dev/components/expanding-search/markdown): An icon that morphs into a search field with results beneath it.
- [Search field](https://uiarc.dev/components/search-field/markdown): A recognizable search entry point with clear affordances.

## Guidance for AI tools

Blocks are complete, self-contained screens with sample data. Replace the sample data and connect the callbacks described above. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
