import { describe, expect, it } from 'vitest';
import { MAX_MISSES, SLICE_ROUNDS } from '@/balance/slice';
import {
  clearSelect,
  createRun,
  forbiddenKeys,
  hintFor,
  selectChar,
  settleReact,
  splitMark,
  tick,
  tryPair,
  winningWords,
} from '@/game/PairEngine';

function firstRewrite(index = 0) {
  return SLICE_ROUNDS[index].pairs.find((p) => p.kind === 'rewrite')!;
}

function playRewrite(state = createRun()) {
  const pair = firstRewrite(state.roundIndex);
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

  it('组错次数用尽判负', () => {
    let state = createRun();
    for (let i = 0; i < MAX_MISSES; i++) {
      state = tryPair(state, '取', '鱼').state;
      if (state.phase === 'react') state = settleReact(state);
    }
    expect(state.phase).toBe('lost');
    expect(state.missesLeft).toBe(0);
  });

  it('改完一整天则通关，结算能点出词', () => {
    let state = createRun();
    const words: string[] = [];
    while (state.phase !== 'won') {
      const pair = firstRewrite(state.roundIndex);
      words.push(pair.word);
      state = playRewrite(state);
    }
    expect(state.phase).toBe('won');
    expect(winningWords(state)).toEqual(words);
    expect(words.length).toBe(SLICE_ROUNDS.length);
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

  it('超时判负，失败来自改不完，不是漏怪', () => {
    const state = tick(createRun(), 99);
    expect(state.phase).toBe('lost');
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
    }
  });
});
