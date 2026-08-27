import * as PIXI from 'pixi.js';
import { SLICE_ROUNDS } from '@/balance/slice';
import { playAd } from '@/core/ads';
import { Game } from '@/core/Game';
import { loadMemory, rememberRun } from '@/core/memory';
import { Platform } from '@/core/PlatformService';
import type { Scene } from '@/core/SceneManager';
import { buzz, startBgm, unlockAudio } from '@/core/sfx';
import { fillCover, preloadSliceArt, tex, watchArt } from '@/core/TextureLoader';
import { track } from '@/core/track';
import { Ease, TweenManager } from '@/core/TweenManager';
import { bindPointerTap } from '@/minigame';
import { tellDay } from '@/game/DayStory';
import {
  acceptLoss,
  clearSelect,
  createRun,
  currentRound,
  grantContinue,
  hintFor,
  settleReact,
  partnersOf,
  selectChar,
  splitMark,
  tryPair,
  tick,
  winningWords,
  type PairAttempt,
  type RunState,
} from '@/game/PairEngine';
import { C, FONT } from '@/ui/theme';

function label(text: string, size: number, color: number, weight: '600' | '700' = '600'): PIXI.Text {
  const t = new PIXI.Text(text, {
    fontFamily: FONT,
    fontSize: size,
    fontWeight: weight,
    fill: color,
  });
  t.anchor.set(0.5);
  return t;
}

export class PlayScene implements Scene {
  readonly name = 'play';
  readonly container = new PIXI.Container();

  private _state: RunState = createRun();
  private _sourceText!: PIXI.Text;
  private _goalText!: PIXI.Text;
  private _becauseText!: PIXI.Text;
  private _ruleText!: PIXI.Text;
  private _bestText!: PIXI.Text;
  private _noticeLayer = new PIXI.Container();
  private _lineText!: PIXI.Text;
  private _hintText!: PIXI.Text;
  private _progressText!: PIXI.Text;
  private _livesText!: PIXI.Text;
  private _timerText!: PIXI.Text;
  private _timerFill!: PIXI.Graphics;
  private _slotA!: PIXI.Text;
  private _slotB!: PIXI.Text;
  private _slotWord!: PIXI.Text;
  private _comboText!: PIXI.Text;
  private _tiles = new Map<string, PIXI.Container>();
  private _tileLayer = new PIXI.Container();
  private _flyLayer = new PIXI.Container();
  private _verdict = new PIXI.Container();
  private _flash = new PIXI.Graphics();
  private _settle = new PIXI.Container();
  private _continue = new PIXI.Container();
  private _teach = new PIXI.Container();
  private _teachHold = false;
  private _bgG = new PIXI.Graphics();
  private _paperG = new PIXI.Graphics();
  private _lastPhase = '';
  private _paperMidY = 360;
  private _paperY = 120;
  private _timerTrack = { x: 40, y: 128, w: 670 };
  private _pulse = 0;
  private _startedAt = 0;
  private _adBusy = false;
  private _doubled = false;
  private _ended = false;
  private _misses = 0;
  private _reverses = 0;
  private _stopWatch: (() => void) | null = null;

  onEnter(): void {
    this.container.removeChildren();
    this._tiles.clear();
    this._state = createRun();
    this._lastPhase = '';
    this._pulse = 0;
    this._startedAt = Date.now();
    this._adBusy = false;
    this._doubled = false;
    this._ended = false;
    this._misses = 0;
    this._reverses = 0;
    this._teachHold = false;
    preloadSliceArt();
    this._stopWatch?.();
    this._stopWatch = watchArt(() => {
      this._paintWorld();
      this._paintNotice();
      this._buildTiles();
      this._paintTiles();
      Game.syncFrameToScreen();
    });
    this._buildChrome();
    this._sync(true);
    startBgm();
    track('run_start', { theme: 'workplace', seed: 'workplace-day', rounds: SLICE_ROUNDS.length });
  }

  onExit(): void {
    this._stopWatch?.();
    this._stopWatch = null;
    this.container.removeChildren();
    this._tiles.clear();
  }

  update(dt: number): void {
    if (this._teachHold) {
      this._pulse += dt;
      return;
    }
    if (
      this._state.phase === 'react'
      && this._state.roundIndex === 0
      && this._state.history.length === 1
      && this._state.lastAttempt?.kind === 'rewrite'
      && this._state.reactUntil <= 0.25
    ) {
      this._openTeach();
      return;
    }
    const before = this._state.phase;
    const beforeBecause = this._state.because;
    const beforeIndex = this._state.roundIndex;
    this._state = tick(this._state, dt);
    this._pulse += dt;
    if (this._state.phase !== before || this._state.phase === 'play') {
      this._sync(false);
    }
    if (before === 'react' && this._state.phase === 'play') {
      this._hideVerdict();
    }
    if (
      this._state.phase === 'play'
      && (before === 'react' || this._state.roundIndex !== beforeIndex || this._state.because !== beforeBecause)
    ) {
      if (this._state.lastAttempt?.kind === 'rewrite' || this._state.because) {
        this._slamNotice();
      }
    }
    this._pulseGuide();
  }

