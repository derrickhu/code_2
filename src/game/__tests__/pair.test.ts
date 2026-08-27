import { describe, expect, it } from 'vitest';
import { PAPER_CHAIN } from '@/balance/chain';
import { CONSEQUENCES } from '@/balance/consequences';
import { MAX_MISSES, SLICE_ROUNDS } from '@/balance/slice';
import {
  acceptLoss,
  clearSelect,
  createRun,
  currentRound,
  forbiddenKeys,
  grantContinue,
  hintFor,
  selectChar,
  settleReact,
  splitMark,
  tick,
  tryPair,
  winningWords,
} from '@/game/PairEngine';

function firstRewrite(state = createRun()) {
  return currentRound(state).pairs.find((p) => p.kind === 'rewrite')!;
}

function playRewrite(state = createRun()) {
  const pair = firstRewrite(state);
  return settleReact(tryPair(state, pair.a, pair.b).state);
}

describe('PairEngine', () => {
  it('对上改写那句话，不改数值', () => {
    const { state, attempt } = tryPair(createRun(), '取', '消');
    expect(attempt.kind).toBe('rewrite');
    expect(attempt.word).toBe('取消');
    expect(state.notice).toBe('今晚全体取消');
    expect(state.combo).toBe(1);
    expect(state.history).toHaveLength(1);
    expect(state.record).toEqual(['取消']);
  });

  it('同一对字反过来是语序惩罚，句子不变', () => {
    const { state, attempt } = tryPair(createRun(), '消', '取');
    expect(attempt.kind).toBe('reversed');
    expect(attempt.line).toContain('顺序反了');
    expect(attempt.line).toContain('取');
    expect(state.notice).toBe('今晚全体加班');
    expect(state.missesLeft).toBe(MAX_MISSES - 1);
    expect(state.combo).toBe(0);
    expect(state.history).toHaveLength(0);
  });

  it('陷阱词让局面更糟并扣机会', () => {
    const { state, attempt } = tryPair(createRun(), '加', '班');
    expect(attempt.kind).toBe('trap');
    expect(state.notice).toBe('今晚必须加班');
    expect(state.missesLeft).toBe(MAX_MISSES - 1);
  });

  it('对不上扣机会，句子不动', () => {
    const { state, attempt } = tryPair(createRun(), '取', '鱼');
    expect(attempt.kind).toBe('miss');
    expect(state.notice).toBe('今晚全体加班');
    expect(state.missesLeft).toBe(MAX_MISSES - 1);
  });

  it('组错次数用尽先给续命，不直接判负', () => {
    let state = createRun();
    for (let i = 0; i < MAX_MISSES; i++) {
      state = tryPair(state, '取', '鱼').state;
      if (state.phase === 'react') state = settleReact(state);
    }
    expect(state.phase).toBe('continue');
    expect(state.missesLeft).toBe(0);
  });

  it('续一次后还能打，再用尽才判负', () => {
    let state = createRun();
    for (let i = 0; i < MAX_MISSES; i++) {
      state = settleReact(tryPair(state, '取', '鱼').state);
    }
    state = grantContinue(state);
    expect(state.phase).toBe('play');
    expect(state.missesLeft).toBe(1);
    expect(state.continueUsed).toBe(true);
    state = settleReact(tryPair(state, '取', '鱼').state);
    expect(state.phase).toBe('lost');
  });

  it('不续则认输', () => {
    let state = createRun();
    for (let i = 0; i < MAX_MISSES; i++) {
      state = settleReact(tryPair(state, '取', '鱼').state);
    }
    state = acceptLoss(state);
    expect(state.phase).toBe('lost');
  });

  it('改完一整天则通关，结算能点出词', () => {
    let state = createRun();
    const words: string[] = [];
    while (state.phase !== 'won') {
      const pair = firstRewrite(state);
      words.push(pair.word);
      state = playRewrite(state);
    }
    expect(state.phase).toBe('won');
    expect(winningWords(state)).toEqual(words);
    expect(words.length).toBeGreaterThan(SLICE_ROUNDS.length);
  });

  it('对上后还在这张纸上，红字换成刚对上的词', () => {
    const state = playRewrite(createRun());
    expect(state.roundIndex).toBe(0);
    expect(state.because).toBeNull();
    expect(state.notice).toBe('今晚全体取消');
    expect(state.mark).toBe('取消');
    expect(state.cutIndex).toBe(1);
    expect(state.cutsNeed).toBe(3);
    expect(hintFor(state)).toContain('还没完');
  });

  it('改满这张纸才换通知，并写明因为', () => {
    let state = createRun();
    state = playRewrite(state);
    state = playRewrite(state);
    expect(state.roundIndex).toBe(0);
    expect(state.because).toBeNull();
    state = playRewrite(state);
    expect(state.because).toBe('摸鱼');
    expect(state.notice).toBe(CONSEQUENCES['摸鱼'].notice);
    expect(state.mark).toBe(CONSEQUENCES['摸鱼'].mark);
    expect(hintFor(state)).toContain('因为你组了「摸鱼」');
  });

  it('一天最多两句被案底改写', () => {
    let state = createRun();
    state = settleReact(tryPair(state, '摸', '鱼').state);
    state = settleReact(tryPair(state, '已', '交').state);
    state = settleReact(tryPair(state, '取', '消').state);
    expect(state.because).toBe('取消');
    expect(state.notice).toBe(CONSEQUENCES['取消'].notice);
    const second = currentRound(state).pairs.find((p) => p.word === '请假')!;
    state = settleReact(tryPair(state, second.a, second.b).state);
    expect(state.because).toBe('请假');
    expect(state.consequenceFired).toBe(2);
    state = playRewrite(state);
    expect(state.because).toBeNull();
    expect(state.consequenceFired).toBe(2);
  });

  it('开局提示直接告诉人改哪两个字', () => {
    const text = hintFor(createRun());
    expect(text).toContain('取');
    expect(text).toContain('消');
    expect(text).toContain('加班');
  });

  it('点同一个字是取消，不是组对', () => {
    let state = selectChar(createRun(), '取');
    expect(state.selected).toBe('取');
    state = selectChar(state, '取');
    expect(state.selected).toBeNull();
  });

  it('clearSelect 只清选择', () => {
    const state = clearSelect(selectChar(createRun(), '摸'));
    expect(state.selected).toBeNull();
    expect(state.notice).toBe('今晚全体加班');
  });

  it('超时先进续命，失败来自改不完，不是漏怪', () => {
    const state = tick(createRun(), 99);
    expect(state.phase).toBe('continue');
    expect(state.lastAttempt?.line).toBe('这句没改完');
  });

  it('通知能拆出要改的词', () => {
    const parts = splitMark('今晚全体加班', '加班');
    expect(parts).toEqual({ pre: '今晚全体', mark: '加班', post: '' });
  });

  it('状态机没有塔防字段', () => {
    expect(forbiddenKeys(createRun())).toEqual([]);
    const raw = JSON.stringify(createRun());
    expect(raw).not.toMatch(/lane|leak|baseHp|path/);
  });

  it('每句至少两条改写、一条陷阱、一条可反序', () => {
    expect(SLICE_ROUNDS.length).toBeGreaterThanOrEqual(8);
    for (const round of SLICE_ROUNDS) {
      const rewrites = round.pairs.filter((p) => p.kind === 'rewrite');
      const traps = round.pairs.filter((p) => p.kind === 'trap');
      expect(rewrites.length).toBeGreaterThanOrEqual(2);
      expect(traps.length).toBeGreaterThanOrEqual(1);
      const hasReverse = rewrites.some((p) => (
        !round.pairs.some((q) => q.a === p.b && q.b === p.a)
      ));
      expect(hasReverse).toBe(true);
      expect(round.pool.length).toBeLessThanOrEqual(12);
      expect(round.notice.includes(round.mark)).toBe(true);
      expect(new Set(round.pool).size).toBe(round.pool.length);
    }
  });

  it('连改词链能接上，字池不重复', () => {
    for (const [word, cut] of Object.entries(PAPER_CHAIN)) {
      expect(cut.mark).toBe(word);
      expect(new Set(cut.pool).size).toBe(cut.pool.length);
      const rewrites = cut.pairs.filter((p) => p.kind === 'rewrite');
      expect(rewrites.length).toBeGreaterThanOrEqual(2);
      for (const pair of rewrites) {
        expect(PAPER_CHAIN[pair.word] || pair.word).toBeTruthy();
        expect(pair.nextText.includes(pair.word) || pair.nextText.includes(PAPER_CHAIN[pair.word]?.mark ?? pair.word)).toBe(true);
      }
    }
    for (const round of SLICE_ROUNDS) {
      if ((round.cuts ?? 2) < 2) continue;
      for (const pair of round.pairs.filter((p) => p.kind === 'rewrite')) {
        expect(PAPER_CHAIN[pair.word]).toBeTruthy();
        expect(pair.nextText.includes(PAPER_CHAIN[pair.word]!.mark)).toBe(true);
      }
    }
  });
});
