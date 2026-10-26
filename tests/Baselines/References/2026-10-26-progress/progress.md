# Progress

> Show how much of a known task is complete.

- Type: Component (feedback)
- Access: Free, open source
- Page: https://uiarc.dev/components/progress
- Markdown: https://uiarc.dev/components/progress/markdown
- Registry item: https://uiarc.dev/r/progress.json
- Source file: `registry/components/progress/progress.tsx`
- Dependencies: motion, lucide-react
- Keywords: status, loading, react progress bar, animated progress bar, upload progress, loading bar, percentage bar, progress indicator

## When to use

- Determinate progress for uploads, imports, or long tasks.
- Progress with a visible percentage and a check at completion, via showValue.

## When not to use

- Use skeleton while content is loading with no progress to report.
- Use usage-meter for quota against a limit, and gauge for dashboard metrics.
- Use stepper to show position in a multi-step flow.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/progress
pnpm dlx shadcn@latest add @uiarc/progress
yarn dlx shadcn@latest add @uiarc/progress
bunx --bun shadcn@latest add @uiarc/progress
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/progress.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/progress/progress.tsx`

   The source is in the registry item: https://uiarc.dev/r/progress.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Progress } from "@/components/arc/progress/progress";

export function UploadProgress({ sent, size }: { sent: number; size: number }) {
  return <Progress label="Uploading report.pdf" value={sent} max={size} showValue />;
}
```

## API reference

### Progress

A linear progress bar with an optional label and a counted percentage that matches the fill.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` | `number` | `0` | Current value, clamped between 0 and max. |
| `max` | `number` | `100` | Value that counts as complete. |
| `label` | `string` | – | Visible label and aria-label. Falls back to "Progress" for assistive tech. |
| `showValue` | `boolean` | `false` | Shows the counted percentage and a check at 100%. |
| `...props` | `Omit<HTMLAttributes<HTMLDivElement>, "children">` | – | Forwarded to the progressbar element, such as className. |

## Accessibility

- Renders role="progressbar" with aria-valuemin, aria-valuemax, aria-valuenow, and a percentage aria-valuetext.
- Label changes crossfade; outgoing copies are aria-hidden while they fade.
- It is not a live region; announce completion separately if it matters.

## Motion

- One smooth spring drives both the fill and the counted number, so they always agree. The fill slides in from the left to keep its rounded end.
- At 100% the fill turns to the success colour and a check settles in beside the count.
- Reduced motion jumps the fill and count to the new value.

## Responsive behavior

- The bar fills its container width, so it works in cards, rows, and full-width layouts.

## Performance

- One spring drives both the fill and the counted number; no observers or timers.
- It is not a live region, so announce completion separately if it matters.

## Notes for AI

- Use for determinate task progress. Use skeleton while content has no progress to report, gauge or activity-rings for dashboard metrics, and usage-meter for quota against a limit.
- Pass raw value and max; the percentage is computed and clamped for you.

## Related

- [Usage meter](https://uiarc.dev/components/usage-meter/markdown): Show what fills an allowance and how close it is to the limit.
- [Skeleton](https://uiarc.dev/components/skeleton/markdown): Reserve space while content is still loading.
- [Gauge](https://uiarc.dev/components/gauge/markdown): Show a value against a known range.
- [Stepper](https://uiarc.dev/components/stepper/markdown): Show where a person is in a multi-step flow and what is done.

## Also in progress

- [Countdown](https://uiarc.dev/components/countdown/markdown): A launch countdown with rolling digits that morphs into a live state at zero.
- [Onboarding checklist](https://uiarc.dev/components/onboarding-checklist/markdown): Guide a new workspace through setup, one step at a time.

## Guidance for AI tools

Progress: Show how much of a known task is complete. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

