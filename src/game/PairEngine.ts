/**
 * 组对规则。渲染层只读这里的状态。
 * 状态机里不允许出现 lane / leak / baseHp —— 钉在测试里。
 */
import { paperCutOf, type PaperCut } from '@/balance/chain';
import { consequenceOf } from '@/balance/consequences';
import { SLICE_ROUNDS, ROUND_SECONDS, MAX_MISSES, type PairDef, type RoundDef } from '@/balance/slice';

export type PairKind = 'rewrite' | 'reversed' | 'trap' | 'miss';

export type Phase = 'play' | 'react' | 'won' | 'lost' | 'continue';

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
  roundId: string;
  source: string;
  notice: string;
  mark: string;
  goal: string;
  hint: string;
  guide?: readonly [string, string];
  pool: string[];
  pairs: PairDef[];
  used: string[];
  selected: string | null;
  missesLeft: number;
  timeLeft: number;
  roundSeconds: number;
  combo: number;
  history: PairAttempt[];
  lastAttempt: PairAttempt | null;
  reactUntil: number;
  /** 本局对上过的词，离开这张纸时可能因此被换掉 */
  record: string[];
  /** 当前句为什么变成这样；没有案底则为 null */
  because: string | null;
  consequenceFired: number;
  continueUsed: boolean;
  /** 这张纸已经改了几刀（从 0 起） */
  cutIndex: number;
  /** 这张纸要改满几刀才换通知 */
  cutsNeed: number;
}

export interface TryPairResult {
  state: RunState;
  attempt: PairAttempt;
}

const REACT: Record<PairKind, number> = {
  rewrite: 2.3,
  reversed: 1.7,
  trap: 1.9,
  miss: 1.4,
};

const MAX_CONSEQUENCES = 2;
const CONTINUE_SECONDS = 18;
const CUT_BONUS_SECONDS = 16;

function clone<T>(v: T): T {
  return JSON.parse(JSON.stringify(v)) as T;
}

export function currentRound(state: RunState): RoundDef {
  return {
    id: state.roundId,
    source: state.source,
    notice: state.notice,
    mark: state.mark,
    goal: state.goal,
    hint: state.hint,
    guide: state.guide,
    pool: state.pool,
    pairs: state.pairs,
    seconds: state.roundSeconds,
    cuts: state.cutsNeed,
  };
}

export function roundSeconds(round: RoundDef): number {
  return round.seconds ?? ROUND_SECONDS;
}

function applyRound(state: RunState, index: number): void {
  const last = state.record[state.record.length - 1];
  const consequence = state.consequenceFired < MAX_CONSEQUENCES && last
    ? consequenceOf(last)
    : undefined;
  const base = SLICE_ROUNDS[index];

  state.roundIndex = index;
  state.used = [];
  state.selected = null;
  state.cutIndex = 0;

  if (consequence) {
    state.roundId = `because-${last}`;
    state.source = consequence.source;
    state.notice = consequence.notice;
    state.mark = consequence.mark;
    state.goal = consequence.goal;
    state.hint = consequence.hint;
    state.guide = undefined;
    state.pool = [...consequence.pool];
    state.pairs = consequence.pairs.map((p) => ({ ...p }));
    state.roundSeconds = ROUND_SECONDS;
    state.timeLeft = ROUND_SECONDS;
    state.cutsNeed = 1;
    state.because = last;
    state.consequenceFired += 1;
    return;
  }

  state.roundId = base.id;
  state.source = base.source;
  state.notice = base.notice;
  state.mark = base.mark;
  state.goal = base.goal;
  state.hint = base.hint;
  state.guide = base.guide;
  state.pool = [...base.pool];
  state.pairs = base.pairs.map((p) => ({ ...p }));
  state.roundSeconds = roundSeconds(base);
  state.timeLeft = state.roundSeconds;
  state.cutsNeed = base.cuts ?? 2;
  state.because = null;
}

