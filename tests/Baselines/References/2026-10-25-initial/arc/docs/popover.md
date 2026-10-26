# Popover

> A small anchored surface for contextual information.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/popover
- Markdown: https://uiarc.dev/components/popover/markdown
- Registry item: https://uiarc.dev/r/popover.json
- Source file: `registry/components/popover/popover.tsx`
- Dependencies: @radix-ui/react-popover
- Keywords: overlay, menu, react popover, radix popover, floating panel, click popover, animated popover, anchored popup

## When to use

- Click-opened panels with interactive content, like share settings or a small filter form.
- Non-modal helpers that should stay open while people interact with the rest of the page.
- Custom pickers built from your own controls anchored to a button.

## When not to use

- Use tooltip for short hover labels.
- Use hover-card for read-only previews that open on hover.
- Use dialog when the choice must block the page, and dropdown-menu for a list of commands.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/popover
pnpm dlx shadcn@latest add @uiarc/popover
yarn dlx shadcn@latest add @uiarc/popover
bunx --bun shadcn@latest add @uiarc/popover
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/popover.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-popover
```

2. Copy the source into your project. Main file: `registry/components/popover/popover.tsx`

   The source is in the registry item: https://uiarc.dev/r/popover.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Popover, PopoverContent, PopoverTrigger } from "@/components/arc/popover/popover";
import { Button } from "@/components/arc/button/button";

export function ShareMenu() {
  return (
    <Popover>
      <PopoverTrigger asChild><Button variant="secondary">Share</Button></PopoverTrigger>
      <PopoverContent>
        <p>Anyone with the link can view.</p>
      </PopoverContent>
    </Popover>
  );
}
```

## API reference

### Popover

Radix Popover.Root. Holds open state.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `open` | `boolean` | – | Controlled open state. |
| `defaultOpen` | `boolean` | `false` | Initial open state when uncontrolled. |
| `onOpenChange` | `(open: boolean) => void` | – | Called when the popover opens or closes. |
| `modal` | `boolean` | `false` | Traps focus and blocks outside interaction when true. |

### PopoverTrigger

Radix trigger that opts out of press-scale so the panel does not shift on open.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `...props` | `ComponentPropsWithoutRef<typeof PopoverPrimitive.Trigger>` | – | Radix trigger props, including asChild and ref. |

### PopoverContent

Portaled floating panel anchored to the trigger.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `align` | `"start" \| "center" \| "end"` | `"start"` | Alignment against the trigger. |
| `sideOffset` | `number` | `6` | Gap from the trigger in px. |
| `collisionPadding` | `number` | `10` | Minimum distance from viewport edges in px. |
| `...props` | `ComponentPropsWithoutRef<typeof PopoverPrimitive.Content>` | – | Radix content props such as side, className, and onOpenAutoFocus. |

### PopoverClose

Radix Popover.Close for an explicit dismiss button.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `...props` | `ComponentPropsWithoutRef<typeof PopoverPrimitive.Close>` | – | Radix close props, including asChild. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Opens the popover from the trigger. |
| Escape | Closes the popover and returns focus to the trigger. |

## Accessibility

- Radix sets aria-expanded, aria-controls, and aria-haspopup="dialog" on the trigger.
- Focus moves into the content on open and back to the trigger on close.
- Content is non-modal by default; give it a heading or aria-label when it holds controls.

## Motion

- CSS transitions: the panel fades and settles from 5px toward its trigger at 0.97 scale on a spring; it leaves in 140ms.
- Transitions instead of keyframes, so a reopen mid-close reverses from where the panel is.
- Reduced motion drops the transform and keeps a short opacity fade.

## Responsive behavior

- The panel is at least 12rem and at most min(22rem, 100vw minus 20px), so it never overflows a phone screen.
- Radix collision handling keeps it 10px from viewport edges by default and flips sides when needed.

## Performance

- Pure CSS transitions with no motion runtime; content mounts in a portal only while open.

## Notes for AI

- Use for click-opened, non-modal panels with interactive content. Use tooltip for short hover labels, hover-card for read-only previews, dialog when the choice must block.
- Compose Popover > PopoverTrigger asChild + PopoverContent. Pass side for placement.

## Related

- [Tooltip](https://uiarc.dev/components/tooltip/markdown): Short supporting text for unfamiliar controls.
- [Hover card](https://uiarc.dev/components/hover-card/markdown): Preview a person or link on hover or focus without leaving the page.
- [Dialog](https://uiarc.dev/components/dialog/markdown): A focused surface for decisions that need attention.
- [Dropdown menu](https://uiarc.dev/components/dropdown-menu/markdown): A focused list of actions anchored to a trigger.

## Also in overlays

- [Drawer](https://uiarc.dev/components/drawer/markdown): A temporary side surface for focused work.
- [Bottom sheet](https://uiarc.dev/components/bottom-sheet/markdown): A sheet that rests at a peek or full height and follows your finger.
- [Share access](https://uiarc.dev/components/share-access/markdown): A share panel: invite people as validated email chips with suggestions, change roles from a compact menu, remove with undo, switch link access and copy the link, with every change landing in place.

## Guidance for AI tools

Popover: A small anchored surface for contextual information. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
