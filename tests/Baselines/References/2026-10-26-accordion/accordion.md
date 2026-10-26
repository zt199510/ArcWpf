# Accordion

> Progressively reveal supporting information in place.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/accordion
- Markdown: https://uiarc.dev/components/accordion/markdown
- Registry item: https://uiarc.dev/r/accordion.json
- Source file: `registry/components/accordion/accordion.tsx`
- Dependencies: @radix-ui/react-accordion, motion, lucide-react
- Keywords: disclosure, layout, react accordion, faq accordion, collapsible sections, animated accordion, radix accordion, expand collapse list, faq component

## When to use

- FAQ sections where only one answer should be open at a time.
- Settings or help pages that group long content under short, scannable questions.
- Page-level FAQs that need larger type, via size="lg".

## When not to use

- Use expandable-card for a single standalone disclosure such as a plan or order summary.
- Use tabs when sections are peer views that people switch between.
- Use onboarding-checklist when the rows are setup tasks to complete.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/accordion
pnpm dlx shadcn@latest add @uiarc/accordion
yarn dlx shadcn@latest add @uiarc/accordion
bunx --bun shadcn@latest add @uiarc/accordion
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/accordion.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-accordion motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/accordion/accordion.tsx`

   The source is in the registry item: https://uiarc.dev/r/accordion.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Accordion } from "@/components/arc/accordion/accordion";

export function Faq() {
  return (
    <Accordion
      size="lg"
      items={[
        { title: "Can I cancel anytime?", content: "Yes. Your plan stays active until the period ends." },
        { title: "Do you offer refunds?", content: "Within 14 days of purchase, no questions asked." },
      ]}
    />
  );
}
```

## API reference

### Accordion

A single-open, collapsible list of question and answer rows built on Radix Accordion.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `items` (required) | `{ title: string; content: ReactNode }[]` | – | Rows in order. The title is the trigger label; content fills the panel. |
| `defaultOpen` | `number` | `0` | Index of the row open on first render. Pass -1 to start with every row closed. |
| `size` | `"md" \| "lg"` | `"md"` | "lg" sets questions at the large text size for page-level FAQs. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Toggles the focused row. |
| ArrowDown / ArrowUp | Moves focus between row triggers. |
| Home / End | Jumps to the first or last trigger. |

## Accessibility

- Radix wires aria-expanded, aria-controls, and region labelling between each trigger and panel.
- Closed panels stay mounted but switch to visibility hidden once collapsed, so they leave the accessibility tree.
- Triggers sit inside heading elements; the chevron is aria-hidden.

## Motion

- Panel height springs without overshoot while the answer slides down 6px out of a subtle blur; the chevron rotates on a snappy spring.
- A toggle mid-flight retargets from the current height instead of restarting.
- Reduced motion applies the same end states in one step.

## Responsive behavior

- Rows fill their container width; the lg size caps answers at 62ch for readable line length.
- Below 520px the lg size drops to a 68px row and smaller answer text with tighter right padding.
- Hover color changes apply only on hover-capable fine pointers.

## Performance

- Closed panels stay mounted and hidden, so every answer is in the DOM; keep very heavy content lazy inside the panel.
- Height springs through motion on the one row that changes, with no layout observers.

## Notes for AI

- Use for FAQs and settings groups where only one section should be open. Use expandable-card for a single standalone disclosure and tabs when sections are peers.
- Content is plain data; pass rich ReactNode answers directly. Only single mode is supported.

## Related

- [Expandable card](https://uiarc.dev/components/expandable-card/markdown): Give a dense card more room when requested.
- [Tabs](https://uiarc.dev/components/tabs/markdown): Switch between related content in the same context.
- [Onboarding checklist](https://uiarc.dev/components/onboarding-checklist/markdown): Guide a new workspace through setup, one step at a time.

## Also in expand

- [Scroll area](https://uiarc.dev/components/scroll-area/markdown): A native scroll container with thin overlay scrollbars and edge fades that appear only when content overflows.
- [Resizable panels](https://uiarc.dev/components/resizable-panels/markdown): Trade space between panes by dragging the divider between them.
- [Expandable text](https://uiarc.dev/components/expandable-text/markdown): Long text clamped to a few lines with a soft fade that glides open with Read more and closes with Show less, shown only when the text overflows.

## Guidance for AI tools

Accordion: Progressively reveal supporting information in place. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

