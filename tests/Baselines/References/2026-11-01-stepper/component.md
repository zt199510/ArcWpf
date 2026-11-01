# Stepper

> Show where a person is in a multi-step flow and what is done.

- Type: Component (feedback)
- Access: Free, open source
- Page: https://uiarc.dev/components/stepper
- Markdown: https://uiarc.dev/components/stepper/markdown
- Registry item: https://uiarc.dev/r/stepper.json
- Source file: `registry/components/stepper/stepper.tsx`
- Dependencies: motion
- Keywords: motion, product, react stepper, step indicator, progress steps, wizard steps, checkout steps, vertical stepper, multi step progress

## When to use

- Showing position in a strict multi-step flow like checkout or account setup.
- Flows where people can jump back to completed steps, via onStepSelect.
- Vertical timelines of steps with descriptions and error states.

## When not to use

- Use multi-step-form when you also want the step content, navigation, and success state.
- Use onboarding-checklist for loosely ordered tasks.
- Use progress when a single percentage is enough.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/stepper
pnpm dlx shadcn@latest add @uiarc/stepper
yarn dlx shadcn@latest add @uiarc/stepper
bunx --bun shadcn@latest add @uiarc/stepper
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/stepper.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/stepper/stepper.tsx`

   The source is in the registry item: https://uiarc.dev/r/stepper.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Stepper } from "@/components/arc/stepper/stepper";

export function CheckoutSteps({ step, goTo }: { step: number; goTo: (index: number) => void }) {
  return (
    <Stepper
      current={step}
      onStepSelect={goTo}
      steps={[
        { id: "cart", label: "Cart" },
        { id: "shipping", label: "Shipping", description: "Address and delivery" },
        { id: "payment", label: "Payment" },
      ]}
    />
  );
}
```

## API reference

### Stepper

A horizontal or vertical steps indicator driven by the current index, with optional navigation back to completed steps.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `steps` (required) | `{ id: string; label: string; description?: string; error?: string }[]` | – | Steps in order. error morphs the marker into an alert and replaces the description. |
| `current` (required) | `number` | – | Index of the step in progress. steps.length marks the flow complete. |
| `orientation` | `"horizontal" \| "vertical"` | `"horizontal"` | Layout direction. |
| `onStepSelect` | `(index: number) => void` | – | Called with the index of a completed step when chosen. Without it the stepper is read only. |
| `details` | `"all" \| "current"` | `"all"` | "current" shows only the active step's description. Errors always show. |
| `compact` | `boolean` | `false` | Markers only. Horizontal steppers switch to this below 30rem on their own. |
| `label` | `string` | `"Progress"` | Accessible name for the stepper. |
| `completeLabel` | `string` | `"All steps complete"` | Announced, and shown in the compact caption, once every step is complete. |
| `className` | `string` | – | Class on the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowLeft / ArrowRight / ArrowUp / ArrowDown | With onStepSelect, moves focus between reachable steps (RTL aware). |
| Home / End | Jumps to the first or the current step. |
| Enter / Space | Selects a completed step. |

## Accessibility

- Renders a labelled nav when interactive, otherwise role="group"; the current step has aria-current="step".
- Each step appends screen-reader-only status text: Completed, Not started, or Error.
- Step changes are announced in a polite live region as "Step 2 of 4: Shipping".
- Upcoming steps are aria-disabled and removed from the tab order.

## Motion

- Connectors fill one after another when progress jumps several steps; the ring then grows around the new current marker.
- Numbers morph into drawn checks or alerts; descriptions rise in while their slot springs to the new height.
- Reduced motion applies all changes instantly.

## Responsive behavior

- Horizontal steppers switch to markers only with a caption below a 30rem container width.
- Set orientation to vertical for sidebars or narrow columns where labels and descriptions should stay visible.
- Labels and descriptions wrap with overflow-wrap, so long words do not overflow.

## Performance

- The stepper only indicates; it renders markers and text, with a ResizeObserver for the description slot height.

## Notes for AI

- Use to show position in a strict multi-step flow such as checkout or setup. Use onboarding-checklist for loosely ordered tasks.
- The stepper only indicates; render the step content yourself and drive current. Set current to steps.length when done.

## Related

- [Onboarding checklist](https://uiarc.dev/components/onboarding-checklist/markdown): Guide a new workspace through setup, one step at a time.
- [Progress](https://uiarc.dev/components/progress/markdown): Show how much of a known task is complete.
- [Tabs](https://uiarc.dev/components/tabs/markdown): Switch between related content in the same context.
- [Breadcrumb](https://uiarc.dev/components/breadcrumb/markdown): Show where a page sits in a hierarchy.

## Also in progress

- [Skeleton](https://uiarc.dev/components/skeleton/markdown): Reserve space while content is still loading.
- [Countdown](https://uiarc.dev/components/countdown/markdown): A launch countdown with rolling digits that morphs into a live state at zero.
- [Usage meter](https://uiarc.dev/components/usage-meter/markdown): Show what fills an allowance and how close it is to the limit.

## Guidance for AI tools

Stepper: Show where a person is in a multi-step flow and what is done. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
