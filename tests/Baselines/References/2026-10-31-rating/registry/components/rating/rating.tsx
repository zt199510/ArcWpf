"use client";

import { useEffect, useId, useRef, useState } from "react";
import type { KeyboardEvent, MouseEvent, PointerEvent } from "react";
import { Star } from "lucide-react";
import { AnimatePresence, animate, motion, useMotionValue, useReducedMotion, useTransform } from "motion/react";
import type { Variants } from "motion/react";
import { motionTokens } from "../lib/motion-tokens";
import styles from "./rating.module.css";

export interface RatingProps {
  label: string;
  value?: number;
  defaultValue?: number;
  /** Receives the committed value; 0 means the rating was cleared. */
  onChange?: (value: number) => void;
  max?: number;
  readOnly?: boolean;
  disabled?: boolean;
  description?: string;
  size?: "sm" | "md" | "lg";
  className?: string;
  /** Pointer, touch, and arrow keys move in half stars. */
  allowHalf?: boolean;
  /** Choosing the current value again clears it; Backspace and Delete clear from the keyboard. */
  allowClear?: boolean;
  /** Show the word or number for the previewed value beside the stars. */
  showValue?: boolean;
  /** One word per whole star. Five whole stars default to "Poor" through "Excellent"; anything else reads "4 out of 5". */
  valueLabels?: readonly string[];
  /** Shown beside empty stars. */
  emptyLabel?: string;
  /** Submits the value with a surrounding form. */
  name?: string;
}

const defaultWords = ["Poor", "Fair", "Good", "Great", "Excellent"] as const;
/** spring.snappy in physical terms. Time-defined springs drop initial velocity, and a pop is nothing but velocity. */
const snappyRoot = (2 * Math.PI) / (motionTokens.spring.snappy.visualDuration * 1.2);
const popSpring = { type: "spring", stiffness: snappyRoot ** 2, damping: 2 * (1 - motionTokens.spring.snappy.bounce) * snappyRoot, mass: 1 } as const;
/** Scale per second handed to a resting star: it peaks near 1.18, then the spring settles it back to 1. */
const popVelocity = 9;
/** Commits closer together than this pop only the chosen star, so held arrow keys stay calm. */
const waveGap = 260;
const still = { duration: 0 } as const;

/** Changed words roll in the direction the value moved; unchanged words stay put. */
const wordMotion: Variants = {
  enter: (direction: number) => ({ opacity: 0, y: `${direction * .3}em`, filter: `blur(${motionTokens.blur.soft}px)` }),
  center: { opacity: 1, y: 0, filter: "blur(0px)", transition: { duration: motionTokens.duration.standard, ease: [...motionTokens.ease.enter] } },
  exit: (direction: number) => ({ opacity: 0, y: `${direction * -.25}em`, filter: `blur(${motionTokens.blur.subtle}px)`, transition: { duration: motionTokens.duration.instant, ease: [...motionTokens.ease.standard] } }),
};

function RollingText({ text, direction, reduced }: { text: string; direction: number; reduced: boolean }) {
  const words = text.split(/(\s+)/);
  return <span className={styles.roll}>
    <span className={styles.srOnly}>{text}</span>
    <span aria-hidden="true"><AnimatePresence mode="popLayout" initial={false} custom={direction}>
      {words.map((word, index) => <motion.span key={`${index}:${word}`} className={styles.word} custom={direction} variants={wordMotion} initial={reduced ? false : "enter"} animate="center" exit={reduced ? undefined : "exit"}>{word}</motion.span>)}
    </AnimatePresence></span>
  </span>;
}

const clip = (fraction: number) => `inset(0 ${Math.min(100, Math.max(0, (1 - fraction) * 100))}% 0 0)`;

interface GlyphProps { fill: number; kept: number; delay: number; soft: boolean; pressed: boolean; pop: { key: number; delay: number } | null; reduced: boolean }