  private _H(): number {
    return Game.logicHeight;
  }

  private _buildChrome(): void {
    const H = this._H();
    const top = Game.safeTop;
    const headerY = Game.safeHeaderCenterY;
    const right = Game.contentRightX(12);

    this.container.addChild(this._bgG);

    const bar = new PIXI.Graphics();
    bar.beginFill(0x1a1410, 0.72).drawRect(0, 0, 750, top).endFill();
    this.container.addChild(bar);

    const title = label('对上了', 26, C.paper, '700');
    title.position.set(88, headerY);
    this.container.addChild(title);

    this._bestText = label('', 16, C.mute);
    this._bestText.anchor.set(0, 0.5);
    this._bestText.position.set(168, headerY);
    this.container.addChild(this._bestText);

    this._livesText = label('', 20, C.accentSoft, '600');
    this._livesText.anchor.set(1, 0.5);
    this._livesText.position.set(right, headerY);
    this.container.addChild(this._livesText);

    this._progressText = label('', 20, C.mute);
    this._progressText.position.set(220, top + 18);
    this.container.addChild(this._progressText);

    this._timerText = label('', 20, C.mute);
    this._timerText.position.set(540, top + 18);
    this.container.addChild(this._timerText);

    this._timerTrack = { x: 40, y: top + 34, w: 670 };
    const timerBg = new PIXI.Graphics();
    timerBg.beginFill(0x2c261f)
      .drawRoundedRect(this._timerTrack.x, this._timerTrack.y, this._timerTrack.w, 8, 4)
      .endFill();
    this.container.addChild(timerBg);
    this._timerFill = new PIXI.Graphics();
    this.container.addChild(this._timerFill);

    this._paperY = top + 48;
    const paperH = 268;
    this._paperMidY = this._paperY + 158;
    this.container.addChild(this._paperG);

    this._sourceText = label('', 18, C.inkSoft);
    this._sourceText.anchor.set(0, 0.5);
    this._sourceText.position.set(72, this._paperY + 36);
    this.container.addChild(this._sourceText);

    this._goalText = label('', 20, C.accent, '700');
    this._goalText.anchor.set(1, 0.5);
    this._goalText.position.set(678, this._paperY + 36);
    this.container.addChild(this._goalText);

    this._becauseText = label('', 22, C.accent, '700');
    this._becauseText.position.set(375, this._paperY + 68);
    this.container.addChild(this._becauseText);

    this._noticeLayer.position.set(375, this._paperMidY - 8);
    this.container.addChild(this._noticeLayer);

    this._ruleText = label('红字是靶。两个字对成词，红字就被改掉。', 20, C.inkSoft);
    this._ruleText.position.set(375, this._paperY + paperH - 28);
    this.container.addChild(this._ruleText);

    this._lineText = label('', 24, C.accent, '700');
    this._lineText.position.set(375, this._paperY + paperH + 8);
    this.container.addChild(this._lineText);

    this._comboText = label('', 20, C.good, '700');
    this._comboText.style.wordWrap = true;
    this._comboText.style.wordWrapWidth = 640;
    this._comboText.style.align = 'center';
    this._comboText.position.set(375, this._paperY + paperH + 24);
    this.container.addChild(this._comboText);

    this._hintText = label('', 22, C.mute);
    this._hintText.style.wordWrap = true;
    this._hintText.style.wordWrapWidth = 640;
    this._hintText.style.align = 'center';
    this._hintText.position.set(375, this._paperY + paperH + 58);
    this.container.addChild(this._hintText);

    const slotY = this._paperY + paperH + 114;
    this._buildSlots(slotY);

    const poolLabel = label('你的字', 18, C.mute);
    poolLabel.position.set(375, slotY + 78);
    this.container.addChild(poolLabel);

    this._tileLayer.position.set(0, slotY + 100);
    this.container.addChild(this._tileLayer);
    this._flash.alpha = 0;
    this.container.addChild(this._flash);
    this._verdict.visible = false;
    this.container.addChild(this._flyLayer);
    this.container.addChild(this._verdict);

    this._settle.visible = false;
    this._continue.visible = false;
    this._teach.visible = false;
    this.container.addChild(this._settle);
    this.container.addChild(this._continue);
    this.container.addChild(this._teach);

    this._paintWorld();
  }

  private _paintWorld(): void {
    const H = this._H();
    this._bgG.clear();
    const bg = tex('images/bg_desk.jpg');
    if (bg) fillCover(this._bgG, bg, 0, 0, 750, H);
    else this._bgG.beginFill(C.bg).drawRect(0, 0, 750, H).endFill();

    this._paperG.clear();
    const paper = tex('images/paper_notice.jpg');
    if (paper) {
      fillCover(this._paperG, paper, 28, this._paperY, 694, 276);
    } else {
      this._paperG.beginFill(C.paper).drawRoundedRect(40, this._paperY, 670, 248, 12).endFill();
    }
  }

