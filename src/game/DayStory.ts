import type { PairAttempt, RunState } from './PairEngine';

export interface PaperStory {
  start: string;
  end: string;
  words: string[];
}

export interface DayStory {
  title: string;
  line: string;
  papers: PaperStory[];
  finished: PaperStory[];
  share: string;
}

function rewriteOnly(history: readonly PairAttempt[]): PairAttempt[] {
  return history.filter((h) => h.kind === 'rewrite');
}

export function groupPapers(history: readonly PairAttempt[]): PaperStory[] {
  const papers: PairAttempt[][] = [];
  for (const h of rewriteOnly(history)) {
    const last = papers[papers.length - 1];
    if (last && last[last.length - 1].to === h.from) {
      last.push(h);
    } else {
      papers.push([h]);
    }
  }
  return papers.map((lines) => ({
    start: lines[0].from,
    end: lines[lines.length - 1].to,
    words: lines.map((l) => l.word),
  }));
}

export function finishedPapers(state: Pick<RunState, 'history' | 'phase' | 'cutIndex'>): PaperStory[] {
  const papers = groupPapers(state.history);
  if (state.phase === 'won' || state.phase === 'lost') return papers;
  if (state.cutIndex === 0) return papers;
  return papers.slice(0, -1);
}

function hasWord(words: readonly string[], word: string): boolean {
  return words.includes(word);
}

export function dayTitle(words: readonly string[], papers: readonly PaperStory[], won: boolean): string {
  if (papers.length === 0) return won ? '这一天是你改的' : '一对都没对上';
  const slack = hasWord(words, '摸鱼') || hasWord(words, '放假') || hasWord(words, '鸽了') || hasWord(words, '请假');
  const done = hasWord(words, '定稿') || hasWord(words, '过了') || hasWord(words, '能成');
  const worse = hasWord(words, '加班') || hasWord(words, '通宵');
  if (!won) return `${papers[papers.length - 1].end}，改到这里`;
  if (slack && done) return '事成了，人先走';
  if (slack && worse) return '一边摸鱼一边加班';
  if (slack) return '通知在，人摸鱼';
  if (done) return '这版算数了';
  if (worse) return '越改越惨';
  return papers[0].end;
}

export function tellDay(state: Pick<RunState, 'history' | 'phase' | 'cutIndex'>): DayStory {
  const papers = groupPapers(state.history);
  const finished = finishedPapers(state);
  const words = rewriteOnly(state.history).map((h) => h.word);
  const won = state.phase === 'won';
  const title = dayTitle(words, papers, won);
  const board = (state.phase === 'won' || state.phase === 'lost' ? papers : finished).map((p) => p.end);
  const line = board.length > 0
    ? `今天写成了：${board.slice(0, 3).join(' · ')}`
    : '对上之后，这一天才会留下字';
  const share = [
    `对上了 · ${title}`,
    '',
    ...board,
    '',
    words.length ? `靠 ${words.join(' · ')}` : '一对都没对上',
  ].join('\n');
  return { title, line, papers, finished, share };
}
