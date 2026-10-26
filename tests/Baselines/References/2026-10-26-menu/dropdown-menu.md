# Dropdown menu

> A focused list of actions anchored to a trigger.

- Type: Component (actions)
- Access: Free, open source
- Page: https://uiarc.dev/components/dropdown-menu
- Markdown: https://uiarc.dev/components/dropdown-menu/markdown
- Registry item: https://uiarc.dev/r/dropdown-menu.json
- Source file: `registry/components/dropdown-menu/dropdown-menu.tsx`
- Dependencies: @radix-ui/react-dropdown-menu, motion, lucide-react
- Keywords: menu, action, react dropdown menu, actions menu, menu button, radix dropdown menu, animated dropdown, more actions menu, kebab menu

## When to use

- A list of commands behind a labelled button, such as Rename, Duplicate, and Delete.
- Row or card actions where a visible trigger is clearer than right-click.
- Short grouped menus with separators and one destructive item at the end.

## When not to use

- Use split-button when one command is the default.
- Use context-menu for right-click actions on content.
- Use user-menu for the account menu, and command-palette for searching many commands.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/dropdown-menu
pnpm dlx shadcn@latest add @uiarc/dropdown-menu
yarn dlx shadcn@latest add @uiarc/dropdown-menu
bunx --bun shadcn@latest add @uiarc/dropdown-menu
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/dropdown-menu.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-dropdown-menu motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/dropdown-menu/dropdown-menu.tsx`

   The source is in the registry item: https://uiarc.dev/r/dropdown-menu.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { DropdownMenu } from "@/components/arc/dropdown-menu/dropdown-menu";
import { Pencil, Copy, Trash2 } from "lucide-react";

export function ProjectActions() {
  return (
    <DropdownMenu
      label="Actions"
      items={[
        { label: "Rename", icon: <Pencil size={15} />, onSelect: rename },
        { label: "Duplicate", icon: <Copy size={15} />, onSelect: duplicate },
        { label: "Delete", icon: <Trash2 size={15} />, onSelect: remove, destructive: true, separatorBefore: true },
      ]}
    />
  );
}
```

## API reference

### DropdownMenu

A labelled trigger that opens a Radix menu with a single highlight gliding between items.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Trigger text. A new label rises in while the trigger width springs. |
| `items` (required) | `DropdownItem[]` | – | Items: { label, onSelect?, disabled?, icon?, destructive?, separatorBefore? }. Labels double as keys, so keep them unique. |
| `icon` | `ReactNode` | – | Leading icon in the trigger. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space / ArrowDown | Opens the menu from the trigger. |
| ArrowDown / ArrowUp | Moves between items, looping at the ends. |
| Home / End | Jumps to the first or last item. |
| Escape | Closes the menu and returns focus to the trigger. |

## Accessibility

- Built on Radix DropdownMenu: the trigger gets aria-haspopup and aria-expanded, items get menuitem roles and typeahead.
- Icons and the highlight are aria-hidden; destructive items are marked by color and should say what they destroy.
- Disabled items are skipped by the keyboard.

## Motion

- The highlight glides between items on a snappy spring for the pointer and jumps instantly for the keyboard.
- The menu scales in from the trigger and items fade in with a short stagger.
- Reduced motion removes the menu transform and highlight glide, keeping a quick opacity fade.

## Responsive behavior

- The trigger label ellipsizes within max-width 100%, so it shrinks inside narrow toolbars instead of overflowing.
- The menu uses Radix collision handling with 12px padding and a 12rem minimum width, flipping away from viewport edges.
- Hover styles apply only on fine pointers; touch taps select without a stuck highlight.

## Performance

- Menu content renders in a portal only while open; items are not virtualized, so keep lists short.
- The menu has a translucent backdrop blur, which drops to a solid surface under prefers-reduced-transparency.

## Notes for AI

- Default choice for a list of commands behind a button. Use split-button when one command is the default, and context-menu for right-click on content.
- Group items with separatorBefore; put destructive items last.
- Items are data, not children, so build the array from your actions.

## Related

- [Split button](https://uiarc.dev/components/split-button/markdown): A primary action with a menu of nearby alternatives.
- [Context menu](https://uiarc.dev/components/context-menu/markdown): Secondary actions kept close to the selected object.
- [User menu](https://uiarc.dev/components/user-menu/markdown): Your account, settings, theme, and sign out behind the avatar. Opens as a bottom sheet on phones.
- [Popover](https://uiarc.dev/components/popover/markdown): A small anchored surface for contextual information.

## Guidance for AI tools

Dropdown menu: A focused list of actions anchored to a trigger. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