  private _buildSlots(y: number): void {
    const drawBox = (x: number, caption: string): PIXI.Text => {
      const box = new PIXI.Graphics();
      const tile = tex('images/tile_paper.png');
      if (tile) fillCover(box, tile, x - 56, y - 40, 112, 80);
      else box.beginFill(C.tile).drawRoundedRect(x - 56, y - 40, 112, 80, 10).endFill();
      this.container.addChild(box);
      const cap = label(caption, 16, C.mute);
      cap.position.set(x, y + 52);
      this.container.addChild(cap);
      const t = label('', 40, C.ink, '700');
      t.position.set(x, y);
      this.container.addChild(t);
      return t;
    };
    this._slotA = drawBox(220, '先');
    const plus = label('+', 36, C.mute, '700');
    plus.position.set(320, y);
    this.container.addChild(plus);
    this._slotB = drawBox(420, '后');
    const eq = label('=', 32, C.mute, '700');
    eq.position.set(520, y);
    this.container.addChild(eq);
    this._slotWord = label('？', 36, C.accent, '700');
    this._slotWord.position.set(600, y);
    this.container.addChild(this._slotWord);

    const clear = new PIXI.Container();
    const cg = new PIXI.Graphics();
    cg.beginFill(0x000000, 0.001).drawRect(160, y - 44, 280, 88).endFill();
    clear.addChild(cg);
    clear.eventMode = 'static';
    bindPointerTap(clear, () => {
      this._state = clearSelect(this._state);
      this._sync(false);
      Game.syncFrameToScreen();
    }, { guard: () => this._state.phase === 'play' && !!this._state.selected });
    this.container.addChild(clear);
  }

  private _sync(rebuildTiles: boolean): void {
    const s = this._state;
    const total = SLICE_ROUNDS.length;
    const mem = loadMemory();
    this._progressText.text = `第 ${s.roundIndex + 1}/${total} 张 · ${s.cutIndex + 1}/${s.cutsNeed} 刀`;
    this._livesText.text = `还能错 ${s.missesLeft} 次`;
    this._timerText.text = `这张 ${Math.ceil(s.timeLeft)} 秒`;
    this._sourceText.text = s.source;
    this._goalText.text = s.mark ? `改掉「${s.mark}」` : '已经改掉了';
    this._becauseText.text = s.because ? `因为你组了「${s.because}」` : '';
    this._ruleText.text = this._isFirstLock()
      ? '第一步：只点「取」和「消」。'
      : (s.mark ? `这张纸第 ${s.cutIndex + 1}/${s.cutsNeed} 刀。改掉红字「${s.mark}」。` : '红字被你改掉了。');
    this._ruleText.visible = s.phase !== 'react';
    this._bestText.text = mem.bestRewrites > 0 ? `纪录 ${mem.bestRewrites}` : '';

    this._paintNotice();

    this._lineText.text = s.phase === 'react' ? (s.lastAttempt?.line ?? '') : '';
    this._lineText.style.fill = s.lastAttempt?.kind === 'rewrite' ? C.good
      : s.lastAttempt?.kind === 'trap' ? C.trap
        : C.accent;

    const story = tellDay(s);
    this._comboText.text = story.finished.length > 0
      ? story.line
      : (s.combo >= 2 ? `连对 ×${s.combo}` : '');
    this._hintText.text = hintFor(s);
    this._hintText.visible = s.phase === 'play';

    if (s.phase === 'react' && s.lastAttempt) {
      this._slotA.text = s.lastAttempt.a;
      this._slotB.text = s.lastAttempt.b;
      this._slotWord.text = s.lastAttempt.word || '？';
      this._slotWord.style.fill = s.lastAttempt.kind === 'rewrite' ? C.good
        : s.lastAttempt.kind === 'trap' ? C.trap
          : C.accent;
    } else {
      this._slotA.text = s.selected ?? '';
      this._slotB.text = '';
      this._slotWord.text = '？';
      this._slotWord.style.fill = C.accent;
    }

    const ratio = Math.max(0, s.timeLeft / Math.max(1, currentRound(s).seconds ?? 32));
    this._timerFill.clear();
    this._timerFill.beginFill(ratio < 0.25 ? C.trap : C.accent);
    this._timerFill.drawRoundedRect(
      this._timerTrack.x,
      this._timerTrack.y,
      this._timerTrack.w * ratio,
      8,
      4,
    );
    this._timerFill.endFill();

    if (rebuildTiles || this._tiles.size === 0 || (this._lastPhase === 'react' && s.phase === 'play')) {
      this._buildTiles();
    }
    this._paintTiles();

    if (s.phase === 'continue' && this._lastPhase !== 'continue') {
      this._showContinue();
    }
    if ((s.phase === 'won' || s.phase === 'lost') && this._lastPhase !== s.phase) {
      this._showSettle();
    }
    this._lastPhase = s.phase;
  }

  private _slamNotice(): void {
    this._noticeLayer.y = this._paperMidY + 36;
    this._noticeLayer.alpha = 0;
    TweenManager.to({
      target: this._noticeLayer,
      props: { y: this._paperMidY, alpha: 1 },
      duration: 0.32,
      ease: Ease.easeOutCubic,
    });
  }

