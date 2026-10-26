# Tabs

> Switch between related content in the same context.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/tabs
- Markdown: https://uiarc.dev/components/tabs/markdown
- Registry item: https://uiarc.dev/r/tabs.json
- Source file: `registry/components/tabs/tabs.tsx`
- Dependencies: @radix-ui/react-tabs, motion, lucide-react
- Keywords: navigation, layout, react tabs, animated tabs, radix tabs, tab component, sliding tab indicator, scrollable tabs, tabs with animation

## When to use

- Peer views of the same object, such as Overview, Activity, and Settings.
- Panels with different heights, where the container should spring between them.
- Long tab sets that need to scroll horizontally with edge buttons.

## When not to use

- Use segmented-control for a compact value toggle not tied to panels.
- Use accordion for stacked sections people read in order.
- Use liquid-tab-bar or morph-nav for app-level navigation between routes.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/tabs
pnpm dlx shadcn@latest add @uiarc/tabs
yarn dlx shadcn@latest add @uiarc/tabs
bunx --bun shadcn@latest add @uiarc/tabs
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/tabs.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-tabs motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/tabs/tabs.tsx`

   The source is in the registry item: https://uiarc.dev/r/tabs.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/arc/tabs/tabs";

export function ProjectTabs() {
  return (
    <Tabs defaultValue="overview">
      <TabsList aria-label="Project">
        <TabsTrigger value="overview">Overview</TabsTrigger>
        <TabsTrigger value="activity">Activity</TabsTrigger>
      </TabsList>
      <TabsContent value="overview">Summary and stats</TabsContent>
      <TabsContent value="activity">Recent events</TabsContent>
    </Tabs>
  );
}
```

## API reference

### Tabs

Radix Tabs root that tracks direction so panels slide the way the selection moved.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` | `string` | – | Controlled active tab value. |
| `defaultValue` | `string` | – | Initial active tab when uncontrolled. |
| `onValueChange` | `(value: string) => void` | – | Called when the active tab changes. |
| `...props` | `ComponentPropsWithoutRef<typeof TabsPrimitive.Root>` | – | Other Radix root props such as activationMode and className. |

### TabsList

Scrollable tab strip. Shows edge scroll buttons when triggers overflow and keeps the active tab in view.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `...props` | `ComponentPropsWithoutRef<typeof TabsPrimitive.List>` | – | Radix list props, including aria-label and loop. |

### TabsTrigger

A tab with a shared selection pill that glides between triggers.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` (required) | `string` | – | Matches a TabsContent value. |
| `...props` | `ComponentPropsWithoutRef<typeof TabsPrimitive.Trigger>` | – | Radix trigger props such as disabled. |

### TabsContent

Panel that slides in and morphs its height from the previous panel.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` (required) | `string` | – | Matches a TabsTrigger value. |
| `forceMount` | `true` | – | Keeps the panel mounted and skips the enter and exit animation. |
| `...props` | `ComponentPropsWithoutRef<typeof TabsPrimitive.Content>` | – | Radix content props such as className. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowLeft / ArrowRight | Moves between tabs and activates them. |
| Home / End | Jumps to the first or last tab. |
| Tab | Moves focus from the tab list into the active panel. |

## Accessibility

- Radix provides role="tablist", role="tab", role="tabpanel", aria-selected, and aria-controls.
- The outgoing panel is made inert while it fades so it cannot take focus.
- Overflow scroll buttons are labelled "Scroll tabs left" and "Scroll tabs right"; give TabsList an aria-label.

## Motion

- The selection pill glides between triggers on a morph spring, scoped to one Tabs instance.
- Panels slide 8px in the direction of travel while the height springs from the previous panel.
- Reduced motion swaps the pill instantly and crossfades panels in place without height animation.

## Responsive behavior

- The tab list keeps max-width 100% and scrolls horizontally when triggers overflow, with scroll buttons at the edges.
- Focusing a tab scrolls it into view, so keyboard navigation works on narrow screens.
- Triggers keep a 5.5rem minimum width, so many tabs overflow sooner on mobile.

## Performance

- ResizeObservers track list overflow and the active panel height; one per Tabs instance is cheap.
- The selection pill is a shared layout animation scoped to one Tabs; avoid dozens of tab sets on one screen.

## Notes for AI

- Use for peer views of the same object. Use segmented-control for a compact value toggle that is not tied to panels, and accordion for stacked sections.
- Every TabsTrigger value needs a matching TabsContent. Long lists scroll horizontally on their own.

## Related

- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.
- [Accordion](https://uiarc.dev/components/accordion/markdown): Progressively reveal supporting information in place.
- [Liquid tab bar](https://uiarc.dev/components/liquid-tab-bar/markdown): Tabs with a lens selection that slides between them, swaps labels cleanly, and fills in icons as it passes.
- [Morph nav](https://uiarc.dev/components/morph-nav/markdown): A navigation bar that morphs into rich menus, search, and a compact state as one surface.

## Also in navigation

- [Breadcrumb](https://uiarc.dev/components/breadcrumb/markdown): Show where a page sits in a hierarchy.
- [Pagination](https://uiarc.dev/components/pagination/markdown): Move through a long collection with clear bounds.
- [Mega menu morph](https://uiarc.dev/components/mega-menu-morph/markdown): A site header whose menus share one floating panel. Moving between sections morphs its size, slides the next menu in from that side and glides the caret under the new trigger, with hover intent, a safe triangle and an accordion sheet on narrow screens.

## Guidance for AI tools

Tabs: Switch between related content in the same context. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
