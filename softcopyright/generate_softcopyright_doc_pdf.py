#!/usr/bin/env python3
"""
软件著作权登记 - 文档鉴别材料（设计说明书）PDF 生成工具
项目: 宋江别慌张小游戏软件

截图未放入 softcopyright/pics 时，PDF 中自动生成「待补游戏截图」占位框。
补图后重新运行本脚本即可替换占位。
"""

import warnings
from pathlib import Path

from fpdf import FPDF
from fpdf.enums import WrapMode
from PIL import Image

warnings.filterwarnings("ignore", category=DeprecationWarning)


SCRIPT_DIR = Path(__file__).resolve().parent
OUTPUT = SCRIPT_DIR / '软著文档-宋江别慌张-V1.0.0.pdf'

SOFTWARE_FULL_NAME = '深圳幸运呱科技有限公司宋江别慌张小游戏软件'
SOFTWARE_VERSION = 'V1.0.0'
APPLICANT_NAME = '深圳幸运呱科技有限公司'

SONGTI_PATH = '/System/Library/Fonts/Supplemental/Songti.ttc'

BODY_FONT_SIZE = 10.5
H1_FONT_SIZE = 16
H2_FONT_SIZE = 14
H3_FONT_SIZE = 12
CODE_FONT_SIZE = 9
HEADER_FONT_SIZE = 10
FOOTER_FONT_SIZE = 9

LINE_HEIGHT = 6.5
CODE_LINE_HEIGHT = 5.0
H1_LINE_HEIGHT = 10
H2_LINE_HEIGHT = 8.5
H3_LINE_HEIGHT = 7.5

LEFT_MARGIN = 25
RIGHT_MARGIN = 20
TOP_MARGIN = 15
BOTTOM_MARGIN = 15

PAGE_W = 210
PAGE_H = 297
CONTENT_W = PAGE_W - LEFT_MARGIN - RIGHT_MARGIN
CONTENT_TOP = TOP_MARGIN + 10

HEADER_TEXT = f'{SOFTWARE_FULL_NAME} {SOFTWARE_VERSION} 设计说明书'
PICS_DIR = SCRIPT_DIR / 'pics'


def _pic(*names):
    for name in names:
        path = PICS_DIR / name
        if path.exists():
            return path
    return PICS_DIR / names[0]


SCREENSHOTS = {
    'home': [(_pic('biehuang_01_home.jpg', 'biehuang_01_home.png'),
              '图1  出征主界面 - 城门首领、上阵好汉、开始挑战与底部页签')],
    'levels': [(_pic('biehuang_02_levels.jpg', 'biehuang_02_levels.png'),
                '图2  关卡列表 - 章节、关名与通关进度')],
    'heroes': [(_pic('biehuang_03_heroes.jpg', 'biehuang_03_heroes.png'),
                '图3  好汉界面 - 上阵四人、等级与换人')],
    'battle': [(_pic('biehuang_04_battle.jpg', 'biehuang_04_battle.png'),
                '图4  战斗界面 - 官军、寨墙、召唤、强化与寨主宋江')],
    'cards': [(_pic('biehuang_05_cards.jpg', 'biehuang_05_cards.png'),
               '图5  强化三选一 - 词条卡与选择')],
    'result': [(_pic('biehuang_06_result.jpg', 'biehuang_06_result.png'),
                '图6  结算界面 - 胜负、奖励与输出')],
    'recruit': [(_pic('biehuang_07_recruit.jpg', 'biehuang_07_recruit.png'),
                 '图7  擂鼓招募 - 单抽、十连、免费次数与保底')],
    'market': [(_pic('biehuang_08_market.jpg', 'biehuang_08_market.png'),
                '图8  梁山集市 - 元宝购买与广告领取')],
    'forge': [(_pic('biehuang_09_forge.jpg', 'biehuang_09_forge.png'),
               '图9  寨主界面 - 宋江与六件装备')],
    'talent': [(_pic('biehuang_10_talent.jpg', 'biehuang_10_talent.png'),
                '图10  养成弹窗 - 天赋、天书或杏黄旗')],
    'fort': [(_pic('biehuang_11_fort.jpg', 'biehuang_11_fort.png'),
              '图11  水泊梁山 - 山寨建筑与功能入口')],
    'task': [(_pic('biehuang_12_task.jpg', 'biehuang_12_task.png'),
              '图12  每日任务或签到')],
    'codex': [(_pic('biehuang_13_codex.jpg', 'biehuang_13_codex.png'),
               '图13  图鉴 - 好汉收集')],
    'daily': [(_pic('biehuang_14_daily.jpg', 'biehuang_14_daily.png'),
               '图14  每日挑战或生辰纲')],
    'pause': [(_pic('biehuang_15_pause.jpg', 'biehuang_15_pause.png'),
               '图15  暂停或输出统计')],
}


