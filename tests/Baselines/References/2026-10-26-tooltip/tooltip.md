# Tooltip

> Short supporting text for unfamiliar controls.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/tooltip
- Markdown: https://uiarc.dev/components/tooltip/markdown
- Registry item: https://uiarc.dev/r/tooltip.json
- Source file: `registry/components/tooltip/tooltip.tsx`
- Dependencies: @radix-ui/react-tooltip, motion
- Keywords: hint, overlay, react tooltip, radix tooltip, icon button tooltip, hover label, animated tooltip, tooltip component

## When to use

- One-line labels on icon-only buttons in toolbars.
- Revealing the full text of truncated labels on hover or focus.
- Dense toolbars where moving between icons should show labels instantly after the first one.

## When not to use

- Use popover for anything with links or controls inside.
- Use hover-card for rich previews such as a person or link.
- Avoid it for information people must see on touch devices, where hover does not exist.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/tooltip
pnpm dlx shadcn@latest add @uiarc/tooltip
yarn dlx shadcn@latest add @uiarc/tooltip
bunx --bun shadcn@latest add @uiarc/tooltip
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/tooltip.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-tooltip motion
```

2. Copy the source into your project. Main file: `registry/components/tooltip/tooltip.tsx`

   The source is in the registry item: https://uiarc.dev/r/tooltip.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Tooltip } from "@/components/arc/tooltip/tooltip";

export function ArchiveButton() {
  return (
    <Tooltip content="Archive">
      <button type="button" aria-label="Archive">
        <ArchiveIcon />
      </button>
    </Tooltip>
  );
}
```

## API reference

### Tooltip

A short label on hover or focus that opens without delay when moving between tooltips.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `content` (required) | `ReactNode` | – | Tooltip content. Strings and numbers crossfade and resize when they change while open. |
| `children` (required) | `ReactElement` | – | The trigger. Rendered with asChild, so it must accept a ref and be focusable. |
| `side` | `"top" \| "bottom"` | `"top"` | Preferred side of the trigger. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Focusing the trigger opens the tooltip. |
| Escape | Closes the tooltip. |

## Accessibility

- Radix links the content to the trigger with aria-describedby and renders role="tooltip".
- Content is supplemental: icon-only triggers still need their own aria-label.
- Do not put interactive elements inside; use popover instead.

## Motion

- Opens after 250ms with a 3px rise from 0.97 scale; within 300ms of another tooltip it opens instantly with a fade only.
- Changing string content rises in with a blur while the bubble springs to the new size.
- Reduced motion removes the transform and keeps a 90ms opacity fade.

## Responsive behavior

- The bubble caps at 15rem and wraps longer text; Radix flips it to the other side near viewport edges.
- It opens on hover and keyboard focus, so touch users rarely see it; never hide essential information in one.

## Performance

- Each tooltip brings its own provider and mounts content in a portal only while open.
- A ResizeObserver measures the text only to spring the bubble size when content changes while open.

## Notes for AI

- Use for one-line labels on icon buttons and truncated text. Use hover-card for rich previews and popover for anything interactive.
- Each Tooltip brings its own provider; no app-level wrapper is needed.

## Related

- [Popover](https://uiarc.dev/components/popover/markdown): A small anchored surface for contextual information.
- [Hover card](https://uiarc.dev/components/hover-card/markdown): Preview a person or link on hover or focus without leaving the page.
- [Action button](https://uiarc.dev/components/action-button/markdown): A compact button for frequent toolbar actions.

## Also in overlays

- [Dialog](https://uiarc.dev/components/dialog/markdown): A focused surface for decisions that need attention.
- [Drawer](https://uiarc.dev/components/drawer/markdown): A temporary side surface for focused work.
- [Bottom sheet](https://uiarc.dev/components/bottom-sheet/markdown): A sheet that rests at a peek or full height and follows your finger.
- [Share access](https://uiarc.dev/components/share-access/markdown): A share panel: invite people as validated email chips with suggestions, change roles from a compact menu, remove with undo, switch link access and copy the link, with every change landing in place.

## Guidance for AI tools

Tooltip: Short supporting text for unfamiliar controls. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
