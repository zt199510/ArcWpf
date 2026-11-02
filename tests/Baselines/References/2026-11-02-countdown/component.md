# Countdown

> A launch countdown with rolling digits that morphs into a live state at zero.

- Type: Component (feedback)
- Access: Free, open source
- Page: https://uiarc.dev/components/countdown
- Markdown: https://uiarc.dev/components/countdown/markdown
- Registry item: https://uiarc.dev/r/countdown.json
- Source file: `registry/components/countdown/countdown.tsx`
- Dependencies: motion
- Keywords: feedback, new, react countdown, countdown timer, launch countdown, event timer, digit roll animation, deadline timer, time zone countdown

## When to use

- A launch page or event hero counting to a keynote.
- A compact deadline pill in a table row or banner.
- Sale or registration windows that should flip to a Live now state on their own.

## When not to use

- Use stopwatch for time counting up from a start.
- Use announcement-bar when the countdown sits inside a site-wide banner with a call to action.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/countdown
pnpm dlx shadcn@latest add @uiarc/countdown
yarn dlx shadcn@latest add @uiarc/countdown
bunx --bun shadcn@latest add @uiarc/countdown
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/countdown.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/countdown/countdown.tsx`

   The source is in the registry item: https://uiarc.dev/r/countdown.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Countdown } from "@/components/arc/countdown/countdown";

export function LaunchCountdown() {
  return (
    <Countdown
      target="2026-10-06T10:00:00"
      timeZone="America/Los_Angeles"
      label="Keynote starts in"
      onComplete={() => router.refresh()}
    />
  );
}
```

## Examples

### Compact hours and minutes

```tsx
<Countdown variant="compact" units={["hours", "minutes"]} target={saleEndsAt} completeLabel="Ended" />
```

## API reference

### Countdown

A countdown to a fixed moment with rolling digit wheels, in a large display form or a compact pill.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `target` (required) | `Date \| number \| string` | – | The moment to count to. Numbers are epoch ms; ISO strings with Z or an offset are exact; strings without an offset are wall time in timeZone, or UTC. |
| `timeZone` | `string` | – | IANA zone for a target string without an offset, such as "America/Los_Angeles". Daylight saving is resolved for the target date. |
| `units` | `CountdownUnit[]` | `["days", "hours", "minutes", "seconds"]` | Units to show, sorted largest first. The largest unit absorbs the rest, so hours can pass 24 when days are left out. |
| `variant` | `"large" \| "compact"` | `"large"` | Large digits with unit labels, or one compact line for banners and table cells. |
| `completeLabel` | `ReactNode` | `"Live now"` | Shown once the target passes, with a pulsing live dot. The digits morph into it. |
| `onComplete` | `() => void` | – | Fires once when the countdown reaches zero while mounted, on time even in a hidden tab. |
| `label` | `string` | `"Time remaining"` | Accessible name, such as "Keynote starts in". |
| `clock` | `() => number` | `Date.now` | Clock source in epoch ms. Pass a server-synced clock when device time cannot be trusted. |
| `ref` | `Ref<HTMLDivElement>` | – | The root element. |
| `className` | `string` | – | Class on the root. |

### toEpoch

Resolves a countdown target to epoch ms without reading the device time zone. Returns NaN for an unparseable string.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `target` (required) | `Date \| number \| string` | – | Same forms as the target prop. |
| `timeZone` | `string` | – | IANA zone for strings without an offset. |

## Accessibility

- The root is role="timer" named by label; the visible digits are aria-hidden.
- A hidden text reads the remaining time in words, dropping seconds until under a minute so it does not chatter.
- When complete, a string completeLabel is read; other nodes read as Complete.

## Motion

- Each digit is a clipped wheel that turns the short way, so 00 to 59 is one step down and the window feathers the edges.
- Digits roll in with a small stagger on first reveal, and the shell springs its width and height when a unit column appears or drops.
- At zero the digits blur and scale out into the complete label, and the live dot pulses.
- Reduced motion jumps the wheels, sizes the shell instantly, swaps with a fade, and stops the pulse.

## Responsive behavior

- The large variant is a container: digits size to clamp(1.75rem, 12.5cqi, text-5xl), so four groups fit at 320px.
- The compact variant is an inline pill with 32px minimum height that springs its width as units drop.

## Performance

- A single timeout wakes just after each whole second; it pauses while the tab is hidden or the countdown is off screen.
- A separate long timeout still fires onComplete on time when the tab is hidden.
- Each digit renders its whole wheel (10 or fewer glyphs), so keep a page to a few countdowns.

## Notes for AI

- Use for a fixed moment people wait for: a launch, a live event, a sale ending. For elapsed time use stopwatch.
- Give targets an offset or a timeZone so every visitor counts to the same instant; never rely on the visitor's zone.
- Pass clock from a server time offset when device clocks may be wrong, such as ticketed drops.
- Use variant="compact" inside announcement bars, table cells, and badges.

## Related

- [Animated counter](https://uiarc.dev/components/animated-counter/markdown): Give changing totals a clear sense of movement.
- [Announcement bar](https://uiarc.dev/components/announcement-bar/markdown): A top banner that rotates messages, counts down, and collapses smoothly when dismissed.
- [Badge](https://uiarc.dev/components/badge/markdown): A small label for status, category, or metadata.

## Also in progress

- [Progress](https://uiarc.dev/components/progress/markdown): Show how much of a known task is complete.
- [Skeleton](https://uiarc.dev/components/skeleton/markdown): Reserve space while content is still loading.
- [Stepper](https://uiarc.dev/components/stepper/markdown): Show where a person is in a multi-step flow and what is done.
- [Usage meter](https://uiarc.dev/components/usage-meter/markdown): Show what fills an allowance and how close it is to the limit.
- [Onboarding checklist](https://uiarc.dev/components/onboarding-checklist/markdown): Guide a new workspace through setup, one step at a time.

## Guidance for AI tools

Countdown: A launch countdown with rolling digits that morphs into a live state at zero. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
