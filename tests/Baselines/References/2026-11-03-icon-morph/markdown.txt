# Icon morph

> Icons that morph between states: menu to close, play to pause, copy to check, and more.

- Type: Component (actions)
- Access: Free, open source
- Page: https://uiarc.dev/components/icon-morph
- Markdown: https://uiarc.dev/components/icon-morph/markdown
- Registry item: https://uiarc.dev/r/icon-morph.json
- Source file: `registry/components/icon-morph/icon-morph.tsx`
- Dependencies: motion
- Keywords: actions, new, react icon morph, animated icon, hamburger to close, play pause morph, copy check icon, sun moon toggle icon, like heart animation, morphing svg icon

## When to use

- Icon buttons that toggle between two states, such as a hamburger menu or a play control.
- Copy and add actions that should confirm with a check in place.
- Like and notify toggles that deserve a small moment of delight.

## When not to use

- Use copy-button for a complete copy-to-clipboard control with its own feedback.
- Use theme-switch for a labelled light and dark toggle wired to the page theme.
- Use a plain lucide icon when the icon never changes state.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/icon-morph
pnpm dlx shadcn@latest add @uiarc/icon-morph
yarn dlx shadcn@latest add @uiarc/icon-morph
bunx --bun shadcn@latest add @uiarc/icon-morph
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/icon-morph.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/icon-morph/icon-morph.tsx`

   The source is in the registry item: https://uiarc.dev/r/icon-morph.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { IconMorph } from "@/components/arc/icon-morph/icon-morph";

export function MenuToggle() {
  const [open, setOpen] = useState(false);
  return (
    <button type="button" aria-label="Menu" aria-expanded={open} onClick={() => setOpen(value => !value)}>
      <IconMorph name="menu" state={open} size={20} />
    </button>
  );
}
```

## API reference

### IconMorph

One entry point for eight two-state icons that morph instead of swapping. Pick one by name and drive it with state.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `name` (required) | `"menu" \| "play" \| "plus" \| "copy" \| "theme" \| "chevron" \| "bell" \| "heart"` | – | Which icon to render. |
| `state` | `boolean` | `false` | The second state: menu to close, play to pause, plus or copy to check, sun to moon, chevron turned, bell on, heart liked. |
| `size` | `number` | `24` | Rendered size in px. |
| `strokeWidth` | `number` | `1.75` | Stroke width in the 24 unit grid. |
| `...props` | `Omit<SVGProps<SVGSVGElement>, "ref" \| "children">` | – | Forwarded to the svg. |

### MenuIcon

Menu to close: outer lines rotate into an X and the middle line folds away. Takes the same props as IconMorph without name.

No props.

### PlayPauseIcon

Play to pause: the triangle splits into two halves that straighten into bars.

No props.

### PlusCheckIcon

Plus to check: each bar swings its ends into one leg of the check.

No props.

### CopyCheckIcon

Copy to check: the sheets tuck away and a check draws in.

No props.

### SunMoonIcon

Sun to moon: the disc grows while a shadow carves the crescent and the rays fold in.

No props.

### ChevronIcon

A chevron that turns on a spring, for disclosure and sort controls.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `turn` | `number` | `180` | Degrees the chevron turns when state is true. 180 flips down to up; 90 turns right to down. |
| `pointing` | `"down" \| "right"` | `"down"` | Where the chevron points at rest. |

### BellIcon

Notifications: turning on rings the bell with a lagging clapper; turning off strikes it through.

No props.

### HeartIcon

Like: the heart fills with a pop and a ring and sparks burst once. Liked color is --icon-morph-like, default --danger.

No props.

## Accessibility

- Every icon renders aria-hidden and focusable="false"; put it inside a button that carries the accessible name.
- Reflect the state on the button with aria-pressed, aria-expanded, or a changing label, since the icon itself says nothing.

## Motion

- Lines rotate and slide, and shapes interpolate their path data, all on interruptible springs, so fast toggling reverses from wherever the icon is.
- The bell ring and heart burst are one-shot moments that replay on each turn on.
- Reduced motion makes every morph instant and skips the ring and burst.

## Responsive behavior

- Icons are fixed-size SVGs on a 24 unit grid; set size per breakpoint from the parent.
- Pair with a hit area of at least 40px on touch; the icon itself adds none.

## Performance

- Each icon animates a few SVG attributes with motion; many on one screen are cheap.
- SunMoonIcon and BellIcon use an SVG mask with a per-instance id from useId.

## Notes for AI

- Use inside icon buttons whose meaning flips between two states: menu and close, play and pause, copy and copied, like and liked.
- Import the specific icon (HeartIcon, ChevronIcon) when only one is needed; IconMorph is a name switch over the same components.
- Icons use currentColor, so color them from the parent button.
- For a whole button that changes label and state use action-swap; for theme switching with a label use theme-switch.

## Related

- [Action swap](https://uiarc.dev/components/action-swap/markdown): Swap an action label while preserving its place and intent.
- [Copy button](https://uiarc.dev/components/copy-button/markdown): Copy a value with immediate confirmation.
- [Theme switcher](https://uiarc.dev/components/theme-switch/markdown): Four smooth ways to move between light and dark appearance.
- [Reactions](https://uiarc.dev/components/reactions/markdown): Acknowledge a post in one tap, with counts that roll.
- [Button](https://uiarc.dev/components/button/markdown): A clear, responsive action with quiet secondary states.

## Also in buttons

- [Action button](https://uiarc.dev/components/action-button/markdown): A compact button for frequent toolbar actions.
- [Split button](https://uiarc.dev/components/split-button/markdown): A primary action with a menu of nearby alternatives.
- [Button group](https://uiarc.dev/components/button-group/markdown): Related actions joined into one surface with hairline dividers: a hover highlight glides between segments, the pressed one answers in place, and an attached menu can close the row.
- [Floating button group](https://uiarc.dev/components/floating-button-group/markdown): Separate soft buttons in a quiet tray, with one shared highlight that morphs from button to button as you move, and a pressed state that settles in place.
- [Expanding button group](https://uiarc.dev/components/expanding-button-group/markdown): Icon buttons in a compact group: the one you point at or focus grows to reveal its label while its neighbours slide aside, and an action confirms in place.
- [Confirm morph](https://uiarc.dev/components/confirm-morph/markdown): A destructive button that morphs into an inline confirmation, a spinner, and a result with undo.

## Guidance for AI tools

Icon morph: Icons that morph between states: menu to close, play to pause, copy to check, and more. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
