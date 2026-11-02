# Announcement bar

> A top banner that rotates messages, counts down, and collapses smoothly when dismissed.

- Type: Component (feedback)
- Access: Free, open source
- Page: https://uiarc.dev/components/announcement-bar
- Markdown: https://uiarc.dev/components/announcement-bar/markdown
- Registry item: https://uiarc.dev/r/announcement-bar.json
- Source file: `registry/components/announcement-bar/announcement-bar.tsx`
- Dependencies: motion, lucide-react
- Keywords: feedback, new, react announcement bar, top banner, promo banner, site notice bar, dismissible banner, rotating announcements, sale countdown banner

## When to use

- Site-wide promotions, launches, or maintenance notices at the top of every page.
- Rotating two to four short announcements in one slim bar.
- A sale banner with a live countdown to its end.

## When not to use

- Use alert for a message tied to one section or form.
- Use toast for feedback on something the visitor just did.
- Use cookie-consent for consent choices.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/announcement-bar
pnpm dlx shadcn@latest add @uiarc/announcement-bar
yarn dlx shadcn@latest add @uiarc/announcement-bar
bunx --bun shadcn@latest add @uiarc/announcement-bar
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/announcement-bar.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/announcement-bar/announcement-bar.tsx`

   The source is in the registry item: https://uiarc.dev/r/announcement-bar.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { AnnouncementBar } from "@/components/arc/announcement-bar/announcement-bar";

export function SiteBanner() {
  return (
    <AnnouncementBar
      id="fall-sale-2026"
      tone="inverted"
      messages={[
        { id: "sale", message: "Fall sale: 30% off annual plans.", countdown: { to: "2026-10-01T00:00:00Z", label: "Ends in" }, action: { label: "See plans", href: "/pricing" } },
        { id: "launch", message: "Workflows are now in beta.", action: { label: "Read more", href: "/blog/workflows" } },
      ]}
    />
  );
}
```

## API reference

### AnnouncementBar

A slim top-of-page bar that rotates messages with a timer ring, supports a call to action and live countdown, and remembers dismissal.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `messages` (required) | `Announcement[]` | – | Messages: { id, message, action?, countdown? }. action is { label, href?, onClick? }; countdown is { to, label? }. |
| `id` | `string` | – | Remembers dismissal in localStorage under arc-announcement:<id>. Change it to show a new campaign again. |
| `open` | `boolean` | – | Controlled visibility. Stored dismissal is ignored when set. |
| `defaultOpen` | `boolean` | `true` | Starting visibility when uncontrolled. |
| `onOpenChange` | `(open: boolean) => void` | – | Called with false when the visitor dismisses the bar. |
| `index` | `number` | – | Controlled index of the visible message. |
| `defaultIndex` | `number` | `0` | Starting message when uncontrolled. |
| `onIndexChange` | `(index: number) => void` | – | Called when rotation or the arrows change the message. |
| `interval` | `number` | `6000` | Milliseconds each message stays before the next one. |
| `autoPlay` | `boolean` | `true` | Rotates automatically. Off when the visitor prefers reduced motion. |
| `controls` | `boolean` | `false` | Shows previous, next, and the pause ring when there are several messages. Off by default: only the close button shows and messages still rotate. |
| `dismissible` | `boolean` | `true` | Shows the dismiss button. |
| `tone` | `"neutral" \| "inverted"` | `"neutral"` | Muted surface, or foreground-colored bar with background text. |
| `onAction` | `(announcement: Announcement) => void` | – | Called when a message's call to action is pressed, after its own onClick. |
| `onCountdownEnd` | `(announcement: Announcement) => void` | – | Called once when a message's countdown reaches zero. |
| `label` | `string` | `"Announcements"` | Accessible name of the region. |
| `className` | `string` | – | Class on the section. |
| `ref` | `Ref<HTMLElement>` | – | The section element. |

### clearAnnouncementDismissal

Forgets a remembered dismissal so the bar with this id shows again.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `id` (required) | `string` | – | The id passed to AnnouncementBar. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Moves through the call to action and dismiss button, plus previous, pause, and next when controls is on. Focus inside pauses rotation. |
| Enter / Space | Presses the focused control. |

## Accessibility

- The bar is a labelled section; with several messages it is a carousel and each message a slide labelled n of total.
- The viewport is aria-live off while rotating and polite while paused, so auto-rotation is not read aloud.
- With controls, the pause ring is a toggle button with aria-pressed and previous and next buttons control the slides region. Without controls, rotation still pauses on hover and focus.
- Countdowns are role="timer" with aria-live off and a spoken label in hours and minutes.
- Rotation pauses on hover, keyboard focus inside, or a hidden tab.

## Motion

- Next messages rise from below and leave upward (previous runs the other way) while the viewport springs to the new height.
- With controls, the ring around the pause button fills linearly over the interval, stops where it is on pause, and finishes the rest on resume.
- Countdown digits drop in from above; dismissing collapses the bar's height so the page below eases up.
- Reduced motion turns rotation off, swaps messages and digits with fades, and removes the height collapse.

## Responsive behavior

- Wide bars mirror the controls with an empty column so the message stays centered on the page.
- The bar is an inline-size container; at 560px and below messages align to the start edge instead of centering.
- Messages wrap on small screens and the viewport springs to the wrapped height; icon buttons are 32px.

## Performance

- One linear motion value drives the ring; it stops while paused, hidden, or dismissed.
- A countdown ticks with one timeout per second, waking just after each whole second.

## Notes for AI

- Place it above the site header. With an id, dismissal persists; call clearAnnouncementDismissal(id) or change the id to show it again.
- Keep messages to one short sentence; the call to action is a link when href is set, a button otherwise.
- For a standalone countdown elsewhere on the page use countdown; this bar has its own lighter timer.

## Related

- [Countdown](https://uiarc.dev/components/countdown/markdown): A launch countdown with rolling digits that morphs into a live state at zero.
- [Cookie consent](https://uiarc.dev/components/cookie-consent/markdown): A consent bar that morphs into preferences and folds into a reopen button.
- [Alert](https://uiarc.dev/components/alert/markdown): A persistent message that helps people recover or continue.
- [Toast](https://uiarc.dev/components/toast/markdown): Brief confirmation for a completed background action.
- [Badge](https://uiarc.dev/components/badge/markdown): A small label for status, category, or metadata.

## Also in messages

- [Toast stack](https://uiarc.dev/components/toast-stack/markdown): Stack short results at the edge until you reach for them.
- [Undo toast](https://uiarc.dev/components/undo-toast/markdown): Archive or delete rows with a second chance: the gap closes, a countdown toast merges repeat actions, and Undo or ⌘Z puts every row back exactly where it was.

## Guidance for AI tools

Announcement bar: A top banner that rotates messages, counts down, and collapses smoothly when dismissed. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
