import { describe, expect, it } from 'vitest';
import { CONSEQUENCES } from '@/balance/consequences';
import { dayTitle, groupPapers, tellDay } from '@/game/DayStory';
import { createRun, currentRound, settleReact, tryPair } from '@/game/PairEngine';

function play(state = createRun(), a?: string, b?: string) {
  const pair = a && b
    ? { a, b }
    : currentRound(state).pairs.find((p) => p.kind === 'rewrite')!;
  return settleReact(tryPair(state, pair.a, pair.b).state);
}

describe('DayStory', () => {
  it('同一张纸的连改收成一句终稿，不是一条流水账', () => {
    let state = createRun();
    state = play(state, '取', '消');
    state = play(state, '放', '假');
    const papers = groupPapers(state.history);
    expect(papers).toHaveLength(1);
    expect(papers[0].start).toBe('今晚全体加班');
    expect(papers[0].end).toBe('今晚放假');
    expect(papers[0].words).toEqual(['取消', '放假']);
  });

  it('没离开这张纸时，今天还没有定稿', () => {
    const state = play(createRun(), '取', '消');
    const story = tellDay(state);
    expect(story.finished).toHaveLength(0);
    expect(story.line).toContain('才会留下字');
  });

  it('改满一张纸，今天留下终稿', () => {
    let state = createRun();
    state = play(state);
    state = play(state);
    state = play(state);
    const story = tellDay(state);
    expect(story.finished[0].end).toBe('今晚摸鱼');
    expect(story.line).toContain('今晚摸鱼');
    expect(state.notice).toBe(CONSEQUENCES['摸鱼'].notice);
  });

  it('通关标题按词定性，分享是今天的通知不是箭头', () => {
    let state = createRun();
    while (state.phase !== 'won') state = play(state);
    const story = tellDay(state);
    expect(story.title).toBe(dayTitle(
      state.history.filter((h) => h.kind === 'rewrite').map((h) => h.word),
      story.papers,
      true,
    ));
    expect(story.share).toContain('对上了');
    expect(story.share).not.toContain('→');
    expect(story.papers.every((p) => story.share.includes(p.end))).toBe(true);
    expect(story.title.length).toBeGreaterThan(0);
  });
});
