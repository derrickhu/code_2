import type { PairDef, RoundDef } from './slice';

/** 上午对上的词 → 下一句被换成这张。一天最多触发 2 次。 */
export const CONSEQUENCES: Record<string, Omit<RoundDef, 'id' | 'guide' | 'seconds'>> = {
  取消: {
    source: '群通知',
    notice: '谁把通知取消的',
    mark: '取消',
    goal: '别认这笔账',
    hint: '因为你组了取消',
    pool: ['不', '是', '请', '假', '取', '消'],
    pairs: [
      { a: '不', b: '是', word: '不是', kind: 'rewrite', nextText: '不是我取消的', line: '这笔账摘出去了' },
      { a: '请', b: '假', word: '请假', kind: 'rewrite', nextText: '你请假没看见', line: '你当时不在' },
      { a: '取', b: '消', word: '取消', kind: 'trap', nextText: '就是你取消的', line: '自己认了' },
    ],
  },
  摸鱼: {
    source: '监控纪要',
    notice: '监控显示全员摸鱼',
    mark: '摸鱼',
    goal: '把摸鱼改掉',
    hint: '因为你组了摸鱼',
    pool: ['加', '班', '已', '交', '摸', '鱼'],
    pairs: [
      { a: '加', b: '班', word: '加班', kind: 'rewrite', nextText: '监控显示全员加班', line: '摸鱼变成加班了' },
      { a: '已', b: '交', word: '已交', kind: 'rewrite', nextText: '监控显示都已交', line: '记录被你改了' },
      { a: '摸', b: '鱼', word: '摸鱼', kind: 'trap', nextText: '监控坐实全员摸鱼', line: '你给自己盖章' },
    ],
  },
  请假: {
    source: '假条',
    notice: '你的假条被打回',
    mark: '打回',
    goal: '把打回改掉',
    hint: '因为你组了请假',
    pool: ['批', '了', '过', '了', '打', '回'],
    pairs: [
      { a: '批', b: '了', word: '批了', kind: 'rewrite', nextText: '你的假条批了', line: '假条算数了' },
      { a: '过', b: '了', word: '过了', kind: 'rewrite', nextText: '你的假条过了', line: '打回不作数' },
      { a: '打', b: '回', word: '打回', kind: 'trap', nextText: '假条再次打回', line: '越改越回' },
    ],
  },
  鸽了: {
    source: '对接群',
    notice: '对接群在等进度',
    mark: '进度',
    goal: '把进度抹掉',
    hint: '因为你组了鸽了',
    pool: ['已', '交', '放', '假', '进', '度'],
    pairs: [
      { a: '已', b: '交', word: '已交', kind: 'rewrite', nextText: '对接群说已交', line: '进度没了' },
      { a: '放', b: '假', word: '放假', kind: 'rewrite', nextText: '对接群今天放假', line: '没人等了' },
      { a: '进', b: '度', word: '进度', kind: 'trap', nextText: '对接群继续催进度', line: '催得更狠' },
    ],
  },
};

export function consequenceOf(word: string): Omit<RoundDef, 'id' | 'guide' | 'seconds'> | undefined {
  return CONSEQUENCES[word];
}

export function allConsequencePairs(): PairDef[] {
  return Object.values(CONSEQUENCES).flatMap((r) => r.pairs);
}