class DocPDF(FPDF):
    def __init__(self):
        super().__init__(orientation='P', unit='mm', format='A4')
        self.set_left_margin(LEFT_MARGIN)
        self.set_right_margin(RIGHT_MARGIN)
        self.set_top_margin(CONTENT_TOP)
        self.set_auto_page_break(auto=True, margin=BOTTOM_MARGIN + 10)
        self.missing_images = []

    def header(self):
        self.set_font('Songti', '', HEADER_FONT_SIZE)
        self.set_text_color(0, 0, 0)
        self.set_xy(LEFT_MARGIN, TOP_MARGIN)
        self.cell(0, 6, HEADER_TEXT, new_x='LEFT', new_y='TOP')
        page_str = str(self.page_no())
        tw = self.get_string_width(page_str)
        self.set_xy(PAGE_W - RIGHT_MARGIN - tw, TOP_MARGIN)
        self.cell(tw, 6, page_str, new_x='LEFT', new_y='TOP')
        line_y = TOP_MARGIN + 7
        self.set_draw_color(0, 0, 0)
        self.set_line_width(0.4)
        self.line(LEFT_MARGIN, line_y, PAGE_W - RIGHT_MARGIN, line_y)
        self.set_y(CONTENT_TOP)

    def footer(self):
        footer_y = PAGE_H - BOTTOM_MARGIN
        self.set_xy(LEFT_MARGIN, footer_y)
        self.set_font('Songti', '', FOOTER_FONT_SIZE)
        self.set_text_color(0, 0, 0)
        self.cell(CONTENT_W, 5, APPLICANT_NAME, align='C')

    def check_page_break(self, h):
        if self.get_y() + h > PAGE_H - BOTTOM_MARGIN - 10:
            self.add_page()

    def write_h1(self, text):
        self.check_page_break(H1_LINE_HEIGHT + 5)
        self.ln(4)
        self.set_font('Songti', '', H1_FONT_SIZE)
        self.set_text_color(0, 0, 0)
        self.set_x(LEFT_MARGIN)
        self.cell(CONTENT_W, H1_LINE_HEIGHT, _safe_text(text), new_x='LMARGIN', new_y='NEXT')
        self.ln(2)

    def write_h2(self, text):
        self.check_page_break(H2_LINE_HEIGHT + 4)
        self.ln(3)
        self.set_font('Songti', '', H2_FONT_SIZE)
        self.set_text_color(0, 0, 0)
        self.set_x(LEFT_MARGIN)
        self.cell(CONTENT_W, H2_LINE_HEIGHT, _safe_text(text), new_x='LMARGIN', new_y='NEXT')
        self.ln(1.5)

    def write_h3(self, text):
        self.check_page_break(H3_LINE_HEIGHT + 3)
        self.ln(2)
        self.set_font('Songti', '', H3_FONT_SIZE)
        self.set_text_color(0, 0, 0)
        self.set_x(LEFT_MARGIN)
        self.cell(CONTENT_W, H3_LINE_HEIGHT, _safe_text(text), new_x='LMARGIN', new_y='NEXT')
        self.ln(1)

    def write_body(self, text, indent=0):
        self.set_font('Songti', '', BODY_FONT_SIZE)
        self.set_text_color(30, 30, 30)
        self.set_x(LEFT_MARGIN + indent)
        self.multi_cell(CONTENT_W - indent, LINE_HEIGHT, _safe_text(text),
                        new_x='LMARGIN', new_y='NEXT', wrapmode=WrapMode.CHAR)

    def write_bullet(self, text, level=0):
        indent = 4 + level * 4
        bullet = '  ' * level + ('- ' if level > 0 else '* ')
        self.write_body(bullet + text, indent=indent)

    def write_code_block(self, lines):
        self.ln(1)
        self.set_font('Songti', '', CODE_FONT_SIZE)
        self.set_text_color(40, 40, 40)
        for line in lines:
            self.check_page_break(CODE_LINE_HEIGHT)
            self.set_fill_color(245, 245, 245)
            self.set_x(LEFT_MARGIN + 4)
            self.cell(CONTENT_W - 4, CODE_LINE_HEIGHT, _safe_text(line.replace('\t', '    ')),
                      fill=True, new_x='LMARGIN', new_y='NEXT')
        self.ln(1)

    def write_table(self, headers, rows, col_widths=None):
        self.ln(1)
        if col_widths is None:
            col_widths = [CONTENT_W / len(headers)] * len(headers)
        row_line_h = 5.6
        pad_x = 1.5
        pad_y = 1.5

        def wrap_cell(text, width):
            text = _safe_text(str(text))
            lines = []
            for paragraph in text.split('\n'):
                current = ''
                for ch in paragraph:
                    if self.get_string_width(current + ch) <= width:
                        current += ch
                    else:
                        if current:
                            lines.append(current)
                        current = ch
                lines.append(current)
            return lines or ['']

        def draw_row(cells, fill):
            self.set_font('Songti', '', BODY_FONT_SIZE)
            wrapped = [wrap_cell(c, col_widths[i] - pad_x * 2) for i, c in enumerate(cells)]
            row_h = max(len(lines) for lines in wrapped) * row_line_h + pad_y * 2
            self.check_page_break(row_h)
            y0 = self.get_y()
            x = LEFT_MARGIN
            self.set_fill_color(*fill)
            self.set_draw_color(0, 0, 0)
            for i, lines in enumerate(wrapped):
                self.rect(x, y0, col_widths[i], row_h, style='DF')
                self.set_xy(x + pad_x, y0 + pad_y)
                for line in lines:
                    self.cell(col_widths[i] - pad_x * 2, row_line_h, line,
                              new_x='LEFT', new_y='NEXT')
                    self.set_x(x + pad_x)
                x += col_widths[i]
            self.set_y(y0 + row_h)

        self.set_font('Songti', '', BODY_FONT_SIZE)
        self.set_text_color(0, 0, 0)
        draw_row(headers, (230, 230, 230))
        for row in rows:
            self.set_text_color(30, 30, 30)
            draw_row(row, (255, 255, 255))
        self.ln(1)

    def write_image(self, img_path, caption='', max_h=118):
        img_path = Path(img_path)
        if not img_path.exists():
            self._write_image_placeholder(img_path, caption)
            return
        img = Image.open(img_path)
        iw, ih = img.size
        max_w = CONTENT_W * 0.46
        ratio = min(max_w / iw, max_h / ih)
        draw_w = iw * ratio
        draw_h = ih * ratio
        total_h = draw_h + 18
        self.check_page_break(total_h)
        self.ln(3)
        x = LEFT_MARGIN + (CONTENT_W - draw_w) / 2
        self.image(str(img_path), x=x, y=self.get_y(), w=draw_w, h=draw_h)
        self.set_y(self.get_y() + draw_h + 2)
        if caption:
            self.set_font('Songti', '', 9)
            self.set_text_color(100, 100, 100)
            self.set_x(LEFT_MARGIN)
            self.cell(CONTENT_W, 5, _safe_text(caption), align='C', new_x='LMARGIN', new_y='NEXT')
            self.set_text_color(30, 30, 30)
        self.ln(3)

    def _write_image_placeholder(self, img_path, caption):
        self.missing_images.append((str(img_path.name), caption))
        box_h = 58
        self.check_page_break(box_h + 16)
        self.ln(3)
        x = LEFT_MARGIN + CONTENT_W * 0.22
        w = CONTENT_W * 0.56
        y = self.get_y()
        self.set_draw_color(120, 120, 120)
        self.set_fill_color(248, 248, 248)
        self.rect(x, y, w, box_h, style='DF')
        self.set_font('Songti', '', 11)
        self.set_text_color(120, 120, 120)
        self.set_xy(x, y + 18)
        self.cell(w, 7, '待补游戏截图', align='C', new_x='LEFT', new_y='NEXT')
        self.set_xy(x, y + 28)
        self.cell(w, 7, img_path.name, align='C', new_x='LEFT', new_y='NEXT')
        self.set_y(y + box_h + 2)
        self.set_font('Songti', '', 9)
        self.set_text_color(100, 100, 100)
        self.set_x(LEFT_MARGIN)
        self.cell(CONTENT_W, 5, _safe_text(caption + '（截图占位）'), align='C',
                  new_x='LMARGIN', new_y='NEXT')
        self.set_text_color(30, 30, 30)
        self.ln(3)


