/**
 * 组对规则。渲染层只读这里的状态。
 * 状态机里不允许出现 lane / leak / baseHp —— 钉在测试里。
 */
import { SLICE_ROUNDS, ROUND_SECONDS, MAX_MISSES, type PairDef, type RoundDef } from '@/balance/slice';

export type PairKind = 'rewrite' | 'reversed' | 'trap' | 'miss';

export type Phase = 'play' | 'react' | 'won' | 'lost';

export interface PairAttempt {
  a: string;
  b: string;
  kind: PairKind;
  word: string;
  line: string;
  from: string;
  to: string;
}

export interface RunState {
  phase: Phase;
  roundIndex: number;
  source: string;
  notice: string;
  mark: string;
  goal: string;
  pool: string[];
  used: string[];
  selected: string | null;
  missesLeft: number;
  timeLeft: number;
  combo: number;
  history: PairAttempt[];
  lastAttempt: PairAttempt | null;
  reactUntil: number;
}

export interface TryPairResult {
  state: RunState;
  attempt: PairAttempt;
}

const REACT: Record<PairKind, number> = {
  rewrite: 1.7,
  reversed: 1.05,
  trap: 1.2,
  miss: 0.75,
};

function clone<T>(v: T): T {
  return JSON.parse(JSON.stringify(v)) as T;
}

export function currentRound(state: RunState): RoundDef {
  return SLICE_ROUNDS[state.roundIndex];
}

export function roundSeconds(round: RoundDef): number {
  return round.seconds ?? ROUND_SECONDS;
}

function applyRound(state: RunState, index: number): void {
  const round = SLICE_ROUNDS[index];
  state.roundIndex = index;
  state.source = round.source;
  state.notice = round.notice;
  state.mark = round.mark;
  state.goal = round.goal;
  state.pool = [...round.pool];
  state.used = [];
  state.selected = null;
  state.timeLeft = roundSeconds(round);
}

function findPair(round: RoundDef, a: string, b: string): PairDef | undefined {
  return round.pairs.find((p) => p.a === a && p.b === b);
}

function findReversed(round: RoundDef, a: string, b: string): PairDef | undefined {
  return round.pairs.find((p) => p.a === b && p.b === a);
}

export function createRun(): RunState {
  const state: RunState = {
    phase: 'play',
    roundIndex: 0,
    source: '',
    notice: '',
    mark: '',
    goal: '',
    pool: [],
    used: [],
    selected: null,
    missesLeft: MAX_MISSES,
    timeLeft: ROUND_SECONDS,
    combo: 0,
    history: [],
    lastAttempt: null,
    reactUntil: 0,
  };
  applyRound(state, 0);
  return state;
}

export function selectChar(state: RunState, ch: string): RunState {
  if (state.phase !== 'play') return state;
  if (!state.pool.includes(ch) || state.used.includes(ch)) return state;
  if (state.selected === ch) {
    return { ...state, selected: null };
  }
  return { ...state, selected: ch };
}

export function clearSelect(state: RunState): RunState {
  if (state.phase !== 'play') return state;
  return { ...state, selected: null };
}

export function tryPair(state: RunState, a: string, b: string): TryPairResult {
  if (state.phase !== 'play') {
    return { state, attempt: dummyMiss(state, a, b) };
  }
  if (a === b) {
    return { state: { ...state, selected: a }, attempt: dummyMiss(state, a, b) };
  }

  const round = currentRound(state);
  const exact = findPair(round, a, b);
  const reversed = exact ? undefined : findReversed(round, a, b);

  let attempt: PairAttempt;
  if (exact?.kind === 'rewrite') {
    attempt = {
      a, b, kind: 'rewrite', word: exact.word, line: exact.line,
      from: state.notice, to: exact.nextText,
    };
  } else if (exact?.kind === 'trap') {
    attempt = {
      a, b, kind: 'trap', word: exact.word, line: exact.line,
      from: state.notice, to: exact.nextText ?? state.notice,
    };
  } else if (reversed) {
    attempt = {
      a, b, kind: 'reversed', word: reversed.word, line: `顺序反了，先「${reversed.a}」再「${reversed.b}」`,
      from: state.notice, to: state.notice,
    };
  } else {
    attempt = {
      a, b, kind: 'miss', word: a + b, line: '对不上，换两个字',
      from: state.notice, to: state.notice,
    };
  }

  return { state: applyAttempt(state, attempt), attempt };
}

