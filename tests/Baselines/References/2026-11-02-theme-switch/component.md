# Theme switcher

> Four smooth ways to move between light and dark appearance.

- Type: Component (actions)
- Access: Free, open source
- Page: https://uiarc.dev/components/theme-switch
- Markdown: https://uiarc.dev/components/theme-switch/markdown
- Registry item: https://uiarc.dev/r/theme-switch.json
- Source file: `registry/components/theme-switch/theme-switch.tsx`
- Dependencies: motion, lucide-react
- Keywords: theme, appearance, motion, react theme switch, dark mode toggle, light dark switch, theme toggle button, view transition theme, sun moon toggle

## When to use

- A single light and dark toggle in an app or marketing top bar.
- Pages that want to run their own view transition from the button's position.

## When not to use

- Use user-menu when theme choice, including system, belongs inside an account menu.
- Use theme-switch-rise for quiet product chrome, or theme-switch-eclipse for a dramatic showcase change.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/theme-switch
pnpm dlx shadcn@latest add @uiarc/theme-switch
yarn dlx shadcn@latest add @uiarc/theme-switch
bunx --bun shadcn@latest add @uiarc/theme-switch
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/theme-switch.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/theme-switch/theme-switch.tsx`

   The source is in the registry item: https://uiarc.dev/r/theme-switch.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ThemeSwitch } from "@/components/arc/theme-switch/theme-switch";
import type { Theme } from "@/components/arc/theme-switch/theme-switch";

export function AppearanceToggle({ theme, setTheme }: { theme: Theme; setTheme: (next: Theme) => void }) {
  return (
    <ThemeSwitch
      theme={theme}
      iconOnly
      onThemeChange={next => {
        if (!document.startViewTransition) return setTheme(next);
        document.startViewTransition(() => setTheme(next));
      }}
    />
  );
}
```

## Examples

### Reveal from the button

```tsx
<ThemeSwitch
  theme={theme}
  onThemeChange={(next, _variant, trigger) => {
    const { left, top, width, height } = trigger.getBoundingClientRect();
    document.documentElement.style.setProperty("--x", `${left + width / 2}px`);
    document.documentElement.style.setProperty("--y", `${top + height / 2}px`);
    document.startViewTransition(() => setTheme(next));
  }}
/>
```

## API reference

### ThemeSwitch

A secondary Button that swaps a sun and moon icon and reports the next theme. The page transition itself is yours to run.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `theme` (required) | `"light" \| "dark"` | – | The current theme. Drives the icon and aria-pressed. |
| `onThemeChange` (required) | `(next: Theme, variant: ThemeSwitchVariant, trigger: HTMLElement) => void` | – | Called on press with the opposite theme, the variant, and the button, so the page transition can start from it. |
| `variant` | `"reveal" \| "eclipse" \| "split" \| "rise"` | `"reveal"` | Names the page transition and sets the small icon nudge that matches it. |
| `label` | `string` | – | Accessible name. Defaults to "Switch to dark mode" or "Switch to light mode". |
| `iconOnly` | `boolean` | `false` | Hides the "Switch theme" text and renders a 38px square button. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Toggles the theme. |

## Accessibility

- Renders the library Button with aria-pressed set when the theme is dark.
- aria-label defaults to "Switch to dark mode" or "Switch to light mode"; pass label to override.
- The icons are aria-hidden.

## Motion

- Sun and moon trade places with a shared rotation, scale, and blur on a snappy spring; the icon also nudges in the direction of the chosen variant.
- A theme that arrives during hydration swaps without motion, so a dark page never spins its switch on load.
- Reduced motion swaps the icon with an instant fade and removes the nudge; skip the page transition too when reduced.

## Responsive behavior

- The labelled button fits a desktop top bar; pass iconOnly for a fixed 38px square on narrow headers and mobile.
- Hover styles apply only on hover-capable fine pointers, so touch taps do not leave a stuck hover state.

## Performance

- The button itself is tiny: one icon swap on a spring, and the first theme after hydration applies without animating.
- The page transition is yours; a full-page view transition snapshots the whole document, so keep it short and skip it for reduced motion.

## Notes for AI

- Use for a single light/dark toggle in a top bar. For light, dark, and system inside an account menu use user-menu.
- The component only reports the change. Apply the theme in onThemeChange, typically inside document.startViewTransition, and use trigger's rect as the transition origin.
- variant is passed back to onThemeChange so one handler can run the matching page transition (reveal, eclipse, split, rise).

## Related

- [Eclipse](https://uiarc.dev/components/theme-switch-eclipse/markdown): The next appearance crosses the page like an eclipse.
- [Split](https://uiarc.dev/components/theme-switch-split/markdown): The next appearance opens from a slim center seam.
- [Rise](https://uiarc.dev/components/theme-switch-rise/markdown): The next appearance rises into place.
- [User menu](https://uiarc.dev/components/user-menu/markdown): Your account, settings, theme, and sign out behind the avatar. Opens as a bottom sheet on phones.
- [Button](https://uiarc.dev/components/button/markdown): A clear, responsive action with quiet secondary states.

## Guidance for AI tools

Theme switcher: Four smooth ways to move between light and dark appearance. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
