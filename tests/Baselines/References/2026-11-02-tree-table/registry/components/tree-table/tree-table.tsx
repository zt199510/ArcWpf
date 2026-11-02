"use client";

import { useEffect, useId, useLayoutEffect, useMemo, useRef, useState, useSyncExternalStore } from "react";
import type { CSSProperties, KeyboardEvent as ReactKeyboardEvent, ReactNode, Ref } from "react";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import type { Transition } from "motion/react";
import { ArrowUp, ChevronRight, ChevronsDownUp, ChevronsUpDown, LoaderCircle, RotateCw } from "lucide-react";
import { Checkbox } from "../checkbox/checkbox";
import { motionTokens } from "../lib/motion-tokens";
import styles from "./tree-table.module.css";

export interface TreeTableRow<T> {
  id: string;
  data: T;
  /** Nested rows. Leave undefined and set hasChildren to load them on first expand with loadChildren. */
  children?: TreeTableRow<T>[];
  /** Marks a row whose children are not loaded yet. */
  hasChildren?: boolean;
}

export interface TreeTableCellContext<T> {
  row: TreeTableRow<T>;
  /** One for top level rows. */
  depth: number;
  expanded: boolean;
}

export interface TreeTableColumn<T> {
  id: string;
  header: string;
  cell: (data: T, context: TreeTableCellContext<T>) => ReactNode;
  /** Makes the column sortable. Siblings are sorted within their parent, so the hierarchy never breaks. */
  sortValue?: (data: T) => string | number;
  /** A grid track, such as "120px" or "minmax(0, 2fr)". Defaults to "minmax(0, 1fr)". */
  width?: string;
  align?: "start" | "end";
  /** Hides the column while the table is narrower than this many px. */
  hideBelow?: number;
}

export interface TreeTableSort { columnId: string; direction: "ascending" | "descending" }

/**
 * A table whose rows nest. Rows expand and collapse on a spring with indent guides, parents carry tri-state checkboxes,
 * sorting reorders siblings without breaking the hierarchy, and children can load on first expand. The first column
 * holds the tree. Keyboard follows the treegrid pattern: arrows move and expand, Space selects, Enter activates, * expands siblings.
 */
export interface TreeTableProps<T> {
  rows: TreeTableRow<T>[];
  columns: TreeTableColumn<T>[];
  /** Accessible name of the table. */
  label: string;
  expanded?: string[];
  defaultExpanded?: string[];
  onExpandedChange?: (ids: string[]) => void;
  /** Adds a checkbox column. Checking a parent checks everything under it. */
  selectable?: boolean;
  selected?: string[];
  defaultSelected?: string[];
  onSelectedChange?: (ids: string[]) => void;
  sort?: TreeTableSort | null;
  defaultSort?: TreeTableSort | null;
  onSortChange?: (sort: TreeTableSort | null) => void;
  /** Loads the children of a row with hasChildren the first time it expands. A rejected promise shows a retry row. */
  loadChildren?: (row: TreeTableRow<T>) => Promise<TreeTableRow<T>[]>;
  /** Names a row for its checkbox, such as the file or team name. */
  rowLabel?: (data: T) => string;
  /** Called when a row without children is clicked or gets Enter. Parent rows toggle instead. */
  onRowActivate?: (row: TreeTableRow<T>) => void;
  /** Caps the height; the header stays pinned while the body scrolls. */
  maxHeight?: number | string;
  emptyState?: ReactNode;
  className?: string;
  ref?: Ref<HTMLDivElement>;
}

type Line =
  | { kind: "row"; key: string; row: TreeTableRow<unknown>; depth: number; parentId: string | null; ancestors: string[]; position: number; size: number; expandable: boolean; expanded: boolean }
  | { kind: "loading" | "error"; key: string; parentId: string; depth: number; ancestors: string[]; position: number; size: number };

