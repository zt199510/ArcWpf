# Action button

> A compact button for frequent toolbar actions.

- Type: Component (actions)
- Access: Free, open source
- Page: https://uiarc.dev/components/action-button
- Markdown: https://uiarc.dev/components/action-button/markdown
- Registry item: https://uiarc.dev/r/action-button.json
- Source file: `registry/components/action-button/action-button.tsx`
- Dependencies: motion, lucide-react
- Keywords: action, toolbar, react action button, async button, submit button with success state, save button animation, loading to success button, cta button with arrow, publish button

## When to use

- A single async commit such as Save, Publish, or Submit where the result should appear on the button.
- Call-to-action buttons where a trailing arrow invites the next step.
- Flows where the button should reset to idle on its own after success, via resetAfterMs.

## When not to use

- Use button with loading for plain form submits that do not need a success state.
- Use action-swap when the control toggles between lasting states.
- Use hold-to-confirm when the action is destructive and needs a deliberate press.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/action-button
pnpm dlx shadcn@latest add @uiarc/action-button
yarn dlx shadcn@latest add @uiarc/action-button
bunx --bun shadcn@latest add @uiarc/action-button
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/action-button.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/action-button/action-button.tsx`

   The source is in the registry item: https://uiarc.dev/r/action-button.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ActionButton } from "@/components/arc/action-button/action-button";

export function PublishButton() {
  return (
    <ActionButton
      label="Publish"
      pendingLabel="Publishing"
      successLabel="Published"
      onAction={publish}
      onActionError={error => toast.error(String(error))}
    />
  );
}
```

## API reference

### ActionButton

A call-to-action button with a trailing arrow that runs an async action and morphs through pending and success.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Idle label and accessible name. |
| `onAction` (required) | `() => void \| Promise<void>` | – | Runs on press. The pending state lasts until the promise settles. |
| `pendingLabel` | `string` | `"Saving"` | Label while onAction runs. |
| `successLabel` | `string` | `"Saved"` | Label after onAction resolves. |
| `resetAfterMs` | `number` | `2400` | Delay before returning to idle after success. 0 keeps the success state. |
| `onActionError` | `(error: unknown) => void` | – | Called when onAction rejects; the button returns to idle. |
| `...props` | `ButtonHTMLAttributes<HTMLButtonElement>` | – | Forwarded to the button, except onClick. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Runs the action. |

## Accessibility

- Native button; the visible label is aria-hidden and a visually hidden copy of label names it.
- Pending sets aria-busy and aria-disabled instead of disabled, so keyboard focus stays through the save.
- A role="status" region announces the pending and success labels.

## Motion

- Changed letters rise from a soft blur while the width springs to fit; the arrow leaves forward and a check draws itself in.
- Presses scale to about 0.97 on a snappy spring.
- Reduced motion drops the press scale, width spring, and stroke draw, and swaps with an instant fade.

## Responsive behavior

- Width springs to fit pending and success labels, so keep them close in length inside tight toolbars.
- Hover shadow applies only on hover-capable fine pointers; touch gets the press scale.

## Performance

- Every letter is a motion span with layout position animation plus a small blur; fine for a few buttons, not for dense tables.
- A ResizeObserver drives the width spring.

## Notes for AI

- Use for a single async commit (save, publish, submit) where the result should show on the button itself.
- Prefer button with loading for plain forms, and action-swap when the control toggles between lasting states.
- Return the real promise from onAction; handle errors in onActionError since the button just resets.

## Related

- [Button](https://uiarc.dev/components/button/markdown): A clear, responsive action with quiet secondary states.
- [Action swap](https://uiarc.dev/components/action-swap/markdown): Swap an action label while preserving its place and intent.
- [Hold to confirm](https://uiarc.dev/components/hold-to-confirm/markdown): Confirm a destructive action by holding, not tapping.
- [Toast](https://uiarc.dev/components/toast/markdown): Brief confirmation for a completed background action.

## Also in buttons

- [Split button](https://uiarc.dev/components/split-button/markdown): A primary action with a menu of nearby alternatives.
- [Button group](https://uiarc.dev/components/button-group/markdown): Related actions joined into one surface with hairline dividers: a hover highlight glides between segments, the pressed one answers in place, and an attached menu can close the row.
- [Floating button group](https://uiarc.dev/components/floating-button-group/markdown): Separate soft buttons in a quiet tray, with one shared highlight that morphs from button to button as you move, and a pressed state that settles in place.
- [Expanding button group](https://uiarc.dev/components/expanding-button-group/markdown): Icon buttons in a compact group: the one you point at or focus grows to reveal its label while its neighbours slide aside, and an action confirms in place.
- [Copy button](https://uiarc.dev/components/copy-button/markdown): Copy a value with immediate confirmation.
- [Icon morph](https://uiarc.dev/components/icon-morph/markdown): Icons that morph between states: menu to close, play to pause, copy to check, and more.
- [Confirm morph](https://uiarc.dev/components/confirm-morph/markdown): A destructive button that morphs into an inline confirmation, a spinner, and a result with undo.

## Guidance for AI tools

Action button: A compact button for frequent toolbar actions. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