  private _paintNotice(): void {
    this._noticeLayer.removeChildren();
    const s = this._state;
    const parts = splitMark(s.notice, s.mark);
    if (!parts) {
      const t = label(s.notice, 42, s.lastAttempt?.kind === 'trap' ? C.trap : C.ink, '700');
      this._noticeLayer.addChild(t);
      return;
    }
    const pre = label(parts.pre, 40, C.ink, '700');
    const mark = label(parts.mark, 46, C.trap, '700');
    const post = label(parts.post, 40, C.ink, '700');
    const gap = 4;
    const total = pre.width + mark.width + post.width + gap * 2;
    let x = -total / 2;
    pre.anchor.set(0, 0.5);
    mark.anchor.set(0, 0.5);
    post.anchor.set(0, 0.5);
    pre.position.x = x;
    x += pre.width + gap;
    mark.position.x = x;
    x += mark.width + gap;
    post.position.x = x;

    const smear = tex('images/ink_smear.png');
    if (smear) {
      const wound = new PIXI.Sprite(smear);
      wound.anchor.set(0.5);
      wound.width = mark.width + 36;
      wound.height = 34;
      wound.alpha = 0.9;
      wound.position.set(mark.x + mark.width / 2, 22);
      this._noticeLayer.addChild(wound);
    }
    this._noticeLayer.addChild(pre, mark, post);
    if (s.phase === 'play') {
      const cap = label(`↑ 改掉这个红字`, 20, C.trap, '700');
      cap.position.set(mark.x + mark.width / 2, 52);
      this._noticeLayer.addChild(cap);
    }
  }

  private _buildTiles(): void {
    this._tileLayer.removeChildren();
    this._tiles.clear();
    const pool = this._state.pool;
    const H = this._H();
    const bottom = Game.safeBottom;
    const avail = Math.max(220, H - this._tileLayer.y - bottom - 16);
    const cols = pool.length <= 4 ? pool.length : Math.min(4, pool.length);
    const rows = Math.ceil(pool.length / cols);
    const gap = 12;
    const tileW = Math.min(154, Math.floor((670 - gap * (cols - 1)) / cols));
    const tileH = Math.min(132, Math.floor((avail - gap * (rows - 1)) / rows));
    const left = (750 - (cols * tileW + (cols - 1) * gap)) / 2;
    const face = tex('images/tile_paper.png');

    pool.forEach((ch, i) => {
      const col = i % cols;
      const row = Math.floor(i / cols);
      const box = new PIXI.Container();
      box.position.set(
        left + col * (tileW + gap) + tileW / 2,
        row * (tileH + gap) + tileH / 2,
      );
      box.eventMode = 'static';
      const bg = new PIXI.Graphics();
      (bg as PIXI.Graphics & { __tw?: number; __th?: number }).__tw = tileW;
      (bg as PIXI.Graphics & { __tw?: number; __th?: number }).__th = tileH;
      box.addChild(bg);
      if (face) {
        const spr = new PIXI.Sprite(face);
        spr.anchor.set(0.5);
        spr.width = tileW;
        spr.height = tileH;
        box.addChild(spr);
      }
      box.addChild(label(ch, tileH > 110 ? 52 : 44, C.ink, '700'));
      bindPointerTap(box, () => this._onTap(ch), {
        guard: () => (
          this._state.phase === 'play'
          && !this._state.used.includes(ch)
          && (!this._isFirstLock() || ch === '取' || ch === '消')
        ),
      });
      this._tileLayer.addChild(box);
      this._tiles.set(ch, box);
    });
  }

  private _paintTiles(): void {
    const selected = this._state.selected;
    const partners = selected ? partnersOf(this._state, selected) : [];
    const guide = currentRound(this._state).guide;
    for (const [ch, box] of this._tiles) {
      const used = this._state.used.includes(ch);
      const isSel = selected === ch;
      const isPartner = partners.includes(ch);
      const bg = box.children[0] as PIXI.Graphics & { __tw?: number; __th?: number };
      const w = bg.__tw ?? 154;
      const h = bg.__th ?? 132;
      bg.clear();
      if (!tex('images/tile_paper.png')) {
        bg.beginFill(used ? C.tileDown : isSel ? C.accentSoft : C.tile);
        bg.drawRoundedRect(-w / 2, -h / 2, w, h, 12);
        bg.endFill();
      }
      const stroke = isSel ? C.accent : isPartner ? C.good : 0x000000;
      bg.lineStyle(isSel || isPartner ? 5 : 0, stroke, isSel || isPartner ? 1 : 0);
      if (isSel || isPartner) bg.drawRoundedRect(-w / 2, -h / 2, w, h, 12);
      const lockedOut = this._isFirstLock() && ch !== '取' && ch !== '消';
      box.alpha = used || lockedOut ? 0.22 : 1;
      box.scale.set(1);
      if (guide && this._state.roundIndex === 0 && !used) {
        box.name = ch === guide[0] || ch === guide[1] ? `guide-${ch}` : '';
      }
    }
  }

