# Avatar group

> Show a team or set of contributors in a small space.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/avatar-group
- Markdown: https://uiarc.dev/components/avatar-group/markdown
- Registry item: https://uiarc.dev/r/avatar-group.json
- Source file: `registry/components/avatar-group/avatar-group.tsx`
- Dependencies: motion
- Keywords: identity, people, react avatar group, avatar stack, overlapping avatars, team members avatars, avatar overflow count, collaborators list

## When to use

- Showing who owns or edits something in a header, card, or table cell.
- Collaborator stacks where people join and leave while the page is open.
- Long member lists that should collapse into a +N chip.

## When not to use

- Use avatar for a single person.
- Use a list or sortable-data-table when people need to see every name and role.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/avatar-group
pnpm dlx shadcn@latest add @uiarc/avatar-group
yarn dlx shadcn@latest add @uiarc/avatar-group
bunx --bun shadcn@latest add @uiarc/avatar-group
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/avatar-group.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/avatar-group/avatar-group.tsx`

   The source is in the registry item: https://uiarc.dev/r/avatar-group.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { AvatarGroup } from "@/components/arc/avatar-group/avatar-group";

const team = [
  { name: "Maya Chen", src: "/people/maya.jpg", status: "online" as const },
  { name: "Leo Park" },
  { name: "Sara Ruiz" },
  { name: "Tom Hale" },
  { name: "Ines Ma" },
];

export function Collaborators() {
  return <AvatarGroup members={team} max={3} label="Editors" />;
}
```

## API reference

### AvatarGroup

An overlapping stack of avatars with a +N overflow chip.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `members` (required) | `{ name: string; src?: string; status?: "online" \| "offline" }[]` | – | People in display order. Names double as keys, so keep them unique. |
| `max` | `number` | `4` | Avatars shown before the rest collapse into the overflow count. |
| `size` | `"sm" \| "md" \| "lg"` | `"md"` | Size passed to each avatar and the overflow chip. |
| `label` | `string` | `"Team members"` | Accessible name for the group, also used in the overflow label. |

## Accessibility

- Renders role="group" with the label as its name.
- The overflow chip is role="img" labelled like "2 more editors".
- Each avatar keeps its own name and status label.

## Motion

- Joining or leaving members open and close their slot on a spring, so the stack slides instead of jumping.
- The overflow count rolls up when it grows and down when it shrinks.
- Hover fans the stack apart in CSS. Reduced motion removes the fan and swaps counts with a plain fade.

## Responsive behavior

- The stack is inline-flex and does not wrap; lower max on narrow rows so it stays compact.
- The hover fan applies only on hover-capable fine pointers, so touch taps do not spread the stack.

## Performance

- Only max avatars render; the rest collapse into one overflow chip, so long member lists stay cheap.
- Joins and leaves animate slot width on a spring; the fan is plain CSS transforms.

## Notes for AI

- Use for presence and ownership in headers, cards, and table cells. Use avatar for one person.
- Pass the full member list and let max handle truncation; do not slice it yourself or the overflow count is lost.

## Related

- [Avatar](https://uiarc.dev/components/avatar/markdown): A compact identity marker for people and accounts.
- [Card](https://uiarc.dev/components/card/markdown): A contained group of related content and actions.
- [User menu](https://uiarc.dev/components/user-menu/markdown): Your account, settings, theme, and sign out behind the avatar. Opens as a bottom sheet on phones.

## Also in avatars

- [Badge](https://uiarc.dev/components/badge/markdown): A small label for status, category, or metadata.
- [Presence stack](https://uiarc.dev/components/presence-stack/markdown): Avatars of the people here now: joins pop in as the stack reflows, a counter rolls past the limit, dots show typing and idle, and hover fans it out with a gliding name label.

## Guidance for AI tools

Avatar group: Show a team or set of contributors in a small space. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
