import type { PairDef } from './slice';

/** 同一张通知上的下一刀。改完不换纸，红字换成你刚对上的词。 */
export interface PaperCut {
  mark: string;
  goal: string;
  hint: string;
  pool: string[];
  pairs: PairDef[];
}

export const PAPER_CHAIN: Record<string, PaperCut> = {
  取消: {
    mark: '取消',
    goal: '再改一刀',
    hint: '这句还没完，还能再改',
    pool: ['放', '假', '过', '了', '取', '消'],
    pairs: [
      { a: '放', b: '假', word: '放假', kind: 'rewrite', nextText: '今晚放假', line: '取消之后是放假' },
      { a: '过', b: '了', word: '过了', kind: 'rewrite', nextText: '这事过了', line: '取消之后算过了' },
      { a: '取', b: '消', word: '取消', kind: 'trap', nextText: '今晚取消不算', line: '取消被你盖死了' },
    ],
  },
  摸鱼: {
    mark: '摸鱼',
    goal: '再改一刀',
    hint: '摸鱼还能再改',
    pool: ['放', '假', '已', '交', '摸', '鱼'],
    pairs: [
      { a: '放', b: '假', word: '放假', kind: 'rewrite', nextText: '今晚放假', line: '摸鱼改成放假' },
      { a: '已', b: '交', word: '已交', kind: 'rewrite', nextText: '今晚都已交', line: '摸鱼不作数了' },
      { a: '摸', b: '鱼', word: '摸鱼', kind: 'trap', nextText: '今晚必须摸鱼', line: '你给摸鱼盖章' },
    ],
  },
  提前: {
    mark: '提前',
    goal: '再改一刀',
    hint: '时间还能再改',
    pool: ['取', '消', '过', '了', '提', '前'],
    pairs: [
      { a: '取', b: '消', word: '取消', kind: 'rewrite', nextText: '周会取消', line: '提前的会没了' },
      { a: '过', b: '了', word: '过了', kind: 'rewrite', nextText: '周会这版过了', line: '会开完了' },
      { a: '提', b: '前', word: '提前', kind: 'trap', nextText: '周会必须提前', line: '你把它坐实了' },
    ],
  },
  能成: {
    mark: '能成',
    goal: '再改一刀',
    hint: '方案还能再改',
    pool: ['定', '稿', '重', '来', '能', '成'],
    pairs: [
      { a: '定', b: '稿', word: '定稿', kind: 'rewrite', nextText: '方案定稿了', line: '能成变成定稿' },
      { a: '重', b: '来', word: '重来', kind: 'rewrite', nextText: '方案重来', line: '推倒再写' },
      { a: '能', b: '成', word: '能成', kind: 'trap', nextText: '谁说能成的', line: '能成被追问' },
    ],
  },
  重来: {
    mark: '重来',
    goal: '再改一刀',
    hint: '重来也能再改',
    pool: ['能', '成', '定', '稿', '重', '来'],
    pairs: [
      { a: '能', b: '成', word: '能成', kind: 'rewrite', nextText: '方案能成了', line: '重来翻过来了' },
      { a: '定', b: '稿', word: '定稿', kind: 'rewrite', nextText: '方案定稿了', line: '重来收到了' },
      { a: '重', b: '来', word: '重来', kind: 'trap', nextText: '方案再重来', line: '越改越重来' },
    ],
  },
  过了: {
    mark: '过了',
    goal: '再改一刀',
    hint: '过了还能再改',
    pool: ['定', '稿', '放', '假', '过', '了'],
    pairs: [
      { a: '定', b: '稿', word: '定稿', kind: 'rewrite', nextText: '客户说定稿', line: '过了就是定稿' },
      { a: '放', b: '假', word: '放假', kind: 'rewrite', nextText: '客户说放假', line: '这版不用改了' },
      { a: '过', b: '了', word: '过了', kind: 'trap', nextText: '客户说还没过', line: '过了被打回' },
    ],
  },
  定稿: {
    mark: '定稿',
    goal: '再改一刀',
    hint: '定稿还能再改',
    pool: ['过', '了', '放', '假', '定', '稿'],
    pairs: [
      { a: '过', b: '了', word: '过了', kind: 'rewrite', nextText: '客户说这版过了', line: '定稿算数了' },
      { a: '放', b: '假', word: '放假', kind: 'rewrite', nextText: '客户说放假', line: '定稿之后放假' },
      { a: '定', b: '稿', word: '定稿', kind: 'trap', nextText: '客户说再定一稿', line: '定稿没完' },
    ],
  },
  加长: {
    mark: '加长',
    goal: '再改一刀',
    hint: '午饭还能再改',
    pool: ['取', '消', '放', '假', '加', '长'],
    pairs: [
      { a: '取', b: '消', word: '取消', kind: 'rewrite', nextText: '午饭缩短取消', line: '加长也不用了' },
      { a: '放', b: '假', word: '放假', kind: 'rewrite', nextText: '午饭后放假', line: '午饭后面是假' },
      { a: '加', b: '长', word: '加长', kind: 'trap', nextText: '午饭再加长', line: '越改越长也怪' },
    ],
  },
  已交: {
    mark: '已交',
    goal: '再改一刀',
    hint: '已交还能再改',
    pool: ['请', '假', '取', '消', '已', '交'],
    pairs: [
      { a: '请', b: '假', word: '请假', kind: 'rewrite', nextText: '你请假不用点名', line: '已交的人请假了' },
      { a: '取', b: '消', word: '取消', kind: 'rewrite', nextText: '点名取消', line: '不用交了' },
      { a: '已', b: '交', word: '已交', kind: 'trap', nextText: '你已交被抽查', line: '已交被盯上' },
    ],
  },
  请假: {
    mark: '请假',
    goal: '再改一刀',
    hint: '假条还能再改',
    pool: ['批', '了', '提', '前', '请', '假'],
    pairs: [
      { a: '批', b: '了', word: '批了', kind: 'rewrite', nextText: '你的假条批了', line: '请假算数了' },
      { a: '提', b: '前', word: '提前', kind: 'rewrite', nextText: '假从提前开始', line: '假条提前生效' },
      { a: '请', b: '假', word: '请假', kind: 'trap', nextText: '假条被打回', line: '越请越回' },
    ],
  },
  放假: {
    mark: '放假',
    goal: '再改一刀',
    hint: '放假还能再改',
    pool: ['摸', '鱼', '鸽', '了', '放', '假'],
    pairs: [
      { a: '摸', b: '鱼', word: '摸鱼', kind: 'rewrite', nextText: '今晚摸鱼', line: '放假改成摸鱼' },
      { a: '鸽', b: '了', word: '鸽了', kind: 'rewrite', nextText: '今晚鸽了', line: '放假改成鸽了' },
      { a: '放', b: '假', word: '放假', kind: 'trap', nextText: '放假取消', line: '假没了' },
    ],
  },
  鸽了: {
    mark: '鸽了',
    goal: '再改一刀',
    hint: '鸽了还能再改',
    pool: ['已', '交', '放', '假', '鸽', '了'],
    pairs: [
      { a: '已', b: '交', word: '已交', kind: 'rewrite', nextText: '对接说已交', line: '鸽了被改掉' },
      { a: '放', b: '假', word: '放假', kind: 'rewrite', nextText: '对接今天放假', line: '没人等了' },
      { a: '鸽', b: '了', word: '鸽了', kind: 'trap', nextText: '对接说你鸽了', line: '自己认了' },
    ],
  },
};

export function paperCutOf(word: string): PaperCut | undefined {
  return PAPER_CHAIN[word];
}
