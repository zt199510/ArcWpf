# Onboarding checklist

> Guide a new workspace through setup, one step at a time.

- Type: Component (feedback)
- Access: Free, open source
- Page: https://uiarc.dev/components/onboarding-checklist
- Markdown: https://uiarc.dev/components/onboarding-checklist/markdown
- Registry item: https://uiarc.dev/r/onboarding-checklist.json
- Source file: `registry/components/onboarding-checklist/onboarding-checklist.tsx`
- Dependencies: motion, lucide-react
- Keywords: onboarding, checklist, getting started, react onboarding checklist, getting started checklist, setup checklist, user onboarding, progress checklist, task list onboarding

## When to use

- A one-time getting-started card with three to eight loosely ordered steps.
- Setup steps that run async actions, like Connect GitHub, and check off when they resolve.
- Checklists people can hide and come back to, with state persisted through callbacks.

## When not to use

- Use stepper when the order is strict and steps map to screens.
- Use multi-step-form when every step collects form input.
- Use accordion for plain collapsible content without completion.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/onboarding-checklist
pnpm dlx shadcn@latest add @uiarc/onboarding-checklist
yarn dlx shadcn@latest add @uiarc/onboarding-checklist
bunx --bun shadcn@latest add @uiarc/onboarding-checklist
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/onboarding-checklist.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/onboarding-checklist/onboarding-checklist.tsx`

   The source is in the registry item: https://uiarc.dev/r/onboarding-checklist.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { OnboardingChecklist } from "@/components/arc/onboarding-checklist/onboarding-checklist";

export function GettingStarted() {
  return (
    <OnboardingChecklist
      title="Set up your workspace"
      defaultCompleted={savedIds}
      onCompletedChange={saveIds}
      steps={[
        { id: "invite", title: "Invite your team", actionLabel: "Invite", onAction: openInvite },
        { id: "connect", title: "Connect GitHub", description: "Sync issues and PRs.", actionLabel: "Connect", onAction: connectGithub },
        { id: "profile", title: "Add a profile photo" },
      ]}
    />
  );
}
```

## API reference

### OnboardingChecklist

A getting-started card with a progress ring, expandable steps with actions, and a folding completed group.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `title` (required) | `string` | – | Card title while steps remain. |
| `steps` (required) | `{ id: string; title: string; description?: string; actionLabel?: string; onAction?: () => void \| Promise<void> }[]` | – | Steps in order. onAction resolving checks the step; throwing an Error keeps it open and shows the message. |
| `doneTitle` | `string` | `"You’re all set"` | Title once every step is done. |
| `defaultCompleted` | `string[]` | `[]` | Step ids that start completed. |
| `onCompletedChange` | `(completed: string[]) => void` | – | Called with completed ids in step order whenever they change. |
| `defaultHidden` | `boolean` | `false` | Starts with the body collapsed. |
| `onHiddenChange` | `(hidden: boolean) => void` | – | Called when the Hide or Show toggle is used. |
| `className` | `string` | – | Class on the card. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowDown / ArrowUp | Moves between step titles. |
| Home / End | Jumps to the first or last step title. |
| Enter / Space | Expands a step, toggles its checkbox, or runs its action. |

## Accessibility

- Each step has a role="checkbox" button with aria-checked, and a title button with aria-expanded controlling a labelled region.
- The ring is role="progressbar" with aria-valuetext like "3 of 5 steps done"; progress and errors are announced via role="status" and role="alert".
- When a focused step finishes, focus moves to the next step's action; leaving rows and the hidden body are inert.

## Motion

- Checks fill from the center and draw their tick; after 720ms the finished step folds into the completed group while the ring and count roll forward.
- Rows and panels open and close on a non-overshooting spring; the title morphs to doneTitle and a tick draws in the ring at 100%.
- Reduced motion removes height and scale animation and keeps short fades.

## Responsive behavior

- The card is a container query context; below 380px the Hide button collapses to an icon.
- Step titles and checkboxes keep 40px targets, and the completed group toggle is 44px tall.

## Performance

- Steps are not virtualized; the component is built for short lists.
- Each finished step waits 720ms on a timer before folding into the completed group.

## Notes for AI

- Use for a short, one-time setup of three to eight loosely ordered steps. Use stepper when the order is strict and steps map to screens.
- Make onAction async and throw an Error with a readable message on failure. Persist onCompletedChange and onHiddenChange to restore state via the default props.

## Related

- [Stepper](https://uiarc.dev/components/stepper/markdown): Show where a person is in a multi-step flow and what is done.
- [Progress](https://uiarc.dev/components/progress/markdown): Show how much of a known task is complete.
- [Accordion](https://uiarc.dev/components/accordion/markdown): Progressively reveal supporting information in place.
- [Checkbox](https://uiarc.dev/components/checkbox/markdown): A binary choice with a precise, legible state.

## Also in progress

- [Skeleton](https://uiarc.dev/components/skeleton/markdown): Reserve space while content is still loading.
- [Countdown](https://uiarc.dev/components/countdown/markdown): A launch countdown with rolling digits that morphs into a live state at zero.
- [Usage meter](https://uiarc.dev/components/usage-meter/markdown): Show what fills an allowance and how close it is to the limit.

## Guidance for AI tools

Onboarding checklist: Guide a new workspace through setup, one step at a time. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
