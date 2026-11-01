# Bottom sheet

> A sheet that rests at a peek or full height and follows your finger.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/bottom-sheet
- Markdown: https://uiarc.dev/components/bottom-sheet/markdown
- Registry item: https://uiarc.dev/r/bottom-sheet.json
- Source file: `registry/components/bottom-sheet/bottom-sheet.tsx`
- Dependencies: @radix-ui/react-dialog, motion, lucide-react
- Keywords: overlay, gesture, motion, react bottom sheet, mobile bottom sheet, draggable sheet, ios sheet, snap points sheet, detents, swipe up panel

## When to use

- Mobile-first secondary tasks such as details, filters, or share options.
- Content that benefits from a peek height before expanding to nearly full screen.
- Maps and media views where the sheet should be dragged between detents.

## When not to use

- Use dialog for interrupting decisions on any screen size.
- Use drawer for side panels on wide desktop layouts.
- Use popover for small anchored content that should not dim the page.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/bottom-sheet
pnpm dlx shadcn@latest add @uiarc/bottom-sheet
yarn dlx shadcn@latest add @uiarc/bottom-sheet
bunx --bun shadcn@latest add @uiarc/bottom-sheet
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/bottom-sheet.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-dialog motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/bottom-sheet/bottom-sheet.tsx`

   The source is in the registry item: https://uiarc.dev/r/bottom-sheet.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { BottomSheet, BottomSheetClose } from "@/components/arc/bottom-sheet/bottom-sheet";
import { Button } from "@/components/arc/button/button";

export function TripSheet() {
  return (
    <BottomSheet
      trigger={<Button>Trip details</Button>}
      title="Lisbon, 3 nights"
      description="Oct 12 to Oct 15"
      detents={[0.4, 0.9]}
    >
      <p>Flights, hotel, and bookings.</p>
      <BottomSheetClose asChild><Button variant="secondary">Done</Button></BottomSheetClose>
    </BottomSheet>
  );
}
```

## Examples

### Controlled sheet with detent tracking

```tsx
<BottomSheet
  open={open}
  onOpenChange={setOpen}
  title="Filters"
  detents={[0.5]}
  onDetentChange={index => track("detent", index)}
>
  <FilterList />
</BottomSheet>
```

## API reference

### BottomSheet

A modal sheet that rises from the bottom edge and rests at one or more detents, with drag, flick, and keyboard control.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `title` (required) | `string` | – | Sheet title, rendered as Dialog.Title. |
| `children` (required) | `ReactNode` | – | Scrollable sheet content. |
| `trigger` | `ReactNode` | – | Control that opens the sheet. Rendered with asChild; focus returns to it on close. |
| `open` | `boolean` | – | Controlled open state. |
| `defaultOpen` | `boolean` | `false` | Initial open state when uncontrolled. |
| `onOpenChange` | `(open: boolean) => void` | – | Called when the sheet opens or closes. |
| `description` | `string` | – | Supporting line, rendered as Dialog.Description. |
| `detents` | `number[]` | `[0.45, 0.92]` | Resting heights as fractions of the viewport height. |
| `initialDetent` | `number` | `0` | Index into the sorted detents the sheet opens at. |
| `onDetentChange` | `(index: number) => void` | – | Called when the sheet settles on a different detent. |
| `closeLabel` | `string` | `"Close"` | aria-label of the close button. |
| `className` | `string` | – | Class on the sheet surface. |

### BottomSheetClose

Radix Dialog.Close for buttons inside the sheet.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `...props` | `ComponentPropsWithoutRef<typeof DialogPrimitive.Close>` | – | Radix close props, including asChild. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowUp / ArrowDown | On the grabber, moves one detent up or down. |
| Home / End | On the grabber, jumps to the tallest or smallest detent. |
| Enter / Space | On the grabber, toggles between smallest and tallest detent. |
| ArrowDown / PageDown / Space | On the sheet, expands to full height first, then scrolls the content. |
| Escape | Closes the sheet. |

## Accessibility

- Built on Radix Dialog: role="dialog", focus trap, and focus return to the trigger. Initial focus lands on the sheet itself.
- The grabber is a button with aria-expanded and an Expand or Collapse sheet label.
- Detent changes are announced through a polite live region.
- Tabbing into content below the fold expands the sheet so focused elements are visible.

## Motion

- One motion value drives the sheet: drags follow the finger 1:1, rubber-band past the tallest detent, and releases project velocity to pick a detent or dismiss.
- The backdrop dim is a function of sheet position, so it tracks drags instead of running on a timer.
- Reduced motion jumps between detents and fades the sheet in and out.

## Responsive behavior

- The sheet is min(100%, 36rem) wide and detents are fractions of the dynamic viewport height, so it adapts to mobile browser chrome.
- Below 40rem the header and body use tighter side padding.
- Touch drags on the content move the sheet until it is fully open, then the content scrolls; mouse and pen drag from the header.

## Performance

- One motion value drives position and backdrop, so drags do not re-render React per frame.
- A ResizeObserver re-fits detents on viewport changes; content is not virtualized, so paginate long lists inside it.

## Notes for AI

- Use for mobile-first secondary tasks where a peek helps: details, filters, share options. Use dialog for interrupting decisions and drawer for side panels on wide layouts.
- Pass a trigger or control open yourself. Use a single-value detents array for a fixed-height sheet.

## Related

- [Drawer](https://uiarc.dev/components/drawer/markdown): A temporary side surface for focused work.
- [Dialog](https://uiarc.dev/components/dialog/markdown): A focused surface for decisions that need attention.
- [Popover](https://uiarc.dev/components/popover/markdown): A small anchored surface for contextual information.

## Also in overlays

- [Hover card](https://uiarc.dev/components/hover-card/markdown): Preview a person or link on hover or focus without leaving the page.
- [Tooltip](https://uiarc.dev/components/tooltip/markdown): Short supporting text for unfamiliar controls.
- [Share access](https://uiarc.dev/components/share-access/markdown): A share panel: invite people as validated email chips with suggestions, change roles from a compact menu, remove with undo, switch link access and copy the link, with every change landing in place.
- [Confirm popover](https://uiarc.dev/components/confirm-popover/markdown): A destructive button that grows into a small confirmation card, with the consequence, optional type-to-confirm, progress on the Confirm button, and a check or error before folding back.

## Guidance for AI tools

Bottom sheet: A sheet that rests at a peek or full height and follows your finger. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