function applyCut(state: RunState, cut: PaperCut): void {
  state.cutIndex += 1;
  state.mark = cut.mark;
  state.goal = cut.goal;
  state.hint = cut.hint;
  state.guide = undefined;
  state.pool = [...cut.pool];
  state.pairs = cut.pairs.map((p) => ({ ...p }));
  state.used = [];
  state.selected = null;
  state.timeLeft = Math.max(state.timeLeft, 12) + CUT_BONUS_SECONDS;
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
    roundId: '',
    source: '',
    notice: '',
    mark: '',
    goal: '',
    hint: '',
    pool: [],
    pairs: [],
    used: [],
    selected: null,
    missesLeft: MAX_MISSES,
    timeLeft: ROUND_SECONDS,
    roundSeconds: ROUND_SECONDS,
    combo: 0,
    history: [],
    lastAttempt: null,
    reactUntil: 0,
    record: [],
    because: null,
    consequenceFired: 0,
    continueUsed: false,
    cutIndex: 0,
    cutsNeed: 2,
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
    next.record.push(attempt.word);
    next.combo += 1;
    return next;
  }

  next.combo = 0;
  next.missesLeft -= 1;
  if (attempt.kind === 'trap' && attempt.to !== next.notice) {
    next.notice = attempt.to;
  }
  return next;
}

export function settleReact(state: RunState): RunState {
  if (state.phase !== 'react') return state;
  const next = clone(state);
  next.reactUntil = 0;
  next.lastAttempt = state.lastAttempt;

  if (state.lastAttempt?.kind === 'rewrite') {
    const word = state.lastAttempt.word;
    const nextCut = state.cutIndex + 1;
    const cut = nextCut < state.cutsNeed ? paperCutOf(word) : undefined;
    if (cut && next.notice.includes(cut.mark)) {
      applyCut(next, cut);
      next.phase = 'play';
      return next;
    }
    const following = state.roundIndex + 1;
    if (following >= SLICE_ROUNDS.length) {
      next.phase = 'won';
      return next;
    }
    applyRound(next, following);
    next.phase = 'play';
    return next;
  }

  if (state.phase === 'react' && state.missesLeft <= 0) {
    next.phase = state.continueUsed ? 'lost' : 'continue';
    return next;
  }

  next.phase = 'play';
  return next;
}

export function tick(state: RunState, dt: number): RunState {
  if (state.phase === 'won' || state.phase === 'lost' || state.phase === 'continue') return state;

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
      phase: state.continueUsed ? 'lost' : 'continue',
      combo: 0,
      lastAttempt: {
        a: '', b: '', kind: 'miss', word: '', line: '这句没改完',
        from: state.notice, to: state.notice,
      },
    };
  }
  return { ...state, timeLeft };
}

export function grantContinue(state: RunState): RunState {
  if (state.phase !== 'continue') return state;
  const next = clone(state);
  next.phase = 'play';
  next.continueUsed = true;
  next.missesLeft = Math.max(1, next.missesLeft);
  next.timeLeft = Math.max(next.timeLeft, CONTINUE_SECONDS);
  next.selected = null;
  return next;
}

export function acceptLoss(state: RunState): RunState {
  if (state.phase !== 'continue') return state;
  return { ...state, phase: 'lost' };
}

export function winningWords(state: RunState): string[] {
  return state.history.filter((h) => h.kind === 'rewrite').map((h) => h.word);
}

export function rewriteWords(state: RunState): string[] {
  return currentRound(state).pairs.filter((p) => p.kind === 'rewrite').map((p) => p.word);
}

export function trapWords(state: RunState): string[] {
  return currentRound(state).pairs.filter((p) => p.kind === 'trap').map((p) => p.word);
}

export function hintFor(state: RunState): string {
  const round = currentRound(state);
  const wins = rewriteWords(state).join('、');
  const traps = trapWords(state).join('、');
  if (state.phase === 'react' && state.lastAttempt?.line) return state.lastAttempt.line;
  if (state.selected) return `先「${state.selected}」，再点后面那个字。对上就能改红字`;
  if (state.lastAttempt?.kind === 'reversed') return state.lastAttempt.line;
  if (state.because) {
    return `因为你组了「${state.because}」。红字「${round.mark}」还是靶，${wins}都能改`;
  }
  if (round.guide && state.roundIndex === 0 && state.cutIndex === 0 && state.history.length === 0) {
    return `先点「${round.guide[0]}」，再点「${round.guide[1]}」。红字「${round.mark}」会变成取消`;
  }
  if (state.cutIndex > 0) {
    return `这张纸还没完，第 ${state.cutIndex + 1}/${state.cutsNeed} 刀。红字换成了「${round.mark}」，${wins}都能再改`;
  }
  return `红字「${round.mark}」是靶。${wins}都能改掉它，不是猜老板。${traps}会更糟`;
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
