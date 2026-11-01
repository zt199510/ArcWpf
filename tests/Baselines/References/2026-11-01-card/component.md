# Card

> A contained group of related content and actions.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/card
- Markdown: https://uiarc.dev/components/card/markdown
- Registry item: https://uiarc.dev/r/card.json
- Source file: `registry/components/card/card.tsx`
- Dependencies: motion, @radix-ui/react-dialog, lucide-react
- Keywords: surface, layout, react card, card component, card with image, expand card to modal, quick look card, shared layout card, project card

## When to use

- Browsable items in a grid, such as projects, listings, or posts.
- Items that should open into a larger quick look dialog without leaving the page, via details.
- Content with media, an owner byline, and a status line that updates in place.

## When not to use

- Use expandable-card for inline disclosure without a dialog.
- Use stat-card or metric-card for numbers.
- Use dialog when the content has no card to grow from.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/card
pnpm dlx shadcn@latest add @uiarc/card
yarn dlx shadcn@latest add @uiarc/card
bunx --bun shadcn@latest add @uiarc/card
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/card.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion @radix-ui/react-dialog lucide-react
```

2. Copy the source into your project. Main file: `registry/components/card/card.tsx`

   The source is in the registry item: https://uiarc.dev/r/card.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Card } from "@/components/arc/card/card";
import { Avatar } from "@/components/arc/avatar/avatar";

export function ProjectCard() {
  return (
    <Card
      title="Harbour redesign"
      description="New booking flow and room pages."
      media={<img src="/harbour.jpg" alt="" />}
      avatar={<Avatar name="Maya Chen" size="sm" />}
      meta="Maya Chen"
      status="Updated 2 hours ago"
      details={<p>Scope, milestones, and open questions.</p>}
    />
  );
}
```

## API reference

### Card

A content card with optional media, byline, and action that can grow into a quick look dialog.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `title` (required) | `string` | – | Card heading. Becomes the dialog trigger when details is set. |
| `description` | `string` | – | Supporting line under the title. |
| `media` | `ReactNode` | – | Image or visual at the top. Zooms slightly on hover. |
| `action` | `ReactNode` | – | Trailing footer control, such as a button. |
| `avatar` | `ReactNode` | – | Small leading visual in the footer, such as the owner's avatar. |
| `meta` | `ReactNode` | – | Who the card belongs to, shown in the byline. |
| `status` | `string` | – | Short status under the meta, such as "Updated 2 hours ago". Changes roll in word by word and are announced politely. |
| `details` | `ReactNode` | – | Quick look content. When set, the card opens into a larger Radix dialog. |
| `open` | `boolean` | – | Controlled quick look state. |
| `defaultOpen` | `boolean` | `false` | Initial quick look state when uncontrolled. |
| `onOpenChange` | `(open: boolean) => void` | – | Called when the quick look opens or closes. |
| `children` | `ReactNode` | – | Extra body content between the description and footer. |
| `...props` | `HTMLAttributes<HTMLElement>` | – | Forwarded to the root article. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | On the title, opens the quick look when details is set. |
| Escape | Closes the quick look and returns focus to the title. |
| Tab | Stays trapped inside the open quick look. |

## Accessibility

- Renders an article with an h3 title; the quick look uses Radix Dialog with the title and description wired as its label and description.
- Only the title becomes a button, so nested actions stay separately focusable.
- Status changes are read through a role="status" region.
- Give media images alt text or an empty alt when decorative, and an aria-label to icon-only actions.

## Motion

- Hover lifts the card 2px and slowly zooms the media.
- The quick look shares layout ids with the card, so surface, photo, title, and byline travel on one spring; details fade in after.
- Reduced motion drops the lift, zoom, and morph, and the dialog simply fades.

## Responsive behavior

- The card fills its grid cell with min-width 0; the quick look panel is min(30rem, 100vw minus a gutter) wide and capped at the viewport height.
- Hover lift and media zoom run only for a mouse; touch and pen never lift.

## Performance

- The quick look shares layout ids with the card, so the morph animates several elements; the dialog mounts only while open.
- The overlay uses a 7px backdrop blur, which can cost frames on low-end devices over busy pages.

## Notes for AI

- Use for browsable items in a grid: projects, listings, posts. Use expandable-card for inline disclosure without a dialog, and stat-card or metric-card for numbers.
- Add details only when there is real extra content; without it the card is a static article.
- Keep status short and change it in place to get the rolling update.

## Related

- [Expandable card](https://uiarc.dev/components/expandable-card/markdown): Give a dense card more room when requested.
- [Dialog](https://uiarc.dev/components/dialog/markdown): A focused surface for decisions that need attention.
- [Avatar](https://uiarc.dev/components/avatar/markdown): A compact identity marker for people and accounts.
- [Photo grid](https://uiarc.dev/components/photo-grid/markdown): Pinch through zoom levels and open any photo straight from its cell.

## Also in cards

- [Metric card](https://uiarc.dev/components/metric-card/markdown): A compact summary for a number that needs context.
- [Empty state](https://uiarc.dev/components/empty-state/markdown): A useful next step when there is nothing to show yet.
- [Metric ticker](https://uiarc.dev/components/metric-ticker/markdown): A strip of live KPIs that roll to new values digit by digit and flash their change; rest on one to pause the strip and open its recent trend as a sparkline.

## Guidance for AI tools

Card: A contained group of related content and actions. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