  private _pulseGuide(): void {
    if (this._state.phase !== 'play' || this._state.roundIndex !== 0) return;
    const guide = currentRound(this._state).guide;
    if (!guide || this._state.history.length > 0) return;
    const target = this._state.selected ? guide[1] : guide[0];
    const box = this._tiles.get(target);
    if (!box) return;
    const s = 1 + Math.sin(this._pulse * 5) * 0.07;
    box.scale.set(s);
  }

  private _isFirstLock(): boolean {
    return this._state.roundIndex === 0 && this._state.history.length === 0;
  }

  private _onTap(ch: string): void {
    if (this._state.phase !== 'play') return;
    if (this._isFirstLock() && ch !== '取' && ch !== '消') return;
    unlockAudio();
    if (!this._state.selected || this._state.selected === ch) {
      this._state = selectChar(this._state, ch);
      buzz('tap');
      this._sync(false);
      Game.syncFrameToScreen();
      return;
    }
    const a = this._state.selected;
    const target = this._state.mark;
    const { state, attempt } = tryPair(this._state, a, ch);
    this._state = state;
    track('pair_try', {
      a: attempt.a,
      b: attempt.b,
      result: attempt.kind,
      word: attempt.word,
      notice_id: currentRound(state).id,
    });
    if (attempt.kind === 'rewrite') {
      track('notice_rewrite', {
        notice_id: currentRound(state).id,
        word: attempt.word,
        from: attempt.from,
        to: attempt.to,
      });
      buzz('rewrite');
    } else {
      if (attempt.kind === 'reversed') this._reverses += 1;
      else this._misses += 1;
      buzz('miss');
    }
    this._fillSlot(attempt);
    this._showVerdict(attempt, target);
    this._flashPaper(attempt.kind);
    this._fly(attempt, a, ch);
    this._sync(false);
    Game.syncFrameToScreen();
  }

  private _showVerdict(attempt: PairAttempt, target: string): void {
    this._verdict.removeChildren();
    this._verdict.visible = true;
    this._verdict.alpha = 1;
    const H = this._H();
    const band = new PIXI.Graphics();
    band.beginFill(0x120f0c, 0.62).drawRect(0, this._paperMidY - 90, 750, 180).endFill();
    this._verdict.addChild(band);

    const moreCuts = attempt.kind === 'rewrite'
      && this._state.cutIndex + 1 < this._state.cutsNeed;
    const copy = attempt.kind === 'rewrite'
      ? {
        title: moreCuts ? '改掉了，还能再改' : '改掉了',
        sub: moreCuts
          ? `「${target}」变成了「${attempt.word}」，红字跟着换`
          : `「${target}」变成了「${attempt.word}」`,
        color: C.good,
      }
      : attempt.kind === 'reversed'
        ? { title: '字对了，顺序反了', sub: attempt.line.replace('顺序反了，', ''), color: C.accent }
        : attempt.kind === 'trap'
          ? { title: '对上了，但更糟', sub: `「${attempt.word}」把局面坐实了`, color: C.trap }
          : { title: '这两个不成词', sub: '换两个字，或换个顺序', color: 0xc8b8a4 };

    const title = label(copy.title, 72, copy.color, '700');
    title.position.set(375, this._paperMidY - 18);
    this._verdict.addChild(title);
    const sub = label(copy.sub, 36, C.paper, '700');
    sub.position.set(375, this._paperMidY + 42);
    this._verdict.addChild(sub);
    if (attempt.kind === 'rewrite') {
      const fromTo = label(`${attempt.from} → ${attempt.to}`, 22, C.paper);
      fromTo.position.set(375, Math.min(H - 80, this._paperMidY + 82));
      this._verdict.addChild(fromTo);
    }
    this._verdict.scale.set(0.86);
    TweenManager.to({
      target: this._verdict,
      props: { alpha: 1 },
      duration: 0.12,
    });
    this._verdict.scale.set(1);
  }

  private _openTeach(): void {
    this._teachHold = true;
    const H = this._H();
    this._teach.removeChildren();
    this._teach.visible = true;
    this._teach.eventMode = 'static';

    const dim = new PIXI.Graphics();
    dim.beginFill(0x120f0c, 0.88).drawRect(0, 0, 750, H).endFill();
    this._teach.addChild(dim);

    const panelH = Math.min(640, H - Game.safeTop - 48);
    const py = Math.max(Game.safeTop + 24, (H - panelH) / 2);
    const panel = new PIXI.Graphics();
    const face = tex('images/panel_paper.png');
    if (face) fillCover(panel, face, 36, py, 678, panelH);
    else panel.beginFill(C.paper).drawRoundedRect(36, py, 678, panelH, 16).endFill();
    this._teach.addChild(panel);

    const add = (text: string, y: number, size: number, color: number, weight: '600' | '700' = '600') => {
      const t = label(text, size, color, weight);
      t.position.set(375, y);
      this._teach.addChild(t);
    };

    add('刚才你做对了', py + 48, 36, C.good, '700');
    add('今晚全体加班 → 今晚全体取消', py + 92, 22, C.ink, '700');
    add('规则只有这一条', py + 148, 28, C.ink, '700');

    add('对', py + 200, 24, C.good, '700');
    add('取 + 消 = 取消    红字被改掉', py + 236, 22, C.ink);
    add('摸 + 鱼 = 摸鱼    也能改掉加班', py + 270, 22, C.ink);

    add('错', py + 326, 24, C.trap, '700');
    add('加 + 班 = 加班    通知更冲', py + 362, 22, C.ink);
    add('消 + 取            字对了，顺序反了', py + 396, 22, C.ink);
    add('取 + 鱼            这两个不成词', py + 430, 22, C.ink);

    add('没有标准答案。能改掉红字就是对。', py + 468, 22, C.inkSoft);
    add('同一张纸还要再改。红字换成你刚对上的词。', py + 502, 22, C.ink);

    const btn = this._makeBtn('继续改', C.accent, C.paper);
    btn.position.set(375, py + panelH - 56);
    bindPointerTap(btn, () => this._closeTeach());
    this._teach.addChild(btn);
    Game.syncFrameToScreen();
  }

