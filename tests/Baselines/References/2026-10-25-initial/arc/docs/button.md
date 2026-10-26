# Button

> A clear, responsive action with quiet secondary states.

- Type: Component (actions)
- Access: Free, open source
- Page: https://uiarc.dev/components/button
- Markdown: https://uiarc.dev/components/button/markdown
- Registry item: https://uiarc.dev/r/button.json
- Source file: `registry/components/button/button.tsx`
- Dependencies: motion
- Keywords: action, control, react button, animated button, loading button, button with spinner, button press animation, morphing button label, primary button

## When to use

- Any single action on a page, form, or dialog, such as Save, Continue, or Cancel.
- Actions whose label changes in place, like Save to Saved, where the width should spring instead of jump.
- Short async work where a spinner on the button is enough feedback, via loading.

## When not to use

- Use action-button when the button itself should show pending and success states after an async commit.
- Use split-button when one default action has two to five close variants.
- Use hold-to-confirm for destructive actions that need more than a single click.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/button
pnpm dlx shadcn@latest add @uiarc/button
yarn dlx shadcn@latest add @uiarc/button
bunx --bun shadcn@latest add @uiarc/button
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/button.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/button/button.tsx`

   The source is in the registry item: https://uiarc.dev/r/button.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Button } from "@/components/arc/button/button";

export function SaveBar() {
  return (
    <Button variant="primary" loading={saving} onClick={save}>
      Save changes
    </Button>
  );
}
```

## Examples

### Label that morphs after saving

```tsx
<Button variant="secondary" loading={saving} onClick={save}>
  {saved ? "Saved" : "Save draft"}
</Button>
```

## API reference

### Button

A native button with press feedback and a label that morphs its width when the content changes.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `variant` | `"primary" \| "secondary" \| "ghost" \| "danger"` | `"primary"` | Visual weight. Use one primary action per surface. |
| `size` | `"sm" \| "md" \| "lg"` | `"md"` | Height and padding. |
| `loading` | `boolean` | `false` | Shows a spinner, sets aria-busy, and swallows clicks while keeping focus. |
| `...props` | `ButtonHTMLAttributes<HTMLButtonElement>` | – | Forwarded to the underlying button, including ref, disabled, type, and onClick. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Activates the button. |

## Accessibility

- Renders a native button, so role and focus come for free.
- Loading uses aria-busy and aria-disabled instead of disabled, so keyboard focus is not lost mid-action.
- Icon-only buttons need an aria-label.
- Disabled primary buttons fade as a whole. Disabled secondary and danger buttons keep their border and take a muted fill and label, and disabled ghost buttons take a muted label, so an unavailable action still reads as a button.

## Motion

- Presses scale to about 0.97 on a snappy spring; icon-sized buttons press slightly deeper.
- A new label crossfades with a short blur while the width springs to fit.
- Reduced motion drops the press scale and swaps labels with a plain fade.

## Responsive behavior

- Size is fixed by the size prop; the button never changes layout by breakpoint, so pick lg for primary touch targets.
- Hover styles apply only on hover-capable fine pointers, so taps on touch screens do not stick in a hover state.

## Performance

- A ResizeObserver measures the label so the width can spring; one per button is cheap, but avoid hundreds in a long list.
- Label changes animate opacity, transform, and a small blur only, with no layout thrash beyond the width spring.

## Notes for AI

- Default choice for any single action. Use action-button for dense icon toolbars and split-button when one action has close alternatives.
- Pass a changing label (Save → Saved) as children to get the width morph for free.
- Wrap in a Radix trigger with asChild; the press scale turns off automatically for popup anchors.

## Related

- [Action button](https://uiarc.dev/components/action-button/markdown): A compact button for frequent toolbar actions.
- [Split button](https://uiarc.dev/components/split-button/markdown): A primary action with a menu of nearby alternatives.
- [Copy button](https://uiarc.dev/components/copy-button/markdown): Copy a value with immediate confirmation.
- [Hold to confirm](https://uiarc.dev/components/hold-to-confirm/markdown): Confirm a destructive action by holding, not tapping.

## Also in buttons

- [Button group](https://uiarc.dev/components/button-group/markdown): Related actions joined into one surface with hairline dividers: a hover highlight glides between segments, the pressed one answers in place, and an attached menu can close the row.
- [Floating button group](https://uiarc.dev/components/floating-button-group/markdown): Separate soft buttons in a quiet tray, with one shared highlight that morphs from button to button as you move, and a pressed state that settles in place.
- [Expanding button group](https://uiarc.dev/components/expanding-button-group/markdown): Icon buttons in a compact group: the one you point at or focus grows to reveal its label while its neighbours slide aside, and an action confirms in place.
- [Action swap](https://uiarc.dev/components/action-swap/markdown): Swap an action label while preserving its place and intent.
- [Icon morph](https://uiarc.dev/components/icon-morph/markdown): Icons that morph between states: menu to close, play to pause, copy to check, and more.
- [Confirm morph](https://uiarc.dev/components/confirm-morph/markdown): A destructive button that morphs into an inline confirmation, a spinner, and a result with undo.

## Guidance for AI tools

Button: A clear, responsive action with quiet secondary states. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
