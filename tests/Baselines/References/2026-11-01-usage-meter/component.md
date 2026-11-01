# Usage meter

> Show what fills an allowance and how close it is to the limit.

- Type: Component (feedback)
- Access: Free, open source
- Page: https://uiarc.dev/components/usage-meter
- Markdown: https://uiarc.dev/components/usage-meter/markdown
- Registry item: https://uiarc.dev/r/usage-meter.json
- Source file: `registry/components/usage-meter/usage-meter.tsx`
- Dependencies: motion, lucide-react
- Keywords: meter, usage, storage, react usage meter, storage usage bar, quota meter, plan limit indicator, segmented progress bar, billing usage

## When to use

- Quota against a fixed allowance, such as storage, seats, or API calls.
- Billing pages that should show what takes the space and how close the plan is to full.
- Plans that can go over the limit, where the overage should be clear without relying on color.

## When not to use

- Use progress for task completion.
- Use bar-chart to compare categories without a limit.
- Use gauge or stat-card for a single headline metric.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/usage-meter
pnpm dlx shadcn@latest add @uiarc/usage-meter
yarn dlx shadcn@latest add @uiarc/usage-meter
bunx --bun shadcn@latest add @uiarc/usage-meter
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/usage-meter.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/usage-meter/usage-meter.tsx`

   The source is in the registry item: https://uiarc.dev/r/usage-meter.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { UsageMeter } from "@/components/arc/usage-meter/usage-meter";

export function StorageCard() {
  return (
    <UsageMeter
      label="Workspace storage"
      unit="GB"
      limit={50}
      segments={[
        { id: "files", label: "Files", value: 21.4 },
        { id: "media", label: "Media", value: 14.2 },
        { id: "backups", label: "Backups", value: 6.8 },
      ]}
    />
  );
}
```

## API reference

### UsageMeter

A segmented bar showing what uses a fixed allowance, with a rolling total, status badge, and interactive legend.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | What is measured, such as "Workspace storage". |
| `segments` (required) | `{ id: string; label: string; value: number }[]` | – | Used amounts in display order. Keep ids stable so segments re-flow. Up to four read clearly. |
| `limit` (required) | `number` | – | The plan limit, in the same unit as the segments. |
| `unit` | `string` | `""` | Unit shown after numbers, such as "GB". |
| `decimals` | `number` | `1` | Digits after the decimal point. |
| `freeLabel` | `string` | `"Free"` | Legend label for remaining space. |
| `overLabel` | `string` | `"Over limit"` | Legend label for the overage. |
| `warnAt` | `number` | `0.9` | Share of the limit at which the badge warns that it is almost full. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowLeft / ArrowRight / ArrowUp / ArrowDown | Moves between legend items (roving tab stop). |
| Home / End | Jumps to the first or last legend item. |
| Enter / Space | Pins or unpins the focused category. |
| Escape | Clears the pinned category. |

## Accessibility

- The meter is a role="group" labelled by its title; the bar is role="img" with a full text summary of every segment and the status.
- Rolling numbers are aria-hidden with screen-reader-only text alongside.
- Status changes (near limit, over limit, growing overage) are announced in a polite live region.
- Legend items are buttons with aria-pressed and labels like "Media, 14.2 GB".

## Motion

- Segments grow in left to right once scrolled into view, then re-flow on one spring each when values change; crossing the limit fades in a hatched overage and limit marker.
- Digits roll like an odometer in the direction the number moved; the status badge springs to its new width.
- Reduced motion jumps values and swaps text with short fades.

## Responsive behavior

- The legend is an auto-fit grid of 108px minimum columns, so it wraps to fewer columns on narrow cards.
- Hovering the bar highlights a category on mouse; on touch a tap pins it instead.
- The caption ellipsizes on one line, so long labels do not push the layout.

## Performance

- Segments animate in once via useInView at 40% visibility, then re-flow on springs when values change.
- A ResizeObserver tracks the bar width; up to four segments read clearly.

## Notes for AI

- Use for quota against a fixed allowance: storage, seats, API calls. Use progress for task completion and bar-chart for comparing categories without a limit.
- Keep segment ids stable across renders so updates animate instead of remounting.

## Related

- [Progress](https://uiarc.dev/components/progress/markdown): Show how much of a known task is complete.
- [Bar chart](https://uiarc.dev/components/bar-chart/markdown): Compare one measure across days and scrub any bar for its value.
- [Gauge](https://uiarc.dev/components/gauge/markdown): Show a value against a known range.

## Also in progress

- [Skeleton](https://uiarc.dev/components/skeleton/markdown): Reserve space while content is still loading.
- [Stepper](https://uiarc.dev/components/stepper/markdown): Show where a person is in a multi-step flow and what is done.
- [Countdown](https://uiarc.dev/components/countdown/markdown): A launch countdown with rolling digits that morphs into a live state at zero.
- [Onboarding checklist](https://uiarc.dev/components/onboarding-checklist/markdown): Guide a new workspace through setup, one step at a time.

## Guidance for AI tools

Usage meter: Show what fills an allowance and how close it is to the limit. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
