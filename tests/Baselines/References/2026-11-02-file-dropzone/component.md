# File dropzone

> A generous target for dropping one or more files.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/file-dropzone
- Markdown: https://uiarc.dev/components/file-dropzone/markdown
- Registry item: https://uiarc.dev/r/file-dropzone.json
- Source file: `registry/components/file-dropzone/file-dropzone.tsx`
- Dependencies: motion, lucide-react
- Keywords: field, files, react file upload, file dropzone, drag and drop upload, upload with progress, file uploader, dropzone component, multi file upload

## When to use

- Attachments and uploads with drag and drop plus a file picker.
- Uploads that need per-file progress, retry, and cancel through onUpload.

## When not to use

- Use input with type file only when a native picker with no list is enough.
- Use empty-state when the drop area is the only thing on an empty page and needs a larger call to action.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/file-dropzone
pnpm dlx shadcn@latest add @uiarc/file-dropzone
yarn dlx shadcn@latest add @uiarc/file-dropzone
bunx --bun shadcn@latest add @uiarc/file-dropzone
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/file-dropzone.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/file-dropzone/file-dropzone.tsx`

   The source is in the registry item: https://uiarc.dev/r/file-dropzone.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { FileDropzone } from "@/components/arc/file-dropzone/file-dropzone";

export function AttachmentUpload() {
  return (
    <FileDropzone
      accept=".pdf,image/*"
      maxFiles={3}
      maxSize={10 * 1024 * 1024}
      onUpload={async (item, { onProgress, signal }) => {
        await uploadFile(item.file!, { onProgress, signal });
      }}
    />
  );
}
```

## Examples

### Folding prompt with a size limit

```tsx
<FileDropzone
  label="Add receipts"
  accept="image/*,.pdf"
  maxFiles={10}
  maxSize={5 * 1024 * 1024}
  compactAt={2}
  listPlacement="inside"
  onFilesChange={setFiles}
/>
```

## API reference

### FileDropzone

A drop target and file picker with a file list that can run uploads with progress, retry, and removal.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `accept` | `string` | – | Accepted types, as in the native accept attribute (".pdf", "image/*"). Others are rejected with an error. |
| `multiple` | `boolean` | `true` | Allow several files. |
| `maxFiles` | `number` | `5` | Most files the list holds. |
| `onFilesChange` | `(files: File[]) => void` | – | Called with the selected File objects whenever the list changes. |
| `label` | `string` | `"Add files"` | Main prompt on the drop target. |
| `description` | `string` | `"Drop files here or choose from your device"` | Secondary prompt. |
| `defaultItems` | `FileDropzoneItem[]` | – | Rows present at mount, such as earlier uploads. They render without an entrance. |
| `onUpload` | `(item: FileDropzoneItem, options: { onProgress: (percent: number) => void; signal: AbortSignal }) => Promise<void>` | – | Uploads each added file. Report progress, resolve on success, reject with an Error whose message becomes the row's reason. Removing a row aborts the signal. |
| `maxSize` | `number` | – | Bytes. Larger files fail with a size reason and never upload. |
| `listPlacement` | `"below" \| "inside"` | `"below"` | Render the list under the target or inside its edge. |
| `note` | `string` | – | Replaces the small line under the description (defaults to accepted types or the file limit). |
| `dropLabel` | `string` | – | Label while files hover over the target. |
| `compactAt` | `number` | – | Once the list holds this many files, the prompt folds to a slim bar. |

### FileDropzoneItem

One row in the list, used by defaultItems and onUpload.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `id` (required) | `string` | – | Stable id. |
| `name` (required) | `string` | – | File name; its extension picks the type icon. |
| `size` (required) | `number` | – | Bytes. |
| `status` | `FileDropzoneStatus` | – | Upload state of the row. |
| `progress` | `number` | – | Upload percent. |
| `error` | `string` | – | Failure reason shown on the row. |
| `retryable` | `boolean` | – | Shows a retry button when failed. |
| `file` | `File` | – | The File, for items the visitor added. |
| `preview` | `string` | – | Thumbnail URL shown instead of the type icon. Image files added by the visitor get an object URL automatically, revoked when the row is removed or the component unmounts. Falls back to the icon if the image fails. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | On the drop target, opens the native file picker. |
| ArrowUp / ArrowDown | In the file list, moves focus to the same action (remove or retry) on the adjacent row. |
| Delete / Backspace | In the file list, removes the focused row. |
| ⌘ + V / Ctrl + V | Pastes files from the clipboard, such as a screenshot, while the pointer is over the dropzone or focus is inside it. Ignored inside text fields. |

## Accessibility

- The target is a native button; the hidden file input is removed from the tab order.
- Uploading rows expose role="progressbar" with aria-valuenow; remove and retry buttons are labelled with the file name.
- Additions, completions, and failures are announced through a polite role="status" region; rejection errors use role="alert".
- Removing a row moves focus to a neighbouring row so keyboard users are not dropped.

## Motion

- The icon lifts while files hover, the edge animates, rows enter and collapse on a smooth spring, and progress bars ease toward their target.
- Reduced motion replaces height and lift animations with short fades and keeps progress changes instant.

## Responsive behavior

- The file list is a container query: below 440px retry becomes icon-only, and below 300px the file icon hides.
- File names and failure reasons ellipsize instead of wrapping.
- The whole target is one tap area that opens the native picker, so it works without drag and drop on touch.

## Performance

- The dashed edge is refitted by a ResizeObserver, plus a rAF loop only while the corner radius is transitioning.
- Rows are not virtualized and maxFiles defaults to 5; row entrances stagger up to eight steps.

## Notes for AI

- Use for file attachments and uploads. Without onUpload the list shows plain selections; read them from onFilesChange and submit yourself.
- Files can be dropped, picked, or pasted. Pass preview on defaultItems (for example a stored thumbnail URL) to show earlier image uploads as thumbnails.
- With onUpload, the component manages per-file status, retry, and abort; wire it to fetch or XHR and pass signal through.
- Also exports formatFileSize(bytes) and the FileDropzoneItem, FileDropzoneStatus, and FileDropzoneUpload types.

## Related

- [Progress](https://uiarc.dev/components/progress/markdown): Show how much of a known task is complete.
- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.
- [Empty state](https://uiarc.dev/components/empty-state/markdown): A useful next step when there is nothing to show yet.

## Also in editors

- [Rich text editor](https://uiarc.dev/components/rich-text-editor/markdown): A lightweight editor with markdown shortcuts, a floating toolbar, a slash menu, and HTML and markdown output.
- [Signature pad](https://uiarc.dev/components/signature-pad/markdown): Smooth ink that thins with speed, with undo, replay, and PNG or SVG export.
- [Avatar upload](https://uiarc.dev/components/avatar-upload/markdown): Click or drop a photo onto the avatar to crop it in place with drag, pinch and zoom. It uploads through a progress ring and settles, with clear errors and retry.

## Guidance for AI tools

File dropzone: A generous target for dropping one or more files. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