  private _closeTeach(): void {
    this._teachHold = false;
    this._teach.visible = false;
    this._teach.removeChildren();
    this._hideVerdict();
    this._state = settleReact(this._state);
    this._sync(true);
    this._slamNotice();
    Game.syncFrameToScreen();
  }

  private _hideVerdict(): void {
    TweenManager.to({
      target: this._verdict,
      props: { alpha: 0 },
      duration: 0.18,
      onComplete: () => {
        this._verdict.visible = false;
        this._verdict.removeChildren();
        this._verdict.alpha = 1;
      },
    });
  }

  private _flashPaper(kind: PairAttempt['kind']): void {
    const color = kind === 'rewrite' ? C.good
      : kind === 'trap' ? C.trap
        : kind === 'reversed' ? C.accent
          : 0x4a4038;
    this._flash.clear();
    this._flash.beginFill(color, 0.28);
    this._flash.drawRoundedRect(28, this._paperY, 694, 276, 12);
    this._flash.endFill();
    this._flash.alpha = 1;
    TweenManager.to({
      target: this._flash,
      props: { alpha: 0 },
      duration: 0.45,
      ease: Ease.easeOutCubic,
    });
  }

  private _fillSlot(attempt: PairAttempt): void {
    this._slotA.text = attempt.a;
    this._slotB.text = attempt.b;
    this._slotWord.text = attempt.word || '？';
    this._slotWord.style.fill = attempt.kind === 'rewrite' ? C.good
      : attempt.kind === 'trap' ? C.trap
        : C.accent;
  }

  private _fly(attempt: PairAttempt, a: string, b: string): void {
    this._flyLayer.removeChildren();
    this._flyLayer.alpha = 1;
    const aBox = this._tiles.get(a);
    const bBox = this._tiles.get(b);
    const startA = aBox
      ? new PIXI.Point(aBox.x, aBox.y + this._tileLayer.y)
      : new PIXI.Point(240, 900);
    const startB = bBox
      ? new PIXI.Point(bBox.x, bBox.y + this._tileLayer.y)
      : new PIXI.Point(510, 900);
    const mid = new PIXI.Point(375, this._paperMidY);
    const color = attempt.kind === 'rewrite' ? C.good
      : attempt.kind === 'trap' ? C.trap
        : C.accent;
    const left = label(a, 64, color, '700');
    left.position.copyFrom(startA);
    const right = label(b, 64, color, '700');
    right.position.copyFrom(startB);
    this._flyLayer.addChild(left, right);

    if (attempt.kind === 'reversed') {
      this._flyReversed(left, right, mid);
      return;
    }
    if (attempt.kind === 'miss') {
      this._flyMiss(left, right, mid);
      return;
    }
    this._flyHit(attempt, left, right, mid, color);
  }

  private _flyReversed(left: PIXI.Text, right: PIXI.Text, mid: PIXI.Point): void {
    TweenManager.to({ target: left, props: { x: mid.x + 40, y: mid.y }, duration: 0.28, ease: Ease.easeOutCubic });
    TweenManager.to({
      target: right,
      props: { x: mid.x - 40, y: mid.y },
      duration: 0.28,
      ease: Ease.easeOutCubic,
      onComplete: () => {
        TweenManager.to({
          target: left,
          props: { x: mid.x - 46, rotation: -0.12 },
          duration: 0.22,
          ease: Ease.easeOutBack,
        });
        TweenManager.to({
          target: right,
          props: { x: mid.x + 46, rotation: 0.12 },
          duration: 0.22,
          ease: Ease.easeOutBack,
          onComplete: () => this._clearFly(0.35),
        });
      },
    });
  }

  private _flyMiss(left: PIXI.Text, right: PIXI.Text, mid: PIXI.Point): void {
    TweenManager.to({ target: left, props: { x: mid.x - 30, y: mid.y + 8, alpha: 0.3 }, duration: 0.28 });
    TweenManager.to({
      target: right,
      props: { x: mid.x + 30, y: mid.y + 8, alpha: 0.3 },
      duration: 0.28,
      onComplete: () => this._clearFly(0.12),
    });
  }

