# Dialog

> A focused surface for decisions that need attention.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/dialog
- Markdown: https://uiarc.dev/components/dialog/markdown
- Registry item: https://uiarc.dev/r/dialog.json
- Source file: `registry/components/dialog/dialog.tsx`
- Dependencies: @radix-ui/react-dialog, motion, lucide-react
- Keywords: modal, overlay, react dialog, animated modal, radix dialog, confirm dialog, modal window, popup dialog

## When to use

- Confirmations and decisions that must interrupt, such as Delete project.
- Short forms like rename or invite that fit in one focused panel.
- Flows where the dialog title changes between steps and should crossfade in place.

## When not to use

- Use drawer for long forms or detail panels that keep the page in context.
- Use bottom-sheet for mobile-first secondary tasks with snap heights.
- Use popover for light, non-modal content anchored to a trigger.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/dialog
pnpm dlx shadcn@latest add @uiarc/dialog
yarn dlx shadcn@latest add @uiarc/dialog
bunx --bun shadcn@latest add @uiarc/dialog
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/dialog.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-dialog motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/dialog/dialog.tsx`

   The source is in the registry item: https://uiarc.dev/r/dialog.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Dialog, DialogClose, DialogContent, DialogTrigger } from "@/components/arc/dialog/dialog";
import { Button } from "@/components/arc/button/button";

export function RenameProject() {
  return (
    <Dialog>
      <DialogTrigger asChild><Button>Rename</Button></DialogTrigger>
      <DialogContent title="Rename project" description="This changes the URL too.">
        <input defaultValue="Arc" aria-label="Project name" />
        <DialogClose asChild><Button>Save</Button></DialogClose>
      </DialogContent>
    </Dialog>
  );
}
```

## Examples

### Confirm with a custom cancel

```tsx
<Dialog open={open} onOpenChange={setOpen}>
  <DialogContent title="Delete project?" description="This cannot be undone.">
    <DialogClose asChild><Button variant="secondary">Cancel</Button></DialogClose>
    <Button onClick={remove}>Delete</Button>
  </DialogContent>
</Dialog>
```

## API reference

### Dialog

Root that tracks open state so the content can animate out. Controlled or uncontrolled.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `open` | `boolean` | – | Controlled open state. |
| `defaultOpen` | `boolean` | `false` | Initial open state when uncontrolled. |
| `onOpenChange` | `(open: boolean) => void` | – | Called when the dialog opens or closes. |
| `...props` | `ComponentPropsWithoutRef<typeof DialogPrimitive.Root>` | – | Other Radix Dialog root props, such as modal. |

### DialogTrigger

Radix Dialog.Trigger. Use asChild to wrap your own button.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `...props` | `ComponentPropsWithoutRef<typeof DialogPrimitive.Trigger>` | – | Radix trigger props, including asChild. |

### DialogContent

Portaled overlay and panel with a titled header and built-in close button.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `title` (required) | `string` | – | Dialog title, rendered as Radix Dialog.Title. Changes crossfade while open. |
| `description` | `string` | – | Optional supporting line, rendered as Dialog.Description. |
| `children` (required) | `ReactNode` | – | Body content, usually a form or actions. |
| `...props` | `ComponentPropsWithoutRef<typeof DialogPrimitive.Content>` | – | Radix content props such as className, onEscapeKeyDown, and onPointerDownOutside. |

### DialogClose

Radix Dialog.Close for custom cancel or confirm buttons.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `...props` | `ComponentPropsWithoutRef<typeof DialogPrimitive.Close>` | – | Radix close props, including asChild. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Escape | Closes the dialog and returns focus to the trigger. |
| Tab / Shift+Tab | Cycles focus within the dialog. |

## Accessibility

- Radix renders role="dialog" with aria-modal, traps focus, and restores it to the trigger on close.
- title and description are wired to aria-labelledby and aria-describedby.
- The close button carries aria-label="Close dialog".

## Motion

- The overlay fades while the panel rises 8px and scales from 0.96 on a smooth spring; closing is shorter and retargets from the current state.
- Title and description changes rise in with a soft blur.
- Reduced motion uses a plain opacity fade, including the CSS keyframe fallback for bare Radix roots.

## Responsive behavior

- The panel is min(100vw minus a 32px gutter, 440px) wide and centered, so it fits phones without extra CSS.
- Height caps at the viewport minus a gutter and the panel scrolls beyond that.

## Performance

- The overlay uses a 7px backdrop blur, which can cost frames on low-end devices over busy pages.
- Content renders in a portal only while open and unmounts after the exit animation.

## Notes for AI

- Use for decisions that must interrupt: confirmations, short forms. Use drawer for side panels, bottom-sheet for mobile-first secondary tasks, popover for light non-modal content.
- Always compose Dialog > DialogTrigger + DialogContent. Wrap your own buttons with asChild.

## Related

- [Drawer](https://uiarc.dev/components/drawer/markdown): A temporary side surface for focused work.
- [Bottom sheet](https://uiarc.dev/components/bottom-sheet/markdown): A sheet that rests at a peek or full height and follows your finger.
- [Popover](https://uiarc.dev/components/popover/markdown): A small anchored surface for contextual information.
- [Hold to confirm](https://uiarc.dev/components/hold-to-confirm/markdown): Confirm a destructive action by holding, not tapping.

## Also in overlays

- [Hover card](https://uiarc.dev/components/hover-card/markdown): Preview a person or link on hover or focus without leaving the page.
- [Tooltip](https://uiarc.dev/components/tooltip/markdown): Short supporting text for unfamiliar controls.
- [Share access](https://uiarc.dev/components/share-access/markdown): A share panel: invite people as validated email chips with suggestions, change roles from a compact menu, remove with undo, switch link access and copy the link, with every change landing in place.

## Guidance for AI tools

Dialog: A focused surface for decisions that need attention. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