/** One star in three layers: the empty outline, a faint mark of the committed value, and the live fill that sweeps left to right. */
function StarGlyph({ fill, kept, delay, soft, pressed, pop, reduced }: GlyphProps) {
  const fillValue = useMotionValue(fill);
  const keptValue = useMotionValue(kept);
  const scale = useMotionValue(1);
  const fillClip = useTransform(fillValue, clip);
  const keptClip = useTransform(keptValue, clip);
  const targets = useRef({ fill, kept, pop: pop?.key });
  useEffect(() => {
    if (targets.current.fill === fill) return;
    targets.current.fill = fill;
    animate(fillValue, fill, reduced ? still : { ...motionTokens.spring.snappy, delay });
  }, [fill, delay, reduced, fillValue]);
  useEffect(() => {
    if (targets.current.kept === kept) return;
    targets.current.kept = kept;
    animate(keptValue, kept, reduced ? still : motionTokens.spring.smooth);
  }, [kept, reduced, keptValue]);
  const popKey = pop?.key;
  const popDelay = pop?.delay ?? 0;
  useEffect(() => {
    if (popKey === undefined || targets.current.pop === popKey) return;
    targets.current.pop = popKey;
    if (!reduced) animate(scale, 1, { ...popSpring, velocity: popVelocity, delay: popDelay });
  }, [popKey, popDelay, reduced, scale]);
  return <motion.span className={styles.lift} initial={false} animate={{ scale: pressed && !reduced ? .86 : soft ? .9 : 1 }} transition={reduced ? still : motionTokens.spring.snappy} aria-hidden="true">
    <motion.span className={styles.pop} style={{ scale }}>
      <span className={styles.base}><Star strokeWidth={1.75} /></span>
      <motion.span className={styles.kept} style={{ clipPath: keptClip }}><Star strokeWidth={1.75} /></motion.span>
      <motion.span className={styles.fill} style={{ clipPath: fillClip }}><Star strokeWidth={1.75} /></motion.span>
    </motion.span>
  </motion.span>;
}

type Gesture = { start: number; last: number; scrub: boolean; touch: boolean };

