# Text shimmer

> Show ongoing work with a calm light across the words.

- Type: Component (text)
- Access: Free, open source
- Page: https://uiarc.dev/components/text-shimmer
- Markdown: https://uiarc.dev/components/text-shimmer/markdown
- Registry item: https://uiarc.dev/r/text-shimmer.json
- Source file: `registry/components/text-shimmer/text-shimmer.tsx`
- Dependencies: motion
- Keywords: text, motion, loading, react text shimmer, shimmer text, ai thinking text, loading text animation, shiny text, gradient text sweep

## When to use

- AI thinking or loading labels such as Generating summary.
- Short status lines that should show ongoing work and settle when done.

## When not to use

- Use text-stream for the response text itself.
- Use skeleton for content placeholders and progress for measurable progress.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/text-shimmer
pnpm dlx shadcn@latest add @uiarc/text-shimmer
yarn dlx shadcn@latest add @uiarc/text-shimmer
bunx --bun shadcn@latest add @uiarc/text-shimmer
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/text-shimmer.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/text-shimmer/text-shimmer.tsx`

   The source is in the registry item: https://uiarc.dev/r/text-shimmer.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { TextShimmer } from "@/components/arc/text-shimmer/text-shimmer";

export function AssistantStatus({ busy }: { busy: boolean }) {
  return (
    <TextShimmer active={busy}>
      {busy ? "Generating summary" : "Summary ready"}
    </TextShimmer>
  );
}
```

## API reference

### TextShimmer

A calm light sweep across a short status line to show that work is ongoing. When active turns false the band glides off and the text settles to solid.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `children` (required) | `string` | – | The status text. Keep it to one short line. A new value rises in place. |
| `active` | `boolean` | `true` | Sweep while work is ongoing. |
| `duration` | `number` | `1.8` | Seconds for one sweep across the text. |
| `as` | `"span" \| "p" \| "div" \| "h2" \| "h3" \| "h4"` | `"span"` | Rendered element. |
| `className` | `string` | – | Merged onto the rendered element. |
| `id` | `string` | – | Forwarded to the rendered element. |

## Accessibility

- Sets aria-busy while active; the text itself stays real, readable text.
- Announce completion from your own aria-live region; the component does not.

## Motion

- A bell-shaped highlight band in the current text color sweeps linearly over a muted base, pausing briefly between passes.
- The sweep only runs while active, in view, and the page is visible; a paused band resumes where it stopped. A changed label rises in with a soft blur.
- Reduced motion stops the sweep and swaps labels with a plain fade.

## Responsive behavior

- Keeps max-width 100%, but the band is tuned for one short line, so avoid wrapping text.
- In forced-colors mode the gradient is removed and the text renders in the system color.

## Performance

- The sweep runs only while active, within 64px of the viewport, and while the page is visible.
- It animates a background-clip gradient on one element, with no per-letter spans.

## Notes for AI

- Use for AI thinking or loading labels. Pair with text-stream for the response itself, and use skeleton or progress for content placeholders.
- Flip active to false when work finishes instead of unmounting, so the text settles smoothly.

## Related

- [Text morph](https://uiarc.dev/components/text-morph/markdown): Morph a label into its next state, letter by letter.
- [Skeleton](https://uiarc.dev/components/skeleton/markdown): Reserve space while content is still loading.
- [Progress](https://uiarc.dev/components/progress/markdown): Show how much of a known task is complete.

## Also in text effects

- [Text reveal](https://uiarc.dev/components/text-reveal/markdown): Reveal a short piece of content with restrained motion.
- [In-view title](https://uiarc.dev/components/in-view-title/markdown): Bring a section title in as it scrolls into view.

## Guidance for AI tools

Text shimmer: Show ongoing work with a calm light across the words. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