  private _flyHit(attempt: PairAttempt, left: PIXI.Text, right: PIXI.Text, mid: PIXI.Point, color: number): void {
    TweenManager.to({
      target: left,
      props: { x: mid.x - 46, y: mid.y },
      duration: 0.34,
      ease: Ease.easeOutCubic,
    });
    TweenManager.to({
      target: right,
      props: { x: mid.x + 46, y: mid.y },
      duration: 0.34,
      ease: Ease.easeOutCubic,
      onComplete: () => {
        left.visible = false;
        right.visible = false;
        const word = label(attempt.word || '', 56, color, '700');
        word.position.set(mid.x, mid.y);
        word.alpha = 0;
        this._flyLayer.addChild(word);
        if (attempt.kind === 'rewrite' || attempt.kind === 'trap') {
          this._dropStamp(mid, attempt.kind === 'trap');
        }
        TweenManager.to({
          target: word,
          props: { alpha: 1, y: mid.y - 12 },
          duration: 0.28,
          ease: Ease.easeOutBack,
          onComplete: () => this._clearFly(0.5),
        });
      },
    });
  }

  private _dropStamp(mid: PIXI.Point, trap: boolean): void {
    const seal = tex('images/stamp_seal.png');
    const stamp = seal ? new PIXI.Sprite(seal) : label('改', 88, trap ? C.trap : C.accent, '700');
    stamp.alpha = 0;
    stamp.position.set(mid.x + 148, mid.y - 18);
    stamp.rotation = trap ? 0.18 : -0.28;
    if (stamp instanceof PIXI.Sprite) {
      stamp.anchor.set(0.5);
      stamp.width = 150;
      stamp.height = 150;
      stamp.tint = trap ? 0x5a1e1e : 0xffffff;
    }
    this._flyLayer.addChild(stamp);
    if (seal) {
      const word = label('改', 42, trap ? C.paper : C.paper, '700');
      word.position.set(mid.x + 148, mid.y - 18);
      word.rotation = stamp.rotation;
      word.alpha = 0;
      this._flyLayer.addChild(word);
      TweenManager.to({ target: word, props: { alpha: 0.95 }, duration: 0.18, ease: Ease.easeOutBack });
    }
    buzz('stamp');
    TweenManager.to({
      target: stamp,
      props: { alpha: trap ? 0.7 : 0.92 },
      duration: 0.18,
      ease: Ease.easeOutBack,
    });
  }

  private _clearFly(delay: number): void {
    TweenManager.to({
      target: this._flyLayer,
      props: { alpha: 1 },
      duration: 0.16,
      delay,
      onComplete: () => {
        this._flyLayer.removeChildren();
        this._flyLayer.alpha = 1;
      },
    });
  }

  private _endFields(cleared: boolean) {
    return {
      cleared,
      duration_ms: Date.now() - this._startedAt,
      words: winningWords(this._state),
      misses: this._misses,
      reverses: this._reverses,
      consequences: this._state.consequenceFired,
    };
  }

  private _commitEnd(cleared: boolean, doubled: boolean): void {
    if (this._ended && !doubled) return;
    const words = winningWords(this._state);
    rememberRun(words, doubled);
    if (!this._ended) {
      track('run_end', this._endFields(cleared));
      this._ended = true;
    }
  }

  private _showContinue(): void {
    const H = this._H();
    this._continue.removeChildren();
    this._continue.visible = true;
    this._continue.eventMode = 'static';
    this._settle.visible = false;

    const dim = new PIXI.Graphics();
    dim.beginFill(0x120f0c, 0.84).drawRect(0, 0, 750, H).endFill();
    this._continue.addChild(dim);

    const kept = this._state.history.slice(-3);
    const panelH = 440 + kept.length * 36;
    const py = Math.max(Game.safeTop + 60, (H - panelH) / 2);
    const g = new PIXI.Graphics();
    const face = tex('images/panel_paper.png');
    if (face) fillCover(g, face, 48, py, 654, panelH);
    else g.beginFill(C.paper).drawRoundedRect(48, py, 654, panelH, 16).endFill();
    this._continue.addChild(g);

    this._continue.addChild(Object.assign(label('这句还能保住', 36, C.accent, '700'), { x: 375, y: py + 52 }));
    this._continue.addChild(Object.assign(
      label(this._state.lastAttempt?.line || '机会用完了', 22, C.ink),
      { x: 375, y: py + 104 },
    ));
    this._continue.addChild(Object.assign(
      label(kept.length ? '已经改过的还在：' : '看一段，再给一次机会', 20, C.inkSoft),
      { x: 375, y: py + 146 },
    ));
    kept.forEach((h, i) => {
      this._continue.addChild(Object.assign(
        label(`${h.from} → ${h.to}`, 20, C.ink),
        { x: 375, y: py + 184 + i * 34 },
      ));
    });

    const adBtn = this._makeBtn('看广告续一次', C.accent, C.paper);
    adBtn.position.set(375, py + panelH - 132);
    bindPointerTap(adBtn, () => { void this._onContinueAd(); }, { guard: () => !this._adBusy });
    this._continue.addChild(adBtn);

    const skip = this._makeBtn('算了，今天到这', 0x4a4038, C.paper);
    skip.position.set(375, py + panelH - 52);
    bindPointerTap(skip, () => {
      this._state = acceptLoss(this._state);
      this._continue.visible = false;
      this._sync(false);
      Game.syncFrameToScreen();
    });
    this._continue.addChild(skip);
    Game.syncFrameToScreen();
  }

