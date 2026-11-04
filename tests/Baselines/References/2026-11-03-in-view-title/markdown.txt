# In-view title

> Bring a section title in as it scrolls into view.

- Type: Component (text)
- Access: Free, open source
- Page: https://uiarc.dev/components/in-view-title
- Markdown: https://uiarc.dev/components/in-view-title/markdown
- Registry item: https://uiarc.dev/r/in-view-title.json
- Source file: `registry/components/in-view-title/in-view-title.tsx`
- Dependencies: motion
- Keywords: text, motion, heading, react scroll animation title, animate on scroll heading, in view text animation, section title reveal, scroll reveal text, framer motion heading

## When to use

- Section headings that should animate once they scroll into view.
- Feature pages where each section title needs a consistent entrance style.
- Headings that should replay on each entry, via once={false}.

## When not to use

- Use text-reveal for hero copy that animates on load.
- Use scroll-highlight for a paragraph that follows scroll progress.
- Use text-shimmer for status lines, not headings.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/in-view-title
pnpm dlx shadcn@latest add @uiarc/in-view-title
yarn dlx shadcn@latest add @uiarc/in-view-title
bunx --bun shadcn@latest add @uiarc/in-view-title
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/in-view-title.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/in-view-title/in-view-title.tsx`

   The source is in the registry item: https://uiarc.dev/r/in-view-title.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { InViewTitle } from "@/components/arc/in-view-title/in-view-title";

export function FeaturesHeader() {
  return (
    <InViewTitle
      variant="line"
      text="Everything your team needs to ship"
      lines={["Everything your team", "needs to ship"]}
    />
  );
}
```

## API reference

### InViewTitle

A section title that reveals itself once it scrolls into view, with five reveal styles.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `text` (required) | `string` | – | The title copy. |
| `variant` | `"word" \| "line" \| "blur" \| "tracking" \| "wipe"` | `"blur"` | Reveal style. blur suits most headings; word and line rise from a clip; tracking opens letter spacing; wipe uncovers left to right. |
| `as` | `"h1" \| "h2" \| "h3"` | `"h2"` | Heading level. |
| `lines` | `string[]` | – | Explicit line breaks for the line variant. Falls back to one line of text. |
| `once` | `boolean` | `true` | Set false to replay the reveal on every entry. |
| `className` | `string` | – | Merged onto the wrapper. |
| `id` | `string` | – | Forwarded to the heading. |

## Accessibility

- The heading carries aria-label with the full text; the animated parts are aria-hidden.
- Titles already scrolled past show immediately, and server-rendered titles appear on their own if scripts are slow.
- Choose as to match the page's heading outline.

## Motion

- Triggers when 45% of the title is visible. Opacity leads, blur clears next, and the transform settles last, so text is readable before it fully arrives.
- Hiding (with once={false}) is a quick fade with no stagger.
- Reduced motion shows the title instantly with no transform, blur, or mask.

## Responsive behavior

- Word, blur, and tracking variants wrap naturally at any width.
- The line variant uses your explicit lines, which do not rewrap, so keep each line short enough for mobile.

## Performance

- One IntersectionObserver per title via useInView, triggering at 45% visibility.
- Each word or line is a motion span with blur and transform; fine for headings, not for long body text.

## Notes for AI

- Default choice for section headings that should animate on scroll. Use text-reveal for on-load hero copy and scroll-highlight for a key paragraph.
- Pass lines only with variant="line"; the other variants split on words.

## Related

- [Text reveal](https://uiarc.dev/components/text-reveal/markdown): Reveal a short piece of content with restrained motion.
- [Text shimmer](https://uiarc.dev/components/text-shimmer/markdown): Show ongoing work with a calm light across the words.

## Also in text effects

- [Text morph](https://uiarc.dev/components/text-morph/markdown): Morph a label into its next state, letter by letter.

## Guidance for AI tools

In-view title: Bring a section title in as it scrolls into view. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