export function Rating({ label, value, defaultValue = 0, onChange, max = 5, readOnly = false, disabled = false, description, size = "md", className, allowHalf = false, allowClear = true, showValue = true, valueLabels, emptyLabel = "Not rated", name }: RatingProps) {
  const generatedId = useId();
  const labelId = `${generatedId}-label`;
  const descriptionId = `${generatedId}-description`;
  const reduced = useReducedMotion() ?? false;
  const step = allowHalf ? .5 : 1;
  const normalize = (next: number) => Math.max(0, Math.min(max, Math.round(next / step) * step));
  const [internalValue, setInternalValue] = useState(defaultValue);
  const [preview, setPreview] = useState<number | null>(null);
  const [pressed, setPressed] = useState<number | null>(null);
  const [pulse, setPulse] = useState<{ key: number; value: number; wave: boolean } | null>(null);
  const [announcement, setAnnouncement] = useState("");
  const currentValue = normalize(value ?? internalValue);
  const displayValue = preview ?? currentValue;
  // Remember where the shown value came from, so the fill sweeps away from it and the words roll the same way.
  const [sweep, setSweep] = useState({ from: displayValue, to: displayValue });
  if (sweep.to !== displayValue) setSweep({ from: sweep.to, to: displayValue });
  const direction = sweep.to >= sweep.from ? 1 : -1;
  const cells = useRef<(HTMLSpanElement | null)[]>([]);
  const radios = useRef<(HTMLButtonElement | null)[]>([]);
  const group = useRef<HTMLDivElement>(null);
  const gesture = useRef<Gesture | null>(null);
  // After a pointer commit the hovered preview steps aside until the pointer reaches a different value.
  const heldAt = useRef<number | null>(null);
  const suppressClickUntil = useRef(0);
  const lastCommit = useRef(0);
  const interactive = !readOnly && !disabled;

  const words = valueLabels ?? (max === 5 && !allowHalf ? defaultWords : undefined);
  const wordFor = (next: number) => Number.isInteger(next) ? words?.[next - 1] : undefined;
  const textFor = (next: number) => next <= 0 ? emptyLabel : wordFor(next) ?? `${next} out of ${max}`;
  const spokenFor = (next: number) => [wordFor(next), `${next} of ${max} stars`].filter(Boolean).join(", ");
  const steps = Array.from({ length: Math.round(max / step) }, (_, index) => (index + 1) * step);

  /** `time` is the event's timeStamp, which shares performance.now()'s clock. */
  const commit = (next: number, time: number, { toggle = false, announce = true } = {}) => {
    if (!interactive) return;
    let chosen = normalize(next);
    if (toggle && allowClear && chosen === currentValue) chosen = 0;
    if (!allowClear) chosen = Math.max(step, chosen);
    if (preview !== null) heldAt.current = preview;
    setPreview(null);
    setPulse({ key: time, value: chosen, wave: time - lastCommit.current > waveGap });
    lastCommit.current = time;
    if (announce) setAnnouncement(chosen === 0 ? "Rating cleared" : spokenFor(chosen));
    if (chosen === currentValue) return;
    setInternalValue(chosen);
    onChange?.(chosen);
  };

  /** Maps a pointer position to a value; dragging past the first star clears when that is allowed. */
  const valueAt = (x: number) => {
    for (let index = 0; index < max; index += 1) {
      const rect = cells.current[index]?.getBoundingClientRect();
      if (!rect) continue;
      if (index === 0 && x < rect.left - rect.width / 2) return allowClear ? 0 : step;
      if (x <= rect.right + 1 || index === max - 1) return allowHalf && x < rect.left + rect.width / 2 ? index + .5 : index + 1;
    }
    return currentValue;
  };
  const starOf = (next: number) => next > 0 ? Math.ceil(next) - 1 : null;

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    if (!interactive || event.button !== 0) return;
    const at = valueAt(event.clientX);
    const touch = event.pointerType !== "mouse";
    gesture.current = { start: at, last: at, scrub: false, touch };
    // Touch keeps following the finger once it leaves the stars; a mouse keeps its hover path.
    if (touch) { try { event.currentTarget.setPointerCapture(event.pointerId); } catch { /* capture is a nicety */ } setPreview(at); }
    setPressed(starOf(at));
  };
  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    if (!interactive) return;
    const at = valueAt(event.clientX);
    const active = gesture.current;
    if (active) {
      if (at !== active.last) { active.last = at; active.scrub = true; }
      setPressed(starOf(at));
      if (active.touch || active.scrub) setPreview(at);
      return;
    }
    if (event.pointerType !== "mouse") return;
    if (heldAt.current === at) return;
    heldAt.current = null;
    setPreview(at);
  };
  const endGesture = (event: PointerEvent<HTMLDivElement>, cancelled = false) => {
    const active = gesture.current;
    gesture.current = null;
    setPressed(null);
    if (!active) return;
    if (!cancelled && (active.touch || active.scrub)) {
      const at = valueAt(event.clientX);
      // The browser may still send a click for this press; the pointer already made the choice.
      suppressClickUntil.current = event.timeStamp + 500;
      commit(at, event.timeStamp, { toggle: !active.scrub });
      heldAt.current = active.touch ? null : at;
      // A drag that began on another star leaves focus behind; keep it on the checked radio.
      if (active.scrub && group.current?.contains(document.activeElement)) radios.current[Math.max(0, Math.round(normalize(at) / step) - 1)]?.focus({ preventScroll: true });
    }
    if (active.touch) setPreview(null);
  };
  const onPointerLeave = (event: PointerEvent<HTMLDivElement>) => {
    if (gesture.current?.touch) return;
    gesture.current = null;
    heldAt.current = null;
    setPressed(null);
    if (event.pointerType === "mouse") setPreview(null);
  };
  const onClick = (event: MouseEvent<HTMLButtonElement>, next: number) => {
    if (event.detail > 0 && event.timeStamp < suppressClickUntil.current) return;
    commit(next, event.timeStamp, { toggle: true });
  };
  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>, own: number) => {
    const moves: Record<string, number> = { ArrowRight: currentValue + step, ArrowUp: currentValue + step, ArrowLeft: currentValue - step, ArrowDown: currentValue - step, Home: step, End: max };
    let next: number | undefined = moves[event.key];
    let announce = false;
    if (event.key === " " || event.key === "Enter") next = own;
    if ((event.key === "Backspace" || event.key === "Delete") && allowClear) { next = 0; announce = true; }
    if (/^[1-9]$/.test(event.key) && Number(event.key) <= max) next = Number(event.key);
    if (next === undefined) return;
    event.preventDefault();
    const chosen = next === 0 ? 0 : Math.max(step, Math.min(max, next));
    commit(chosen, event.timeStamp, { announce });
    radios.current[Math.max(0, Math.round(chosen / step) - 1)]?.focus();
  };

  const tabStop = currentValue > 0 ? currentValue : step;
  const pulsing = pulse && pulse.value === currentValue && currentValue > 0 ? pulse : null;
  const radio = (next: number, half: boolean) => <button
    key={next}
    ref={node => { radios.current[Math.round(next / step) - 1] = node; }}
    type="button"
    role="radio"
    className={[styles.hit, half ? styles.half : ""].filter(Boolean).join(" ")}
    aria-checked={currentValue === next}
    aria-label={spokenFor(next)}
    tabIndex={interactive && next === tabStop ? 0 : -1}
    disabled={disabled}
    onClick={event => onClick(event, next)}
    onKeyDown={event => onKeyDown(event, next)}
  />;

  return <div className={[styles.field, styles[size], className].filter(Boolean).join(" ")} data-disabled={disabled || undefined}>
    <span id={labelId} className={styles.label}>{label}</span>
    <div className={styles.row}>
      <div
        ref={group}
        className={styles.stars}
        role={readOnly ? "img" : "radiogroup"}
        aria-labelledby={readOnly ? undefined : labelId}
        aria-label={readOnly ? `${label}: ${currentValue > 0 ? `${currentValue} out of ${max}` : emptyLabel}` : undefined}
        aria-describedby={description && !readOnly ? descriptionId : undefined}
        aria-disabled={disabled || undefined}
        data-rated={displayValue > 0 || undefined}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={event => endGesture(event)}
        onPointerCancel={event => endGesture(event, true)}
        onPointerLeave={onPointerLeave}
      >
        {Array.from({ length: max }, (_, index) => {
          const fill = Math.max(0, Math.min(1, displayValue - index));
          const order = sweep.to > sweep.from ? index - Math.floor(sweep.from) : Math.ceil(sweep.from) - 1 - index;
          const popping = pulsing && index < Math.ceil(pulsing.value) && (pulsing.wave || index === Math.ceil(pulsing.value) - 1);
          return <span key={`${generatedId}-${index}`} ref={node => { cells.current[index] = node; }} className={styles.cell}>
            <StarGlyph
              fill={fill}
              kept={Math.max(0, Math.min(1, currentValue - index))}
              delay={Math.max(0, order) * motionTokens.stagger.item}
              soft={displayValue > 0 && fill === 0}
              pressed={pressed === index}
              pop={popping ? { key: pulsing.key, delay: pulsing.wave ? index * motionTokens.stagger.item : 0 } : null}
              reduced={reduced}
            />
            {!readOnly && allowHalf && radio(index + .5, true)}
            {!readOnly && radio(index + 1, false)}
          </span>;
        })}
      </div>
      {showValue && <span className={styles.value} aria-hidden="true" data-empty={displayValue === 0 || undefined}>
        {[0, ...steps].map(next => <span key={next} className={styles.sizer}>{textFor(next)}</span>)}
        <RollingText text={textFor(displayValue)} direction={direction} reduced={reduced} />
      </span>}
    </div>
    {description && <span id={descriptionId} className={styles.description}><RollingText text={description} direction={direction} reduced={reduced} /></span>}
    {!readOnly && <span className={styles.srOnly} aria-live="polite">{announcement}</span>}
    {name && <input type="hidden" name={name} value={currentValue} />}
  </div>;
}