function dummyMiss(state: RunState, a: string, b: string): PairAttempt {
  return {
    a, b, kind: 'miss', word: a + b, line: '',
    from: state.notice, to: state.notice,
  };
}

function applyAttempt(state: RunState, attempt: PairAttempt): RunState {
  const next = clone(state);
  next.selected = null;
  next.lastAttempt = attempt;
  next.phase = 'react';
  next.reactUntil = REACT[attempt.kind];

  if (attempt.kind === 'rewrite') {
    next.notice = attempt.to;
    next.mark = '';
    next.used.push(attempt.a, attempt.b);
    next.history.push(attempt);
    next.combo += 1;
    return next;
  }

  next.combo = 0;
  next.missesLeft -= 1;
  if (attempt.kind === 'trap' && attempt.to !== next.notice) {
    next.notice = attempt.to;
  }
  if (next.missesLeft <= 0) {
    next.phase = 'lost';
    next.reactUntil = 0;
  }
  return next;
}

export function settleReact(state: RunState): RunState {
  if (state.phase !== 'react') return state;
  const next = clone(state);
  next.reactUntil = 0;
  next.lastAttempt = state.lastAttempt;

  if (state.lastAttempt?.kind === 'rewrite') {
    const following = state.roundIndex + 1;
    if (following >= SLICE_ROUNDS.length) {
      next.phase = 'won';
      return next;
    }
    applyRound(next, following);
    next.phase = 'play';
    return next;
  }

  next.phase = 'play';
  return next;
}

export function tick(state: RunState, dt: number): RunState {
  if (state.phase === 'won' || state.phase === 'lost') return state;

  if (state.phase === 'react') {
    const left = state.reactUntil - dt;
    if (left <= 0) return settleReact(state);
    return { ...state, reactUntil: left };
  }

  const timeLeft = state.timeLeft - dt;
  if (timeLeft <= 0) {
    return {
      ...state,
      timeLeft: 0,
      phase: 'lost',
      combo: 0,
      lastAttempt: {
        a: '', b: '', kind: 'miss', word: '', line: '这句没改完',
        from: state.notice, to: state.notice,
      },
    };
  }
  return { ...state, timeLeft };
}

export function winningWords(state: RunState): string[] {
  return state.history.filter((h) => h.kind === 'rewrite').map((h) => h.word);
}

export function hintFor(state: RunState): string {
  const round = currentRound(state);
  if (state.phase === 'react' && state.lastAttempt?.line) return state.lastAttempt.line;
  if (state.selected) return `${state.selected} + ？ 再点后面那个字`;
  if (state.lastAttempt?.kind === 'reversed') return state.lastAttempt.line;
  if (round.guide && state.roundIndex === 0 && state.history.length === 0) {
    return `先点「${round.guide[0]}」，再点「${round.guide[1]}」，把${round.mark}改掉`;
  }
  return `${round.hint}。这句要${round.goal}`;
}

export function partnersOf(state: RunState, ch: string): string[] {
  const round = currentRound(state);
  return round.pairs.filter((p) => p.a === ch).map((p) => p.b);
}

export function splitMark(notice: string, mark: string): { pre: string; mark: string; post: string } | null {
  if (!mark) return null;
  const i = notice.indexOf(mark);
  if (i < 0) return null;
  return {
    pre: notice.slice(0, i),
    mark,
    post: notice.slice(i + mark.length),
  };
}

export function forbiddenKeys(state: RunState): string[] {
  return Object.keys(state).filter((k) => (
    k === 'lane' || k === 'leak' || k === 'baseHp' || k === 'path'
  ));
}
