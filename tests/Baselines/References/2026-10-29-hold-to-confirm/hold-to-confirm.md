# Hold to confirm

> Confirm a destructive action by holding, not tapping.

- Type: Component (actions)
- Access: Free, open source
- Page: https://uiarc.dev/components/hold-to-confirm
- Markdown: https://uiarc.dev/components/hold-to-confirm/markdown
- Registry item: https://uiarc.dev/r/hold-to-confirm.json
- Source file: `registry/components/hold-to-confirm/hold-to-confirm.tsx`
- Dependencies: motion, lucide-react
- Keywords: hold, long press, confirm, react hold to confirm, press and hold button, long press delete, hold to delete, destructive action button, confirm without dialog

## When to use

- Destructive actions like delete or revoke where the consequence is already visible on screen.
- Replacing a confirm dialog with a deliberate press in compact settings rows.
- Actions that should not fire on an accidental tap on touch devices.

## When not to use

- Use dialog when people need to read what will happen before confirming.
- Use button or action-button for safe, easily undone actions.
- Use swipe-actions for delete in touch lists.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/hold-to-confirm
pnpm dlx shadcn@latest add @uiarc/hold-to-confirm
yarn dlx shadcn@latest add @uiarc/hold-to-confirm
bunx --bun shadcn@latest add @uiarc/hold-to-confirm
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/hold-to-confirm.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/hold-to-confirm/hold-to-confirm.tsx`

   The source is in the registry item: https://uiarc.dev/r/hold-to-confirm.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { HoldToConfirm } from "@/components/arc/hold-to-confirm/hold-to-confirm";

export function DeleteProject({ onDelete }: { onDelete: () => void }) {
  return (
    <HoldToConfirm
      label="Hold to delete project"
      confirmedLabel="Deleted"
      duration={1500}
      onConfirm={onDelete}
    />
  );
}
```

## Examples

### Keep holding hint

```tsx
const [holding, setHolding] = useState(false);

<HoldToConfirm label="Hold to revoke key" onConfirm={revoke} onHoldChange={setHolding} />
<p aria-hidden="true">{holding ? "Keep holding" : ""}</p>
```

## API reference

### HoldToConfirm

A button that commits only after a press is held, with a fill that tracks the hold and rewinds on early release.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Instruction and action, such as "Hold to delete project". |
| `onConfirm` (required) | `() => void` | – | Called once when the hold completes. |
| `confirmedLabel` | `string` | `"Done"` | Label after the hold completes. |
| `duration` | `number` | `1200` | Hold length in milliseconds. |
| `icon` | `ReactNode` | `<Trash2 />` | Leading icon. Replaced by a drawn check when done. |
| `tone` | `"danger" \| "neutral"` | `"danger"` | Fill color. |
| `confirmed` | `boolean` | – | Controls the done state. Set back to false to reset; leave undefined to let the button manage it. |
| `onHoldChange` | `(holding: boolean) => void` | – | Reports when a hold starts and ends, for hints like "Keep holding". |
| `...props` | `ButtonHTMLAttributes<HTMLButtonElement>` | – | Forwarded to the button, except children and onClick. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Space / Enter (hold) | Fills the button; releasing early rewinds it. |
| Tab away | Cancels an active hold. |

## Accessibility

- aria-describedby points to a hidden hint: "Press and hold for N seconds to confirm. With a keyboard, hold Space or Enter."
- aria-label tracks the visible label; a role="status" region announces confirmedLabel.
- Once done the button sets aria-disabled; the text is drawn twice so it changes color exactly at the fill edge.

## Motion

- The fill advances on a linear timeline while held, rewinds on a smooth spring when released, and resumes from where it was on a quick re-press.
- The button presses to about 0.97; on completion the label morphs, the width springs, and a check draws itself in. Touch devices get a short vibration.
- Reduced motion drops the press scale and rewind spring and swaps label and icon with a fade; the fill still tracks the hold.

## Responsive behavior

- Uses pointer events with capture, so mouse, pen, and touch holds all work; sliding more than 24px off the button cancels the hold.
- Touch holds trigger a short vibration on completion where the device supports it.
- Keyboard users hold Space or Enter, and a hidden hint explains the duration.

## Performance

- The fill runs on a single linear motion animation, not a React state update per frame.
- The label is drawn twice to change color at the fill edge; a ResizeObserver handles the width spring on completion.

## Notes for AI

- Use for destructive or hard-to-undo actions when the consequence is already visible on screen. Use dialog when people need to read what will happen first.
- Write the label as an instruction ("Hold to …"); pass confirmed to reset it after an undo or a failed request.
- Pair with onHoldChange to show a nearby hint while the press is held.

## Related

- [Button](https://uiarc.dev/components/button/markdown): A clear, responsive action with quiet secondary states.
- [Action button](https://uiarc.dev/components/action-button/markdown): A compact button for frequent toolbar actions.
- [Dialog](https://uiarc.dev/components/dialog/markdown): A focused surface for decisions that need attention.
- [Swipe actions](https://uiarc.dev/components/swipe-actions/markdown): Reveal row actions with a swipe, or from the same actions in a menu.

## Also in gestures

- [Reactions](https://uiarc.dev/components/reactions/markdown): Acknowledge a post in one tap, with counts that roll.

## Guidance for AI tools

Hold to confirm: Confirm a destructive action by holding, not tapping. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
