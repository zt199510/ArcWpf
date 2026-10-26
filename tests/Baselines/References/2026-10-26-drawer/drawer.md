# Drawer

> A temporary side surface for focused work.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/drawer
- Markdown: https://uiarc.dev/components/drawer/markdown
- Registry item: https://uiarc.dev/r/drawer.json
- Source file: `registry/components/drawer/drawer.tsx`
- Dependencies: @radix-ui/react-dialog, motion, lucide-react
- Keywords: overlay, surface, react drawer, side panel, slide over panel, sheet component, draggable drawer, radix dialog drawer, filters drawer

## When to use

- Side panels for filters, settings, or record details that keep the page in context.
- Forms that are too long for a dialog but should not leave the current view.
- Panels from any edge, via side, with drag-to-dismiss on the header.

## When not to use

- Use dialog for short decisions and confirmations.
- Use bottom-sheet for mobile-first sheets with snap points.
- Use popover for small anchored content that does not need a modal overlay.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/drawer
pnpm dlx shadcn@latest add @uiarc/drawer
yarn dlx shadcn@latest add @uiarc/drawer
bunx --bun shadcn@latest add @uiarc/drawer
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/drawer.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-dialog motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/drawer/drawer.tsx`

   The source is in the registry item: https://uiarc.dev/r/drawer.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Drawer, DrawerTrigger, DrawerContent, DrawerClose } from "@/components/arc/drawer/drawer";
import { Button } from "@/components/arc/button/button";

export function FiltersDrawer() {
  return (
    <Drawer>
      <DrawerTrigger asChild><Button variant="secondary">Filters</Button></DrawerTrigger>
      <DrawerContent title="Filters" description="Narrow the list of projects.">
        <FilterForm />
        <DrawerClose asChild><Button>Apply</Button></DrawerClose>
      </DrawerContent>
    </Drawer>
  );
}
```

## API reference

### Drawer

Root. A Radix Dialog root that keeps the panel mounted while it slides out and closes it after a drag.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `open` | `boolean` | – | Controlled open state. |
| `defaultOpen` | `boolean` | `false` | Initial state when uncontrolled. |
| `onOpenChange` | `(open: boolean) => void` | – | Called when the drawer opens or closes. |
| `...props` | `ComponentPropsWithoutRef<typeof DialogPrimitive.Root>` | – | Other Radix Dialog root props, such as modal and children. |

### DrawerTrigger

Radix Dialog.Trigger. Use asChild to wrap your own button.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `...props` | `ComponentPropsWithoutRef<typeof DialogPrimitive.Trigger>` | – | Radix trigger props, including asChild. |

### DrawerContent

Overlay and panel with a titled header that doubles as the drag handle, a close button, and a scrolling body.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `title` (required) | `string` | – | Rendered as the dialog title. A new title rises in while open. |
| `description` | `string` | – | Rendered as the dialog description under the title. |
| `side` | `"left" \| "right" \| "top" \| "bottom"` | `"right"` | Edge the panel attaches to and slides from. |
| `children` (required) | `ReactNode` | – | Body content. |
| `...props` | `ComponentPropsWithoutRef<typeof DialogPrimitive.Content>` | – | Radix content props such as className, onInteractOutside, and onEscapeKeyDown. |

### DrawerClose

Radix Dialog.Close for extra close buttons in the body or footer.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `...props` | `ComponentPropsWithoutRef<typeof DialogPrimitive.Close>` | – | Radix close props, including asChild. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Escape | Closes the drawer and returns focus to the trigger. |
| Tab / Shift+Tab | Cycles focus within the panel while it is open. |

## Accessibility

- Radix Dialog provides role="dialog", aria-modal, focus trapping, and focus return.
- title and description are wired to Dialog.Title and Dialog.Description, so the panel is always named.
- The header close button is labelled "Close drawer"; dragging is optional and never the only way to close.

## Motion

- The panel springs in from its edge and leaves faster on a tween; a drag past a third of the panel or a quick flick closes it and keeps the release velocity.
- Dragging the header away from the edge rubber-bands; the overlay fades in and out.
- Reduced motion disables dragging and replaces the slide with a short opacity fade.

## Responsive behavior

- Left and right panels are min(30rem, 100vw minus a gutter) wide; below 40rem they grow to nearly full width with tighter padding.
- Top and bottom panels span the full width and cap at min(32rem, 100dvh).
- The header is the drag handle with touch-action none, so a touch drag moves the panel while the body still scrolls normally.

## Performance

- The overlay uses a 4px backdrop blur, which can cost frames on low-end devices over busy pages.
- Drag runs on motion pan handlers with no React re-render per frame; the panel stays mounted only while sliding out.

## Notes for AI

- Use for side panels with forms, filters, settings, or detail views that keep the page in context. Use dialog for short decisions and bottom-sheet for mobile-first sheets.
- Always compose Drawer as the root; under a bare Radix Dialog root the panel falls back to CSS keyframes and loses drag.
- Control open when the drawer must close after an async submit.

## Related

- [Dialog](https://uiarc.dev/components/dialog/markdown): A focused surface for decisions that need attention.
- [Bottom sheet](https://uiarc.dev/components/bottom-sheet/markdown): A sheet that rests at a peek or full height and follows your finger.
- [Popover](https://uiarc.dev/components/popover/markdown): A small anchored surface for contextual information.
- [Button](https://uiarc.dev/components/button/markdown): A clear, responsive action with quiet secondary states.

## Also in overlays

- [Hover card](https://uiarc.dev/components/hover-card/markdown): Preview a person or link on hover or focus without leaving the page.
- [Tooltip](https://uiarc.dev/components/tooltip/markdown): Short supporting text for unfamiliar controls.
- [Share access](https://uiarc.dev/components/share-access/markdown): A share panel: invite people as validated email chips with suggestions, change roles from a compact menu, remove with undo, switch link access and copy the link, with every change landing in place.

## Guidance for AI tools

Drawer: A temporary side surface for focused work. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
