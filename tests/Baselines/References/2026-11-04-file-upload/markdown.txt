# File upload

> A complete file selection flow with constraints, progress, and error feedback.

- Type: Block
- Page: https://uiarc.dev/components/blocks/file-upload
- Markdown: https://uiarc.dev/components/blocks/file-upload/markdown

- Access: Free, open source
- Registry id: `file-upload`
- Source file: `registry/components/file-upload/file-upload.tsx`
- Built from: File picker, Progress, Feedback
- Keywords: react file upload, drag and drop upload, upload progress, file dropzone, multiple file upload, upload with retry

Use it for documents or media when people need to see upload progress and recover from invalid files.

## When to use

- Uploading files with per-file progress, retry, and remove.
- Attachments that must be validated by type and size before upload.
- Uploads that should abort when a file is removed, via the AbortSignal.

## When not to use

- Use file-dropzone when you only need to pick files without upload state.
- Use import-mapper when uploaded data needs column mapping.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/file-upload
pnpm dlx shadcn@latest add @uiarc/file-upload
yarn dlx shadcn@latest add @uiarc/file-upload
bunx --bun shadcn@latest add @uiarc/file-upload
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/file-upload.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/file-upload/file-upload.tsx`

   The source is in the registry item: https://uiarc.dev/r/file-upload.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { FileUpload } from "@/components/arc/file-upload/file-upload";

export function Attachments() {
  return (
    <FileUpload
      accept="image/*,.pdf"
      maxSize={10 * 1024 * 1024}
      onUpload={async (file, { onProgress, signal }) => {
        await uploadWithProgress(file, onProgress, signal);
      }}
    />
  );
}
```

## API reference

### FileUpload

A dropzone with a file list that validates type and size, runs your upload per file with progress, and supports retry and remove.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `accept` | `string` | – | Comma-separated extensions or MIME types, including wildcards like image/*. Rejected files stay listed with an error. |
| `maxSize` | `number` | – | Maximum size in bytes. Larger files are listed with an error. |
| `multiple` | `boolean` | `true` | Allows several files. When false a new file replaces the old one. |
| `disabled` | `boolean` | `false` | Disables the dropzone and input. |
| `label` | `string` | `"Upload files"` | Dropzone heading. |
| `description` | `string` | `"Drop files here or browse from your device."` | Helper text, linked with aria-describedby. |
| `value` | `FileUploadItem[]` | – | Controlled file list. |
| `onChange` | `(files: FileUploadItem[]) => void` | – | Called when files are added or removed. |
| `onUpload` | `(file: File, options: { onProgress: (percent: number) => void; signal: AbortSignal }) => Promise<void>` | – | Uploads each valid file. Report 0-100 through onProgress, resolve when done, reject to mark it failed. Removing a file aborts its signal. |

### FileUploadItem

Item type: { id: string; file: File; error?: string }.

No props.

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | Opens the file picker from the dropzone. |
| Tab | Moves through the dropzone and each row's retry and remove buttons. |

## Accessibility

- The dropzone is role=button with aria-disabled and aria-describedby for the helper text.
- An aria-live polite status line announces added, rejected, uploaded, failed, and removed files.
- Retry and remove buttons are labelled with the file name.
- After removing a file, focus moves to the neighbouring remove button or back to the dropzone.

## Motion

- The dropzone label swaps to Drop to add files while dragging; rows open and close their height on a spring.
- One spring drives the progress bar and the counted percentage, the bar folds away once the file lands, and a check draws in.
- Reduced motion jumps progress and replaces height and blur swaps with fades.

## Responsive behavior

- The dropzone and list fill their container; file names ellipsize on one line.
- On touch devices the dropzone opens the native file picker, since drag and drop is desktop only.

## Performance

- Uploads run through your onUpload, one call per valid file; there is no concurrency limit, so queue large batches yourself.
- The file list is not virtualized; keep it to a reasonable number of files.

## Notes for AI

- Use when files are uploaded with progress. Use file-dropzone when you only need to pick files without upload state.
- Wire onUpload to fetch or XHR and honour the AbortSignal; without onUpload files are only listed and validated.

## Related

- [File dropzone](https://uiarc.dev/components/file-dropzone/markdown): A generous target for dropping one or more files.
- [Progress](https://uiarc.dev/components/progress/markdown): Show how much of a known task is complete.

## Guidance for AI tools

Blocks are complete, self-contained screens with sample data. Replace the sample data and connect the callbacks described above. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