  private async _onContinueAd(): Promise<void> {
    if (this._adBusy) return;
    this._adBusy = true;
    const ok = await playAd('continue');
    this._adBusy = false;
    if (!ok) return;
    this._state = grantContinue(this._state);
    this._continue.visible = false;
    buzz('continue');
    this._sync(false);
    Game.syncFrameToScreen();
  }

  private _showSettle(): void {
    const H = this._H();
    this._continue.visible = false;
    this._settle.removeChildren();
    this._settle.visible = true;
    this._settle.eventMode = 'static';

    const dim = new PIXI.Graphics();
    dim.beginFill(0x120f0c, 0.84).drawRect(0, 0, 750, H).endFill();
    this._settle.addChild(dim);

    const won = this._state.phase === 'won';
    const words = winningWords(this._state);
    const story = tellDay(this._state);
    const board = story.papers.map((p) => p.end);
    this._commitEnd(won, false);

    const extra = Platform.isMinigame ? 0 : 76;
    const gap = board.length > 6 ? 28 : 36;
    const panelH = Math.min(H - Game.safeTop - 24, 320 + board.length * gap + extra);
    const py = Math.max(Game.safeTop + 20, (H - panelH) / 2);
    const g = new PIXI.Graphics();
    const face = tex('images/panel_paper.png');
    if (face) fillCover(g, face, 48, py, 654, panelH);
    else g.beginFill(C.paper).drawRoundedRect(48, py, 654, panelH, 16).endFill();
    this._settle.addChild(g);

    this._settle.addChild(Object.assign(
      label(won ? '今天的通知' : '改到这里', 28, won ? C.good : C.trap, '700'),
      { x: 375, y: py + 40 },
    ));
    const title = label(story.title, 40, C.ink, '700');
    title.style.wordWrap = true;
    title.style.wordWrapWidth = 580;
    title.style.align = 'center';
    title.position.set(375, py + 92);
    this._settle.addChild(title);

    const because = this._state.consequenceFired > 0
      ? `因为你组了${this._state.record.slice(0, this._state.consequenceFired).join('、')}，后面才变成这样`
      : (won ? '别人对不上你这一天' : (this._state.lastAttempt?.line || '换顺序，或换两个字'));
    this._settle.addChild(Object.assign(label(because, 20, C.inkSoft), { x: 375, y: py + 138 }));

    const lineSize = board.length > 6 ? 22 : 26;
    board.forEach((text, i) => {
      this._settle.addChild(Object.assign(
        label(text, lineSize, C.ink, '700'),
        { x: 375, y: py + 180 + i * gap },
      ));
    });

    const adBtn = this._makeBtn(this._doubled ? '已记双倍' : '看广告记双倍', this._doubled ? 0x4a4038 : C.accent, C.paper);
    adBtn.position.set(375, py + panelH - (Platform.isMinigame ? 124 : 200));
    bindPointerTap(adBtn, () => { void this._onDoubleAd(); }, {
      guard: () => !this._adBusy && !this._doubled && words.length > 0,
    });
    this._settle.addChild(adBtn);

    if (!Platform.isMinigame) {
      const copy = this._makeBtn('复制这一天', 0x5a5148, C.paper);
      copy.position.set(375, py + panelH - 124);
      bindPointerTap(copy, () => this._copyDay());
      this._settle.addChild(copy);
    }

    const btn = this._makeBtn('再来一天', C.ink, C.paper);
    btn.position.set(375, py + panelH - 48);
    bindPointerTap(btn, () => this.onEnter());
    this._settle.addChild(btn);
    Game.syncFrameToScreen();
  }

  private _copyDay(): void {
    const text = tellDay(this._state).share;
    const done = (): void => { Platform.showToast('已复制今天的通知'); };
    if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
      navigator.clipboard.writeText(text).then(done).catch(done);
      return;
    }
    done();
  }

  private async _onDoubleAd(): Promise<void> {
    if (this._adBusy || this._doubled) return;
    this._adBusy = true;
    const ok = await playAd('double');
    this._adBusy = false;
    if (!ok) return;
    this._doubled = true;
    this._commitEnd(this._state.phase === 'won', true);
    this._showSettle();
  }

  private _makeBtn(text: string, fill: number, ink: number): PIXI.Container {
    const btn = new PIXI.Container();
    const bg = new PIXI.Graphics();
    bg.beginFill(fill).drawRoundedRect(-160, -34, 320, 68, 12).endFill();
    btn.addChild(bg);
    btn.addChild(label(text, 26, ink, '700'));
    btn.eventMode = 'static';
    return btn;
  }
}