def _safe_text(text):
    return ''.join(c if ord(c) <= 0xFFFF else '?' for c in text)


def img(key):
    return SCREENSHOTS[key][0]


def write_document(pdf: DocPDF):
    pdf.add_page()
    pdf.write_h1('目  录')
    for item in [
        '一、引言',
        '    1.1 编写目的',
        '    1.2 软件概述',
        '    1.3 运行环境',
        '    1.4 术语与缩略语',
        '二、软件总体设计',
        '    2.1 软件需求概括',
        '    2.2 总体架构设计',
        '    2.3 模块划分与关系',
        '    2.4 场景与界面系统设计',
        '    2.5 主循环与资源加载设计',
        '三、核心模块详细设计',
        '    3.1 入口、主界面与页签',
        '    3.2 战斗：召唤、站位与合成',
        '    3.3 强化三选一与寨主技能',
        '    3.4 关卡、波次与敌人',
        '    3.5 好汉、招募与集市',
        '    3.6 寨主装备与养成',
        '    3.7 山寨、任务与图鉴',
        '    3.8 存档与云同步',
        '四、数据结构设计',
        '五、数据接口设计',
        '六、出错处理设计',
        '七、性能优化设计',
        '八、结论',
    ]:
        pdf.write_body(item)

    pdf.add_page()
    pdf.write_h1('一、引言')
    pdf.write_h2('1.1 编写目的')
    pdf.write_body(
        f'编写本设计说明书是{SOFTWARE_FULL_NAME} {SOFTWARE_VERSION}软件著作权登记材料的一部分。'
        '本文档说明本软件的功能范围、总体架构、核心模块、数据结构、接口、异常处理和性能方案，'
        '证明本软件为独立开发完成的原创游戏软件。'
    )
    pdf.write_body('本文档面向软件著作权审查人员及后续维护人员，描述技术实现，不包含运营数据和用户隐私数据。')

    pdf.write_h2('1.2 软件概述')
    pdf.write_body(
        '宋江别慌张是一款竖屏塔防小游戏。官军从画面上方压向寨墙，寨墙血量归零即本关失败。'
        '玩家从已拥有的好汉中选出四人上阵，用局内银币召唤，召唤结果在这四人中随机。'
        '两个同名同星的好汉可以合成，合成结果随机变成上阵四人中任意一人的高一星。'
        '另一项操作是强化，弹出三张词条卡供选择。寨主宋江站在寨墙前，按冷却自动放出剑气。'
    )
    pdf.write_body('本软件的主要功能包括:')
    for text in [
        '出征主界面: 展示本关首领、上阵好汉、关卡宝箱和开始挑战，底部为商城、好汉、出征、寨主、山寨五个页签。',
        '战斗系统: 召唤、站位扩展、同星合成、强化三选一、寨墙承伤、波次推进和胜负结算。',
        '好汉系统: 三十二员好汉，分射手、炮手、法师、战士、辅助、控制，各有绰号、兵器和被动。',
        '招募与集市: 招募令单抽和十连，十连必出史诗；通关第二十关后开放传说并设保底。集市用元宝或广告兑换资源。',
        '寨主养成: 佩剑、头盔、铠甲、战靴、玉佩、号角六件装备，以及天赋、天书、杏黄旗和装备箱。',
        '山寨经营: 水泊梁山地图连接每日任务、签到、巡山、生辰纲和每日挑战。',
        '存档同步: 进度先写本地，微信登录后与腾讯云 CloudBase 按更新时间对齐。',
    ]:
        pdf.write_bullet(text)
    p, c = img('home')
    pdf.write_image(p, c)

    pdf.write_h2('1.3 运行环境')
    pdf.write_table(
        ['环境项', '要求说明'],
        [
            ['客户端平台', '微信小游戏；同一工程可在团结编辑器内直接运行'],
            ['开发语言', 'C#、JavaScript'],
            ['游戏引擎', '团结引擎，内核为 Unity 2022.3'],
            ['画面', '竖屏。战斗用正交相机，界面按 1080 宽的画布排版'],
            ['后端平台', '腾讯云 CloudBase 云函数 biehuang-api + HTTP 访问'],
            ['数据存储', '本地存档；云端集合 biehuang_playerData'],
        ],
        [36, 129],
    )

    pdf.write_h2('1.4 术语与缩略语')
    pdf.write_table(
        ['术语', '含义说明'],
        [
            ['Sim', '纯 C# 战斗与养成规则，不依赖 Unity 画面对象'],
            ['Loadout', '一局开始前，把局外养成折算成的攻击力、词条和上阵名单'],
            ['召唤', '花费银币，在空位上随机召出上阵四人中的一员'],
            ['合成', '两个同名同星拖到一起，变成随机一位上阵好汉的高一星'],
            ['强化', '花费银币，从三张词条卡中选一张'],
            ['替天行道', '寨主宋江的自动技能，冷却结束后放出剑气'],
            ['GAME_KEY', '游戏代号 biehuang，用于云函数、集合名和存档隔离'],
        ],
        [36, 129],
    )

    pdf.write_h1('二、软件总体设计')
    pdf.write_h2('2.1 软件需求概括')
    pdf.write_body(
        '客户端负责镜头、界面、动画、音效、本地存档和云同步请求。'
        '战斗胜负、召唤花费、合成结果、词条效果和养成加成全部在 Sim 层结算，画面层只根据事件播放表现。'
    )
    for text in [
        '一局约数十秒到数分钟，竖屏操作集中在召唤、拖拽合成和三选一。',
        '主线五章共五十关，章名为金沙滩、三打祝家庄、夜打曾头市、大破连环马、三败高太尉。',
        '局外用体力出征，用招募令获得好汉，用铜钱、精铁升级和强化。',
        '断网时仍可游玩，进度保存在本地；登录成功后再与云端对齐。',
    ]:
        pdf.write_bullet(text)

    pdf.write_h2('2.2 总体架构设计')
    pdf.write_body('软件分为入口、规则、表现、存档和后端五层。规则层不引用 UnityEngine，便于单独推演一局。')
    pdf.write_code_block([
        'Game.cs                         启动、镜头、主界面与战斗切换',
        '  +-- HomeScreen.cs             五个页签和局外入口',
        '  +-- BattleView.cs             单位、寨墙、特效的画面',
        '  +-- BattleHud.cs              召唤、强化、三选一、结算',
        '  +-- Sim/Battle.cs             一局规则：召唤、合成、伤害、波次',
        '  +-- Sim/Defs.cs               好汉、敌人、章节、花费表',
        '  +-- Sim/Meta.cs               局外养成折算成 Loadout',
        '  +-- Profile.cs                本地存档读写',
        '  +-- CloudSync.cs / Backend.cs 登录、拉取、上传',
        '  +-- cloudfunctions/biehuang-api',
    ])

    pdf.write_h2('2.3 模块划分与关系')
    pdf.write_table(
        ['模块层', '代表文件', '功能简述'],
        [
            ['入口', 'Game.cs', '空场景启动后创建相机、主界面和战斗'],
            ['规则', 'Battle.cs / Defs.cs / Meta.cs', '召唤、合成、伤害、关卡和养成数值'],
            ['战斗表现', 'BattleView.cs / BattleHud.cs / WorldFx.cs', '单位移动、技能演出、按钮和弹窗'],
            ['局外界面', 'HomeScreen.cs / MetaUi.cs / MetaPanels.cs', '页签、招募、装备、任务、图鉴'],
            ['存档', 'Profile.cs / CloudSync.cs', '本地 JSON 与云端按更新时间对齐'],
            ['后端', 'biehuang-api', '微信登录、JWT、存档 pull 与 push'],
        ],
        [28, 62, 75],
    )
    pdf.write_body(
        '界面点击只调用 Sim 的公开方法，例如 Battle.Summon、Battle.Merge、Battle.Enhance。'
        'Sim 用事件把召唤、命中、波次和胜负通知给 BattleView，表现层不回写伤害数字。'
    )

    pdf.write_h2('2.4 场景与界面系统设计')
    pdf.write_body(
        '工程使用空场景启动。Game 在运行时创建相机和界面根节点，不依赖预先摆好的场景物体。'
        '局外是一张主界面，用底部五个页签切换商城、好汉、出征、寨主和山寨。战斗时隐藏主界面，叠上战场和操作条。'
    )
    p, c = img('levels')
    pdf.write_image(p, c)

    pdf.write_h2('2.5 主循环与资源加载设计')
    pdf.write_body(
        '目标帧率为 60。战斗每帧调用 Battle.Tick 推进时间、移动、攻击和波次，再由 BattleView 按事件刷新精灵。'
        '角色、敌人和界面图放在 Resources，按名字加载。命令行带截图目录时进入自检：机器人自动打一局并在关键节点存图。'
    )

    pdf.write_h1('三、核心模块详细设计')
    pdf.write_h2('3.1 入口、主界面与页签')
    pdf.write_body(
        'Game.Boot 在场景加载后创建唯一的 Game 对象。正常启动先显示封面，CloudSync 完成登录或超时后进入出征页。'
        '出征页中间是本关城门和首领，寨墙上站着当前上阵的四位好汉，下方是开始挑战。'
        '顶部有聚义宴、签到、关卡和设置；两侧有每日挑战、生辰纲、巡山、任务和图鉴。未解锁的入口点击后提示需要通关的关卡。'
        '底部页签从左到右为商城、好汉、出征、寨主、山寨。有可领取或可升级内容时，页签显示红点。'
    )

    pdf.write_h2('3.2 战斗：召唤、站位与合成')
    pdf.write_body(
        '一局开始时银币为关卡配置值，默认 100。召唤花费按本局已召唤次数查表，从 10 递增。'
        '初始 7 个站位，每成功招募 5 次增加 1 格，最多 12 格。站满后只能合成或挪位。'
    )
    pdf.write_body(
        '召唤结果只从上阵的四位好汉里随机。拖拽规则是：空位则移动；不同好汉或不同星则交换；'
        '同名且同星、星级小于 4 则合成。合成后该格变成上阵四人中随机一人的高一星，并立刻结算一次技能。'
        '保底：第一个达到三星及以上的若是辅助，下一个三星及以上必定是输出位。'
    )
    p, c = img('battle')
    pdf.write_image(p, c)
    pdf.write_body(
        '伤害由寨主攻击力、好汉等级、职业突破、强化词条、关卡职业增益和星级相乘，再乘该好汉的攻击系数。'
        '射手、战士、炮手、法师、控制、辅助的出手方式不同：有的一轮多箭，有的近身连击，有的落雷连锁，辅助不造成伤害只加攻速。'
    )

    pdf.write_h2('3.3 强化三选一与寨主技能')
    pdf.write_body(
        '强化按钮花费银币，费用随本局强化次数按 100 左移增长。免费强化次数优先消耗。'
        '弹出三张卡，内容是某位好汉的普攻强化、星级技能或传说大招。场上星级越高的好汉，对应词条权重越高；不在场的好汉也能出现，但概率较低。'
        '合成出四星时，额外弹出该好汉的技能三选一。'
    )
    p, c = img('cards')
    pdf.write_image(p, c)
    pdf.write_body(
        '寨主宋江的技能名为替天行道。基础伤害为攻击力的 2 倍，基础冷却 40 秒，首次在 12 秒时施放。'
        '剑气从寨墙推向城门方向，只覆盖寨墙前的一块区域。天赋、天书和号角会提高伤害、增加连斩或缩短冷却。'
        '技能转满后自动挥出，界面用环形冷却显示剩余时间。'
    )

    pdf.write_h2('3.4 关卡、波次与敌人')
    pdf.write_body(
        '主线每章 10 关，共 5 章 50 关。每一波有固定时长。第 4 波出现首领，第 9 波出现押纲精英。'
        '首领血量和移动按关卡倍率计算，随通关进度加压，避免养成落后仍能无伤通过。'
    )
    pdf.write_table(
        ['章节', '首领', '关卡举例'],
        [
            ['金沙滩', '官军头目', '金沙滩、鸭嘴滩、梁山泊'],
            ['三打祝家庄', '栾廷玉', '独龙冈、盘陀路、祝家庄'],
            ['夜打曾头市', '史文恭', '曾家五虎、曾头市'],
            ['大破连环马', '呼延灼', '钩镰枪、连环阵'],
            ['三败高太尉', '高俅', '十节度、擒高俅'],
        ],
        [40, 36, 89],
    )
    pdf.write_body(
        '敌人包括小兵、盾兵、骑兵、弩手、精英、押纲兵、飞鹰、重甲、火油兵和连环马。'
        '弩手停在远处放箭，飞鹰走空中路线，押纲兵被击倒后掉落生辰纲。部分关卡给某一职业伤害加成，出征前显示在关卡信息里。'
    )
    p, c = img('result')
    pdf.write_image(p, c)
    pdf.write_body('寨墙血量归零则失败，清空全部波次则胜利。结算给出奖励，并可查看各位好汉和宋江的输出统计。')

    pdf.write_h2('3.5 好汉、招募与集市')
    pdf.write_body(
        '好汉页管理上阵四人、等级和三套预设。好汉最高 30 级，升级消耗铜钱和碎片。'
        '重复获得的好汉转为碎片。上阵变化会写入存档，下一局 Loadout 使用新名单。'
    )
    p, c = img('heroes')
    pdf.write_image(p, c)
    pdf.write_body(
        '擂鼓招募消耗招募令。单次 1 枚，十连 10 枚。十连至少出一名史诗。'
        '通关第 20 关前不出现传说；开放后显示传说和史诗概率，并提示再招募多少次必得传说，保底次数为 40。'
        '每日可用广告免费招募，次数有上限。招募等级随招募次数提升。'
    )
    p, c = img('recruit')
    pdf.write_image(p, c)
    pdf.write_body(
        '梁山集市出售招募令、铜钱、精铁、体力和锦缎，价格用元宝。'
        '另有每日广告领取的元宝和体力，领完后按钮变为已领完。元宝不足时提示通过广告或任务获得。'
    )
    p, c = img('market')
    pdf.write_image(p, c)

    pdf.write_h2('3.6 寨主装备与养成')
    pdf.write_body(
        '寨主页中央是宋江，两侧六件装备：佩剑加攻击力，头盔加寨墙生命，铠甲加寨墙减伤，'
        '战靴加攻击速度，玉佩加暴击率，号角缩短大招冷却。装备用精铁升级，品质分档，突破后给对应职业额外伤害。'
        '强化上限随已通关关卡提高。'
    )
    p, c = img('forge')
    pdf.write_image(p, c)
    pdf.write_body(
        '页内四个入口为天赋、天书、杏黄旗和装备箱。天赋按层提高替天行道的伤害。'
        '天书分上下卷，影响剑气连斩、眩晕和对精英首领的倍率。杏黄旗提供全军伤害、对精英和首领的倍率，以及旗技震慑。'
        '装备箱打开后随机装备。这些加成在 BuildLoadout 时写入本局，战斗中不再读局外界面。'
    )
    p, c = img('talent')
    pdf.write_image(p, c)

    pdf.write_h2('3.7 山寨、任务与图鉴')
    pdf.write_body(
        '山寨页是水泊梁山地图，建筑包括聚义厅、铁匠铺、校场、酒馆、金库、水寨和山门。'
        '分别进入每日任务、装备页、好汉页、招募页、巡山、生辰纲和出征。金库上显示当前可领的巡山铜钱。'
    )
    p, c = img('fort')
    pdf.write_image(p, c)
    pdf.write_body(
        '每日任务包含出征、获胜、升级好汉、强化装备、观看广告和擂鼓招募，完成后发放元宝、招募令、铜钱或精铁。'
        '签到按日期领取。巡山按离线小时结算，上限 8 小时，可用广告增加次数。'
        '每日挑战和生辰纲在通关指定关卡后开放，各自有每日次数。图鉴记录已收集好汉，可领取收集奖励。'
    )
    p, c = img('task')
    pdf.write_image(p, c)
    p, c = img('codex')
    pdf.write_image(p, c)
    p, c = img('daily')
    pdf.write_image(p, c)
    pdf.write_body('战斗中可打开暂停和输出统计。暂停期间不推进 Battle.Tick。')
    p, c = img('pause')
    pdf.write_image(p, c)

    pdf.write_h2('3.8 存档与云同步')
    pdf.write_body(
        'Profile 把通关、好汉、装备、货币、任务和设置写成本地 JSON。每次招募、购买、通关和升级后立即保存。'
        'CloudSync 在启动时请求登录。微信环境下用 code 换用户身份；编辑器内使用本地调试身份。'
        '拉取云端存档后，按更新时间决定采用本地还是云端。本地较新则上传，云端较新则覆盖本地并刷新主界面。'
        '云同步没有单独界面，失败时保留本地进度，下次启动再试。'
    )

    pdf.write_h1('四、数据结构设计')
    pdf.write_h2('4.1 好汉与敌人')
    pdf.write_body(
        'HeroDef 记录姓名、绰号、职业、稀有度、攻击系数、冷却、攻击名称和大招。'
        'EnemyDef 记录生命、移速、对寨墙伤害、停步线和掉落银币。StageDef 记录关卡序号、首领、波次、寨墙生命、开局银币和职业增益。'
    )
    pdf.write_code_block([
        'HeroDef { Name, Nick, Kind, Rarity, Coef, Interval, AtkName, UltName, PassiveName }',
        'EnemyDef { Kind, Name, Hp, Speed, WallDps, StopY, Coin }',
        'StageDef { No, Name, BossKind, WallHp, StartCoins, BuffRole }',
        'ChapterDef { Name, Boss, Intro, Levels[10] }',
    ])

    pdf.write_h2('4.2 一局状态与局外存档')
    pdf.write_body(
        'Battle 持有银币、已召唤次数、已强化次数、站位列表、各位好汉的词条和输出统计、寨墙生命、当前波次。'
        'Loadout 是只读的开局加成，包含上阵名单、攻击力、暴击、好汉伤害倍率、寨墙倍率、替天行道参数和开局银币。'
        '玩家存档包含已通关数、体力及恢复时间、货币、好汉等级与碎片、装备等级与品质、天赋、天书、杏黄旗、招募保底、每日次数和音效开关。'
    )
    pdf.write_table(
        ['货币', '用途'],
        [
            ['银币', '仅本局使用，召唤和强化'],
            ['元宝', '集市购买'],
            ['铜钱', '好汉升级'],
            ['精铁', '装备强化'],
            ['招募令', '擂鼓招募'],
            ['体力', '出征消耗，上限 60，每 300 秒恢复 1 点，每局消耗 5 点'],
        ],
        [36, 129],
    )

    pdf.write_h1('五、数据接口设计')
    pdf.write_body('云函数 biehuang-api 提供四个路径。游戏代号为 biehuang，与其他游戏的集合和密钥分开。')
    pdf.write_table(
        ['方法与路径', '作用'],
        [
            ['POST /login', '用微信 code 或调试身份换取用户和 JWT'],
            ['POST /save/pull', '按用户读取 biehuang_playerData'],
            ['POST /save/push', '写入存档，保留较新的更新时间'],
            ['GET 或 POST /health', '返回服务是否可用'],
        ],
        [55, 110],
    )
    pdf.write_body(
        '客户端 Backend 统一组请求，附带令牌。pull 与 push 的正文是存档 JSON 和更新时间。'
        '环境变量要求 BIEHUANG_JWT_SECRET。微信真机另配小游戏的 AppId 和 Secret。'
    )
    pdf.write_code_block([
        '启动 -> POST /login',
        '登录成功 -> POST /save/pull',
        '比较本地与云端更新时间',
        '云端较新 -> 覆盖本地并刷新界面',
        '本地较新 -> POST /save/push',
    ])

    pdf.write_h1('六、出错处理设计')
    for text in [
        '银币不足时召唤和强化按钮不可用，不扣除货币。',
        '体力、招募令或元宝不足时弹出提示，不进入战斗、不发奖。',
        '广告未看完不发放招募、元宝或体力。每日次数用完后按钮改为明日或已领完。',
        '未达到解锁关卡时，每日挑战和生辰纲只提示所需关卡，不扣次数。',
        '云登录或存档请求失败时保留本地存档，不清空进度。',
        '云函数未知路径返回 404，内部错误返回错误码，不把密钥写入响应。',
        '战斗暂停或结算弹窗打开时不再推进 Tick，避免后台把寨墙打穿。',
    ]:
        pdf.write_bullet(text)

    pdf.write_h1('七、性能优化设计')
    pdf.write_body(
        '同屏敌人、箭矢、伤害数字和特效使用对象池，波次结束后回收。'
        'Battle.Tick 只做数值和位置，精灵的创建和销毁放在表现层，并按事件批量处理。'
        '界面图在首次使用时加载并缓存。竖屏界面按安全区排版，不每帧重建整页，只有页签切换或发奖后重建当前页。'
        '自检模式用机器人跑完一局，用于确认召唤、合成和结算不会中途抛错。'
    )

    pdf.write_h1('八、结论')
    pdf.write_body(
        '宋江别慌张把竖屏守寨、随机召唤、同星合成和三选一强化放在同一局内，'
        '局外用招募、装备、天赋和山寨任务承接养成。规则集中在 Sim，画面、存档和云函数分别独立。'
        '上述模块均已在本软件中实现，可作为软件著作权登记的文档鉴别材料。'
    )


def main():
    pdf = DocPDF()
    pdf.add_font('Songti', '', SONGTI_PATH)
    write_document(pdf)
    pdf.output(str(OUTPUT))

    from pypdf import PdfReader
    pages = len(PdfReader(str(OUTPUT)).pages)
    print('=' * 60)
    print('  宋江别慌张软著文档鉴别材料 PDF 生成报告')
    print('=' * 60)
    print(f'  输出文件: {OUTPUT}')
    print(f'  页数:     {pages}')
    if pdf.missing_images:
        print(f'  待补截图: {len(pdf.missing_images)} 张')
        for name, caption in pdf.missing_images:
            print(f'    - {name}  {caption}')
    else:
        print('  截图已全部写入')
    print('=' * 60)


if __name__ == '__main__':
    main()