const ROW = 44, INDENT = 20, CHEVRON = 24;
const { spring, duration, ease } = motionTokens;
const cx = (...names: (string | false | undefined)[]) => names.filter(Boolean).join(" ");

const subscribe = () => () => {};
function useReducedFlag() {
  const hydrated = useSyncExternalStore(subscribe, () => true, () => false);
  return !!useReducedMotion() && hydrated;
}

function useControllable<V>(value: V | undefined, initial: V, onChange?: (next: V) => void) {
  const [inner, setInner] = useState(initial);
  const current = value !== undefined ? value : inner;
  const set = (next: V) => { if (value === undefined) setInner(next); onChange?.(next); };
  return [current, set] as const;
}

/** Ids of every expandable row whose children are known, for expand all. */
export function collectExpandableIds<T>(rows: TreeTableRow<T>[], loaded?: Record<string, TreeTableRow<T>[]>): string[] {
  return rows.flatMap(row => {
    const kids = row.children ?? loaded?.[row.id];
    return kids?.length ? [row.id, ...collectExpandableIds(kids, loaded)] : [];
  });
}

export function TreeTable<T>({
  rows, columns, label, expanded: expandedProp, defaultExpanded = [], onExpandedChange, selectable = false,
  selected: selectedProp, defaultSelected = [], onSelectedChange, sort: sortProp, defaultSort = null, onSortChange,
  loadChildren, rowLabel, onRowActivate, maxHeight, emptyState, className, ref,
}: TreeTableProps<T>) {
  const uid = useId().replace(/[^a-zA-Z0-9-]/g, "");
  const reduced = useReducedFlag();
  const rootRef = useRef<HTMLDivElement | null>(null);
  const rowRefs = useRef(new Map<string, HTMLDivElement>());
  const [expandedList, setExpandedList] = useControllable(expandedProp, defaultExpanded, onExpandedChange);
  const [selectedList, setSelectedList] = useControllable(selectedProp, defaultSelected, onSelectedChange);
  const [sort, setSort] = useControllable<TreeTableSort | null>(sortProp, defaultSort, onSortChange);
  const [loaded, setLoaded] = useState<Record<string, TreeTableRow<T>[]>>({});
  const [status, setStatus] = useState<Record<string, "loading" | "error">>({});
  const [arrived, setArrived] = useState<{ ids: string[]; at: string[] | null }>({ ids: [], at: null });
  const [focusId, setFocusId] = useState<string | null>(null);
  const [hoverId, setHoverId] = useState<string | null>(null);
  const [width, setWidth] = useState<number | null>(null);
  const [live, setLive] = useState("");
  const expanded = useMemo(() => new Set(expandedList), [expandedList]);
  const selected = useMemo(() => new Set(selectedList), [selectedList]);

  const kidsOf = (row: TreeTableRow<T>) => row.children ?? loaded[row.id];
  const canExpand = (row: TreeTableRow<T>) => !!kidsOf(row)?.length || (!!row.hasChildren && !!loadChildren && !kidsOf(row));

  useLayoutEffect(() => {
    const node = rootRef.current;
    if (!node) return;
    const observer = new ResizeObserver(() => setWidth(node.offsetWidth));
    observer.observe(node);
    setWidth(node.offsetWidth);
    return () => observer.disconnect();
  }, []);

  const shown = columns.filter(column => width === null || !column.hideBelow || width >= column.hideBelow);
  const template = [selectable ? "44px" : "", ...shown.map(column => column.width ?? "minmax(0, 1fr)")].filter(Boolean).join(" ");

  // Siblings sort inside their parent, so a sorted tree still reads top down.
  const sortColumn = sort ? columns.find(column => column.id === sort.columnId && column.sortValue) : undefined;
  const order = (list: TreeTableRow<T>[]) => {
    if (!sortColumn?.sortValue || !sort) return list;
    const value = sortColumn.sortValue, sign = sort.direction === "ascending" ? 1 : -1;
    return [...list].sort((a, b) => {
      const x = value(a.data), y = value(b.data);
      return sign * (typeof x === "number" && typeof y === "number" ? x - y : String(x).localeCompare(String(y), "en-US", { numeric: true }));
    });
  };

  // Every row in view, plus a loading or retry line under a parent that is still fetching.
  const lines: Line[] = [];
  const walk = (list: TreeTableRow<T>[], depth: number, parentId: string | null, ancestors: string[]) => {
    const sorted = order(list);
    sorted.forEach((row, index) => {
      const open = expanded.has(row.id);
      const expandable = canExpand(row);
      lines.push({ kind: "row", key: row.id, row: row as TreeTableRow<unknown>, depth, parentId, ancestors, position: index + 1, size: sorted.length, expandable, expanded: open && expandable });
      if (!open || !expandable) return;
      const kids = kidsOf(row);
      const next = [...ancestors, row.id];
      if (kids) walk(kids, depth + 1, row.id, next);
      else lines.push({ kind: status[row.id] === "error" ? "error" : "loading", key: `${row.id}:pending`, parentId: row.id, depth: depth + 1, ancestors: next, position: 1, size: 1 });
    });
  };
  walk(rows, 1, null, []);
  const rowLines = lines.filter((line): line is Extract<Line, { kind: "row" }> => line.kind === "row");
  const byId = new Map(rowLines.map(line => [line.key, line]));

  // When children arrive, the first one takes over the loading row's slot instead of both squeezing past each other.
  // The handoff only holds until the expanded set changes, so a later collapse and expand grows rows as usual.
  const handoff = new Set(arrived.at === expandedList ? arrived.ids.filter(id => byId.get(id)?.expanded) : []);

  // Tri-state: a parent is checked when all its known children are, and mixed when only some are.
  const index = useMemo(() => {
    const parent = new Map<string, string | null>(), children = new Map<string, string[]>(), all: string[] = [];
    const visit = (list: TreeTableRow<T>[], parentId: string | null) => list.forEach(row => {
      parent.set(row.id, parentId); all.push(row.id);
      const kids = row.children ?? loaded[row.id];
      if (kids?.length) { children.set(row.id, kids.map(kid => kid.id)); visit(kids, row.id); }
    });
    visit(rows, null);
    return { parent, children, all };
  }, [loaded, rows]);
  const mixed = useMemo(() => {
    const some = new Set<string>();
    const visit = (id: string): boolean => {
      const kids = index.children.get(id);
      let any = selected.has(id);
      kids?.forEach(kid => { if (visit(kid)) any = true; });
      if (any && !selected.has(id)) some.add(id);
      return any;
    };
    rows.forEach(row => visit(row.id));
    return some;
  }, [index, rows, selected]);

  function normalize(next: Set<string>) {
    const settle = (id: string): boolean => {
      const kids = index.children.get(id);
      if (!kids) return next.has(id);
      const states = kids.map(settle);
      if (states.every(Boolean)) next.add(id); else next.delete(id);
      return next.has(id);
    };
    rows.forEach(row => settle(row.id));
    return next;
  }
  function descendants(id: string): string[] { return (index.children.get(id) ?? []).flatMap(kid => [kid, ...descendants(kid)]); }
  function toggleSelect(id: string, on = !selected.has(id)) {
    const next = new Set(selected);
    [id, ...descendants(id)].forEach(member => on ? next.add(member) : next.delete(member));
    setSelectedList([...normalize(next)]);
  }
  const allOn = rows.length > 0 && rows.every(row => selected.has(row.id));
  const someOn = !allOn && index.all.some(id => selected.has(id));
  function toggleAll() {
    setSelectedList(allOn ? [] : [...normalize(new Set(index.all))]);
    setLive(allOn ? "Selection cleared" : "All rows selected");
  }

  function fetchChildren(row: TreeTableRow<T>) {
    if (!loadChildren || status[row.id] === "loading") return;
    setStatus(current => ({ ...current, [row.id]: "loading" }));
    setLive("Loading");
    loadChildren(row).then(kids => {
      setLoaded(current => ({ ...current, [row.id]: kids }));
      const at = expandedRef.current;
      setArrived(current => ({ ids: current.at === at ? [...current.ids, row.id] : [row.id], at }));
      setStatus(current => { const next = { ...current }; delete next[row.id]; return next; });
      setLive(`${kids.length} ${kids.length === 1 ? "row" : "rows"} loaded`);
      // Children that arrive under a checked parent start checked.
      if (selectedRef.current.has(row.id)) {
        const add = (list: TreeTableRow<T>[]): string[] => list.flatMap(kid => [kid.id, ...(kid.children ? add(kid.children) : [])]);
        selectRef.current([...new Set([...selectedRef.current, ...add(kids)])]);
      }
    }, () => {
      setStatus(current => ({ ...current, [row.id]: "error" }));
      setLive("Couldn't load rows");
    });
  }
  const selectedRef = useRef(selected);
  const selectRef = useRef(setSelectedList);
  const fetchRef = useRef(fetchChildren);
  const byIdRef = useRef(byId);
  const expandedRef = useRef(expandedList);
  useEffect(() => { expandedRef.current = expandedList; selectedRef.current = selected; selectRef.current = setSelectedList; fetchRef.current = fetchChildren; byIdRef.current = byId; });

  // Rows opened from outside, such as a controlled expand all, still fetch their children.
  const waiting = lines.filter(line => line.kind === "loading" && !status[line.parentId]).map(line => line.parentId).join(" ");
  useEffect(() => {
    if (!waiting) return;
    waiting.split(" ").forEach(id => { const line = byIdRef.current.get(id); if (line) fetchRef.current(line.row as TreeTableRow<T>); });
  }, [waiting]);

  function setOpen(row: TreeTableRow<T>, open: boolean) {
    if (open === expanded.has(row.id)) return;
    const next = new Set(expanded);
    if (open) next.add(row.id); else next.delete(row.id);
    setExpandedList([...next]);
    if (open && !kidsOf(row) && row.hasChildren) fetchChildren(row);
  }

  const expandable = collectExpandableIds(rows, loaded);
  const everyOpen = expandable.length > 0 && expandable.every(id => expanded.has(id));
  function toggleEverything() {
    setExpandedList(everyOpen ? [] : [...new Set([...expandedList, ...expandable])]);
    setLive(everyOpen ? "Collapsed all rows" : "Expanded all rows");
  }

  function cycleSort(column: TreeTableColumn<T>) {
    const next: TreeTableSort | null = sort?.columnId !== column.id ? { columnId: column.id, direction: "ascending" }
      : sort.direction === "ascending" ? { columnId: column.id, direction: "descending" } : null;
    setSort(next);
    setLive(next ? `Sorted by ${column.header.toLowerCase()}, ${next.direction}` : "Sort cleared");
  }

  // Keep a real row focusable, and pull focus up to the nearest ancestor when its row collapses away.
  const current = focusId && byId.has(focusId) ? focusId : null;
  const fallback = focusId && !current ? (() => {
    let id: string | null | undefined = focusId;
    while (id && !byId.has(id)) id = index.parent.get(id);
    return id ?? rowLines[0]?.key ?? null;
  })() : null;
  const tabId = current ?? fallback ?? rowLines[0]?.key ?? null;
  useEffect(() => {
    if (!fallback || !rootRef.current?.contains(document.activeElement)) return;
    setFocusId(fallback);
    rowRefs.current.get(fallback)?.focus({ preventScroll: true });
  }, [fallback]);

  function focusRow(id: string | undefined) {
    if (!id) return;
    setFocusId(id);
    const node = rowRefs.current.get(id);
    node?.focus({ preventScroll: true });
    node?.scrollIntoView({ block: "nearest" });
  }

  function onRowKey(event: ReactKeyboardEvent<HTMLDivElement>, line: Extract<Line, { kind: "row" }>) {
    if (event.target !== event.currentTarget) return;
    const at = rowLines.indexOf(line);
    const row = line.row as TreeTableRow<T>;
    const go = (to: number) => { event.preventDefault(); focusRow(rowLines[Math.max(0, Math.min(rowLines.length - 1, to))]?.key); };
    switch (event.key) {
      case "ArrowDown": return go(at + 1);
      case "ArrowUp": return go(at - 1);
      case "Home": return go(0);
      case "End": return go(rowLines.length - 1);
      case "PageDown": return go(at + 10);
      case "PageUp": return go(at - 10);
      case "ArrowRight":
        event.preventDefault();
        if (line.expandable && !line.expanded) setOpen(row, true);
        else if (line.expanded) focusRow(rowLines[at + 1]?.parentId === line.key ? rowLines[at + 1]!.key : undefined);
        return;
      case "ArrowLeft":
        event.preventDefault();
        if (line.expanded) setOpen(row, false);
        else if (line.parentId) focusRow(line.parentId);
        return;
      case "*": {
        event.preventDefault();
        const siblings = rowLines.filter(other => other.parentId === line.parentId && other.expandable).map(other => other.row as TreeTableRow<T>);
        setExpandedList([...new Set([...expandedList, ...siblings.map(sibling => sibling.id)])]);
        siblings.forEach(sibling => { if (!kidsOf(sibling) && sibling.hasChildren) fetchChildren(sibling); });
        return;
      }
      case " ":
        if (!selectable) return;
        event.preventDefault();
        toggleSelect(line.key);
        return;
      case "Enter":
        event.preventDefault();
        if (status[line.key] === "error") fetchChildren(row);
        else if (line.expandable) setOpen(row, !line.expanded);
        else onRowActivate?.(row);
        return;
      case "a":
        if (selectable && (event.metaKey || event.ctrlKey)) { event.preventDefault(); toggleAll(); }
        return;
    }
  }

  // The branch around the row under the pointer or focus lights its guide, so a deep row shows which parent it belongs to.
  const active = hoverId ?? current;
  const activeBranch = active ? (byId.get(active)?.expanded ? active : byId.get(active)?.parentId) : null;
  const sortKey = sort ? `${sort.columnId}:${sort.direction}` : "none";
  const height: Transition = reduced ? { duration: 0 } : spring.smooth;
  const fade = (delay: number): Transition => reduced ? { duration: duration.instant } : { ...height, opacity: { duration: duration.standard, ease: [...ease.enter], delay } };
  const exit = reduced ? { opacity: 0, transition: { duration: duration.instant } } : { height: 0, opacity: 0, transition: { height, opacity: { duration: duration.fast, ease: [...ease.standard] } } };
  // A loading row whose children just arrived leaves at once; its first child already fills the slot.
  const pendingVariants = (parentId: string) => ({
    leave: (taken?: Set<string>) => taken?.has(parentId) ? { height: 0, opacity: 0, transition: { duration: 0 } } : exit,
  });
  const style = { "--tt-columns": template, "--tt-row": `${ROW}px`, "--tt-indent": `${INDENT}px`, "--tt-chevron": `${CHEVRON}px`, "--tt-max": maxHeight === undefined ? "none" : typeof maxHeight === "number" ? `${maxHeight}px` : maxHeight } as CSSProperties;
  const [treeColumn, ...restColumns] = shown;

  const guides = (ancestors: string[], depth: number) => ancestors.slice(0, depth - 1).map((id, level) =>
    <span key={id} className={styles.guide} data-active={id === activeBranch || undefined} style={{ left: level * INDENT + CHEVRON / 2 }} aria-hidden="true" />);

  return <div ref={node => { rootRef.current = node; if (typeof ref === "function") ref(node); else if (ref) ref.current = node; }}
    className={cx(styles.root, className)} style={style}>
    <div className={styles.scroller}>
      <div role="treegrid" aria-label={label} aria-multiselectable={selectable || undefined} aria-rowcount={rowLines.length + 1} className={styles.grid}>
        <div role="row" className={styles.header}>
          {selectable && <div role="columnheader" className={styles.checkCell}>
            <Checkbox aria-label={allOn ? "Clear selection" : "Select all rows"} tabIndex={-1} checked={allOn ? true : someOn ? "indeterminate" : false} onCheckedChange={toggleAll} />
          </div>}
          {shown.map(column => {
            const sorted = sort?.columnId === column.id ? sort.direction : undefined;
            return <div key={column.id} role="columnheader" aria-sort={column.sortValue ? sorted ?? "none" : undefined} className={styles.headerCell} data-align={column.align} data-tree={column === treeColumn || undefined}>
              {column === treeColumn && expandable.length === 0 && <span className={styles.leaf} aria-hidden="true" />}
              {column === treeColumn && expandable.length > 0 && <button type="button" className={styles.expandAll} onClick={toggleEverything} aria-label={everyOpen ? "Collapse all" : "Expand all"} title={everyOpen ? "Collapse all" : "Expand all"}>
                <AnimatePresence mode="popLayout" initial={false}>
                  <motion.span key={everyOpen ? "in" : "out"} className={styles.glyph} initial={reduced ? { opacity: 0 } : { opacity: 0, scale: .7, rotate: everyOpen ? -90 : 90 }} animate={{ opacity: 1, scale: 1, rotate: 0 }}
                    exit={reduced ? { opacity: 0 } : { opacity: 0, scale: .7, rotate: everyOpen ? 90 : -90 }} transition={reduced ? { duration: duration.instant } : spring.snappy}>
                    {everyOpen ? <ChevronsDownUp size={16} strokeWidth={1.75} aria-hidden="true" /> : <ChevronsUpDown size={16} strokeWidth={1.75} aria-hidden="true" />}
                  </motion.span>
                </AnimatePresence>
              </button>}
              {column.sortValue ? <button type="button" className={styles.sort} data-sorted={sorted ? "" : undefined} onClick={() => cycleSort(column)}>
                <span className={styles.headerLabel}>{column.header}</span>
                <motion.span className={styles.sortIcon} initial={false} aria-hidden="true"
                  animate={{ opacity: sorted ? 1 : 0, rotate: sorted === "descending" ? 180 : 0, y: sorted || reduced ? 0 : 2 }}
                  transition={reduced ? { duration: 0 } : spring.snappy}><ArrowUp size={14} strokeWidth={1.75} /></motion.span>
              </button> : <span className={styles.headerLabel}>{column.header}</span>}
            </div>;
          })}
        </div>

        {rowLines.length === 0 && <div role="row" className={styles.empty}><div role="gridcell">{emptyState ?? "Nothing to show"}</div></div>}

        <AnimatePresence initial={false} custom={handoff}>
          {lines.map(line => {
            const takesOver = line.kind === "row" && line.position === 1 && !!line.parentId && handoff.has(line.parentId);
            const enter = reduced ? { opacity: 0 } : { height: takesOver ? ROW : 0, opacity: 0 };
            const delay = reduced ? 0 : Math.min(line.position - 1, 8) * .018;
            if (line.kind !== "row") {
              const parent = byId.get(line.parentId);
              return <motion.div key={line.key} role="row" aria-level={line.depth} className={styles.row} data-pending={line.kind}
                initial={enter} animate={{ height: ROW, opacity: 1 }} variants={pendingVariants(line.parentId)} exit="leave" transition={fade(0)}>
                {selectable && <div role="gridcell" className={styles.checkCell} />}
                <div role="gridcell" className={styles.treeCell} style={{ paddingLeft: (line.depth - 1) * INDENT }}>
                  {guides(line.ancestors, line.depth)}
                  {line.kind === "loading" ? <span className={styles.pending}><LoaderCircle className={styles.spin} size={16} strokeWidth={1.75} aria-hidden="true" />Loading</span>
                    : <span className={styles.pending} data-tone="danger">Couldn&rsquo;t load
                      <button type="button" className={styles.retry} onClick={() => parent && fetchChildren(parent.row as TreeTableRow<T>)}><RotateCw size={14} strokeWidth={1.75} aria-hidden="true" />Retry</button>
                    </span>}
                </div>
                {restColumns.map(column => <div key={column.id} role="gridcell" className={styles.cell} data-align={column.align}>
                  {line.kind === "loading" && <span className={styles.skeleton} aria-hidden="true" />}
                </div>)}
              </motion.div>;
            }
            const row = line.row as TreeTableRow<T>;
            const context: TreeTableCellContext<T> = { row, depth: line.depth, expanded: line.expanded };
            const checked = selected.has(line.key) ? true : mixed.has(line.key) ? "indeterminate" as const : false;
            const loading = status[line.key] === "loading";
            return <motion.div key={line.key} ref={node => { if (node) rowRefs.current.set(line.key, node); else rowRefs.current.delete(line.key); }}
              role="row" aria-level={line.depth} aria-posinset={line.position} aria-setsize={line.size} aria-expanded={line.expandable ? line.expanded : undefined}
              aria-selected={selectable ? checked === true : undefined} aria-busy={loading || undefined}
              tabIndex={tabId === line.key ? 0 : -1} className={styles.row} data-selected={checked === true || undefined} data-expandable={line.expandable || undefined}
              initial={enter} animate={{ height: ROW, opacity: 1 }} exit={exit} transition={fade(delay)}
              layout="position" layoutDependency={sortKey}
              onFocus={event => { if (event.target === event.currentTarget) setFocusId(line.key); }}
              onPointerEnter={() => setHoverId(line.key)} onPointerLeave={() => setHoverId(id => id === line.key ? null : id)}
              onClick={() => { setFocusId(line.key); if (line.expandable) setOpen(row, !line.expanded); else onRowActivate?.(row); }}
              onKeyDown={event => onRowKey(event, line)}>
              {selectable && <div role="gridcell" className={styles.checkCell} onClick={event => event.stopPropagation()}>
                <Checkbox aria-label={rowLabel ? `Select ${rowLabel(row.data)}` : `Select row ${line.position} of ${line.size}`} tabIndex={-1} checked={checked} onCheckedChange={() => toggleSelect(line.key)} />
              </div>}
              {treeColumn && <div role="gridcell" className={styles.treeCell} style={{ paddingLeft: (line.depth - 1) * INDENT }} data-align={treeColumn.align}>
                {guides(line.ancestors, line.depth)}
                {line.expandable ? <button type="button" tabIndex={-1} aria-hidden="true" className={styles.chevron}
                  onClick={event => { event.stopPropagation(); setFocusId(line.key); setOpen(row, !line.expanded); }}>
                  {loading ? <LoaderCircle className={styles.spin} size={16} strokeWidth={1.75} />
                    : <motion.span className={styles.glyph} initial={false} animate={{ rotate: line.expanded ? 90 : 0 }} transition={reduced ? { duration: 0 } : spring.snappy}>
                      <ChevronRight size={16} strokeWidth={1.75} />
                    </motion.span>}
                </button> : <span className={styles.leaf} aria-hidden="true" />}
                <span className={styles.treeContent}>{treeColumn.cell(row.data, context)}</span>
              </div>}
              {restColumns.map(column => <div key={column.id} role="gridcell" className={styles.cell} data-align={column.align}>{column.cell(row.data, context)}</div>)}
            </motion.div>;
          })}
        </AnimatePresence>
      </div>
    </div>
    <p id={`${uid}-live`} className={styles.srOnly} aria-live="polite">{live}</p>
  </div>;
}

export default TreeTable;
