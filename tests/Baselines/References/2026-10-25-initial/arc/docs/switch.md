# Switch

> A tactile toggle for settings that take effect immediately.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/switch
- Markdown: https://uiarc.dev/components/switch/markdown
- Registry item: https://uiarc.dev/r/switch.json
- Source file: `registry/components/switch/switch.tsx`
- Dependencies: @radix-ui/react-switch, motion
- Keywords: toggle, settings, react switch, toggle switch, ios toggle, radix switch, settings toggle, animated switch

## When to use

- Settings that take effect as soon as they are flipped, like notifications.
- Settings lists where each row is one on and off preference.

## When not to use

- Use checkbox when the choice waits for a submit button.
- Use segmented-control for a choice between named options.
- Use theme-switch for a light and dark toggle.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/switch
pnpm dlx shadcn@latest add @uiarc/switch
yarn dlx shadcn@latest add @uiarc/switch
bunx --bun shadcn@latest add @uiarc/switch
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/switch.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-switch motion
```

2. Copy the source into your project. Main file: `registry/components/switch/switch.tsx`

   The source is in the registry item: https://uiarc.dev/r/switch.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Switch } from "@/components/arc/switch/switch";

export function NotificationsToggle() {
  const [enabled, setEnabled] = useState(true);
  return <Switch label="Email notifications" checked={enabled} onCheckedChange={setEnabled} />;
}
```

## API reference

### Switch

A Radix switch whose thumb stretches while pressed and travels on a spring.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` | `string` | – | Visible label inside the switch; also used as aria-label when none is passed. |
| `checked` | `boolean` | – | Controlled state. |
| `defaultChecked` | `boolean` | `false` | Initial state when uncontrolled. |
| `onCheckedChange` | `(checked: boolean) => void` | – | Called on every toggle. |
| `...props` | `ComponentPropsWithoutRef<typeof SwitchPrimitive.Root>` | – | Radix Switch root props, including ref, name, disabled, and aria-label. Pointer and key handlers are chained, not replaced. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Space / Enter | Toggles the switch. Holding Space stretches the thumb until release. |

## Accessibility

- Radix renders a button with role="switch" and aria-checked.
- The label prop becomes aria-label unless one is passed; icon-only usage needs an aria-label.

## Motion

- Pressing stretches the thumb toward the other side like a held finger; releasing sends it across on a snappy spring.
- Reduced motion removes the stretch and moves the thumb instantly; CSS transitions are also disabled.

## Responsive behavior

- The switch is inline and keeps a control-height minimum, so the whole label is a touch target.
- Hover styles apply only on fine pointers; touch gets the press stretch.

## Performance

- Motion is CSS transitions on the track and thumb, with no observers or per-frame work.

## Notes for AI

- Use for settings that take effect immediately. Use checkbox for choices confirmed by a submit button.
- Exported both as a named and a default export.
- Pass name to submit with a form through Radix's hidden input.

## Related

- [Checkbox](https://uiarc.dev/components/checkbox/markdown): A binary choice with a precise, legible state.
- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.
- [Theme switcher](https://uiarc.dev/components/theme-switch/markdown): Four smooth ways to move between light and dark appearance.

## Also in toggles

- [Radio cards](https://uiarc.dev/components/radio-cards/markdown): Selectable option cards with a sliding selection ring, price and description slots, and radio keyboard behavior.
- [Billing toggle](https://uiarc.dev/components/billing-toggle/markdown): A monthly and yearly switch with a savings badge and prices that roll to the new amount.
- [Radio group](https://uiarc.dev/components/radio-group/markdown): Choose one option from a visible set.
- [Rating](https://uiarc.dev/components/rating/markdown): A simple, accessible way to capture a numeric opinion.

## Guidance for AI tools

Switch: A tactile toggle for settings that take effect immediately. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
