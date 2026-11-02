# Action swap

> Swap an action label while preserving its place and intent.

- Type: Component (actions)
- Access: Free, open source
- Page: https://uiarc.dev/components/action-swap
- Markdown: https://uiarc.dev/components/action-swap/markdown
- Registry item: https://uiarc.dev/r/action-swap.json
- Source file: `registry/components/action-swap/action-swap.tsx`
- Dependencies: motion, lucide-react
- Keywords: action, motion, react toggle button, follow button, state cycling button, action swap, morphing button, send button animation, async toggle button

## When to use

- One control that cycles through related states, like Follow and Following or Send and Sent.
- Toggles that run async work on press and should show a working phase and a failure message in place.
- Short confirmations that should revert on their own after a moment, via holdFor.

## When not to use

- Use action-button for a single save or publish that ends in a success tick.
- Use button when the label never changes state.
- Use theme-switch for a light and dark toggle, since it reports the theme change for a page transition.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/action-swap
pnpm dlx shadcn@latest add @uiarc/action-swap
yarn dlx shadcn@latest add @uiarc/action-swap
bunx --bun shadcn@latest add @uiarc/action-swap
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/action-swap.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/action-swap/action-swap.tsx`

   The source is in the registry item: https://uiarc.dev/r/action-swap.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ActionSwap } from "@/components/arc/action-swap/action-swap";
import { Send, Check } from "lucide-react";

export function SendInvite() {
  return (
    <ActionSwap
      states={[
        { id: "idle", label: "Send invite", icon: <Send size={16} /> },
        { id: "sent", label: "Sent", icon: <Check size={16} />, tone: "success", pendingLabel: "Sending", holdFor: 2000 },
      ]}
      onAction={next => (next.id === "sent" ? sendInvite() : undefined)}
    />
  );
}
```

## Examples

### Follow toggle

```tsx
<ActionSwap
  states={[
    { id: "follow", label: "Follow", tone: "accent" },
    { id: "following", label: "Following", tone: "quiet", pendingLabel: "Following" },
  ]}
  onAction={next => setFollowing(next.id === "following")}
/>
```

## API reference

### ActionSwap

A compact pill that steps through a list of states on each press, morphing icon, tone, and letters together.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `states` (required) | `ActionSwapState[]` | – | Ordered states. Each press moves to the next and wraps around. Needs at least two to be enabled. |
| `value` | `string` | – | Controlled state id. |
| `defaultValue` | `string` | – | Initial state id when uncontrolled. Defaults to the first state. |
| `onValueChange` | `(value: string, state: ActionSwapState) => void` | – | Called when the control commits to a new state. |
| `onAction` | `(next: ActionSwapState, current: ActionSwapState) => void \| PromiseLike<unknown>` | – | Runs on press. Return a promise to show a working phase; a rejection shows failureLabel and stays put. |
| `failureLabel` | `string` | `"Try again"` | Label shown for 1.8s after onAction rejects or throws. |
| `size` | `"sm" \| "md" \| "lg"` | `"md"` | Height and padding. |
| `...props` | `ButtonHTMLAttributes<HTMLButtonElement>` | – | Forwarded to the button, except children, onClick, onChange, and value. |

### ActionSwapState

One state in the states array.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `id` (required) | `string` | – | Stable id used by value and onValueChange. |
| `label` (required) | `string` | – | Visible label and accessible name. |
| `icon` | `ReactNode` | – | Leading icon, sized at 16px. |
| `tone` | `"neutral" \| "accent" \| "success" \| "warning" \| "quiet"` | `"neutral"` | Fill color. Success draws its icon in with a stroke wipe. |
| `pendingLabel` | `string` | – | Shown while onAction runs on the way into this state. Without it the pill folds into a round spinner. |
| `holdFor` | `number` | – | Milliseconds to hold this state before advancing on its own, for brief confirmations. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Advances to the next state. |

## Accessibility

- Native button whose aria-label always matches the visible label, since the letters themselves are aria-hidden.
- The working phase sets aria-busy and aria-disabled, so focus stays on the button.
- A polite role="status" region announces each new label, pending label, and failure.

## Motion

- Width, padding, tone, and icon move on one morph spring; only the changed run of letters rises in from a soft blur.
- A working state without pendingLabel folds the pill into a circle; the working shape shows for at least 450ms.
- Reduced motion jumps width and fold and swaps icons and letters with an instant fade.

## Responsive behavior

- Width follows the current label, so keep labels similar in length to avoid shifting neighbours on narrow rows.
- Hover styles apply only on hover-capable fine pointers; on touch the press scale is the only feedback.

## Performance

- Each letter is its own motion span with layout position animation; keep labels short and avoid many swaps on one screen.
- A ResizeObserver tracks the label width for the morph spring.

## Notes for AI

- Pick this for one control that cycles through related states (Follow → Following, Send → Sent → Send). Use action-button for a single save with a success tick.
- Keep states few and labels short; the morph reads best between similar lengths.
- Do the work in onAction and return its promise; control value only when the state lives elsewhere.

## Related

- [Action button](https://uiarc.dev/components/action-button/markdown): A compact button for frequent toolbar actions.
- [Button](https://uiarc.dev/components/button/markdown): A clear, responsive action with quiet secondary states.
- [Copy button](https://uiarc.dev/components/copy-button/markdown): Copy a value with immediate confirmation.

## Also in buttons

- [Split button](https://uiarc.dev/components/split-button/markdown): A primary action with a menu of nearby alternatives.
- [Button group](https://uiarc.dev/components/button-group/markdown): Related actions joined into one surface with hairline dividers: a hover highlight glides between segments, the pressed one answers in place, and an attached menu can close the row.
- [Floating button group](https://uiarc.dev/components/floating-button-group/markdown): Separate soft buttons in a quiet tray, with one shared highlight that morphs from button to button as you move, and a pressed state that settles in place.
- [Expanding button group](https://uiarc.dev/components/expanding-button-group/markdown): Icon buttons in a compact group: the one you point at or focus grows to reveal its label while its neighbours slide aside, and an action confirms in place.
- [Icon morph](https://uiarc.dev/components/icon-morph/markdown): Icons that morph between states: menu to close, play to pause, copy to check, and more.
- [Confirm morph](https://uiarc.dev/components/confirm-morph/markdown): A destructive button that morphs into an inline confirmation, a spinner, and a result with undo.

## Guidance for AI tools

Action swap: Swap an action label while preserving its place and intent. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
