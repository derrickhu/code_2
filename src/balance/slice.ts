export type PairKindDef = 'rewrite' | 'trap';

export interface PairDef {
  a: string;
  b: string;
  word: string;
  kind: PairKindDef;
  nextText: string;
  line: string;
}

export interface RoundDef {
  id: string;
  source: string;
  notice: string;
  mark: string;
  goal: string;
  hint: string;
  guide?: readonly [string, string];
  pool: string[];
  pairs: PairDef[];
  seconds?: number;
  /** 同一张通知要改几刀。默认 2。 */
  cuts?: number;
}

export const ROUND_SECONDS = 40;
export const MAX_MISSES = 6;

export const SLICE_ROUNDS: readonly RoundDef[] = [
  {
    id: 'overtime',
    source: '群通知',
    notice: '今晚全体加班',
    mark: '加班',
    goal: '改掉红字加班',
    hint: '红字是靶，对上就能改，不是猜老板',
    guide: ['取', '消'],
    seconds: 48,
    cuts: 3,
    pool: ['取', '消', '摸', '鱼', '加', '班'],
    pairs: [
      { a: '取', b: '消', word: '取消', kind: 'rewrite', nextText: '今晚全体取消', line: '通知被你改了' },
      { a: '摸', b: '鱼', word: '摸鱼', kind: 'rewrite', nextText: '今晚全体摸鱼', line: '全员立场变了' },
      { a: '加', b: '班', word: '加班', kind: 'trap', nextText: '今晚必须加班', line: '越改越加班' },
    ],
  },
  {
    id: 'evening-meeting',
    source: '周会纪要',
    notice: '周会改到晚上',
    mark: '晚上',
    goal: '别晚上开',
    hint: '对上就能改开会时间',
    cuts: 2,
    pool: ['提', '前', '取', '消', '晚', '上'],
    pairs: [
      { a: '提', b: '前', word: '提前', kind: 'rewrite', nextText: '周会提前开', line: '晚上不作数了' },
      { a: '取', b: '消', word: '取消', kind: 'rewrite', nextText: '周会取消', line: '这会没了' },
      { a: '晚', b: '上', word: '晚上', kind: 'trap', nextText: '周会必须晚上', line: '你给它盖了章' },
    ],
  },
  {
    id: 'killed',
    source: '邮件',
    notice: '方案被毙了',
    mark: '毙了',
    goal: '把毙了改掉',
    hint: '对上就能翻案',
    cuts: 2,
    pool: ['能', '成', '重', '来', '毙', '了'],
    pairs: [
      { a: '能', b: '成', word: '能成', kind: 'rewrite', nextText: '方案能成了', line: '毙掉的是上一句' },
      { a: '重', b: '来', word: '重来', kind: 'rewrite', nextText: '方案重来', line: '推倒重写也是改' },
      { a: '毙', b: '了', word: '毙了', kind: 'trap', nextText: '方案彻底毙了', line: '别帮它盖章' },
    ],
  },
  {
    id: 'client',
    source: '客户群',
    notice: '客户说再改一版',
    mark: '再改',
    goal: '别再改了',
    hint: '对上就能收住',
    cuts: 2,
    pool: ['过', '了', '定', '稿', '再', '改'],
    pairs: [
      { a: '过', b: '了', word: '过了', kind: 'rewrite', nextText: '客户说这版过了', line: '这版算数了' },
      { a: '定', b: '稿', word: '定稿', kind: 'rewrite', nextText: '客户说定稿', line: '改到此为止' },
      { a: '再', b: '改', word: '再改', kind: 'trap', nextText: '客户说必须再改', line: '你把它坐实了' },
    ],
  },
  {
    id: 'lunch',
    source: '行政通知',
    notice: '午饭缩短十分钟',
    mark: '缩短',
    goal: '把缩短改掉',
    hint: '对上就能抢回午饭',
    cuts: 2,
    pool: ['加', '长', '取', '消', '缩', '短'],
    pairs: [
      { a: '加', b: '长', word: '加长', kind: 'rewrite', nextText: '午饭加长十分钟', line: '这十分钟是你的' },
      { a: '取', b: '消', word: '取消', kind: 'rewrite', nextText: '午饭缩短取消', line: '行政被你改了' },
      { a: '缩', b: '短', word: '缩短', kind: 'trap', nextText: '午饭再缩短十分钟', line: '越改越短' },
    ],
  },
  {
    id: 'rollcall',
    source: '工作群',
    notice: '谁还没交点名',
    mark: '点名',
    goal: '把自己摘出去',
    hint: '对上就能脱身',
    cuts: 2,
    pool: ['已', '交', '请', '假', '点', '名'],
    pairs: [
      { a: '已', b: '交', word: '已交', kind: 'rewrite', nextText: '你已交不用点名', line: '名单上没你了' },
      { a: '请', b: '假', word: '请假', kind: 'rewrite', nextText: '你请假不用点名', line: '你把自己划出去了' },
      { a: '点', b: '名', word: '点名', kind: 'trap', nextText: '你还没交被点名', line: '枪口对准自己' },
    ],
  },
  {
    id: 'tomorrow',
    source: '排班表',
    notice: '明天继续',
    mark: '继续',
    goal: '明天不作数',
    hint: '对上就能改明天',
    cuts: 2,
    pool: ['放', '假', '鸽', '了', '加', '班'],
    pairs: [
      { a: '放', b: '假', word: '放假', kind: 'rewrite', nextText: '明天放假', line: '明天不作数了' },
      { a: '鸽', b: '了', word: '鸽了', kind: 'rewrite', nextText: '明天鸽了', line: '对接人是你' },
      { a: '加', b: '班', word: '加班', kind: 'trap', nextText: '明天继续加班', line: '又给自己加了一天' },
    ],
  },
  {
    id: 'allnighter',
    source: '全员公告',
    notice: '全公司通宵',
    mark: '通宵',
    goal: '改掉通宵',
    hint: '最后一句，对上就收工',
    seconds: 36,
    cuts: 2,
    pool: ['取', '消', '放', '假', '通', '宵'],
    pairs: [
      { a: '取', b: '消', word: '取消', kind: 'rewrite', nextText: '全公司取消通宵', line: '公告作废' },
      { a: '放', b: '假', word: '放假', kind: 'rewrite', nextText: '全公司放假', line: '通宵没了' },
      { a: '通', b: '宵', word: '通宵', kind: 'trap', nextText: '全公司必须通宵', line: '你给通宵盖了章' },
    ],
  },
];
