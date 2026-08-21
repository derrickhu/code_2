import * as PIXI from 'pixi.js';
import { SLICE_ROUNDS } from '@/balance/slice';
import { Game } from '@/core/Game';
import type { Scene } from '@/core/SceneManager';
import { Ease, TweenManager } from '@/core/TweenManager';
import { bindPointerTap } from '@/minigame';
import {
  clearSelect,
  createRun,
  currentRound,
  hintFor,
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
  private _settle = new PIXI.Container();
  private _lastPhase = '';
  private _paperMidY = 360;
  private _timerTrack = { x: 40, y: 128, w: 670 };
  private _pulse = 0;

  onEnter(): void {
    this.container.removeChildren();
    this._tiles.clear();
    this._state = createRun();
    this._lastPhase = '';
    this._pulse = 0;
    this._buildChrome();
    this._sync(true);
    console.log('[pair] run_start', { theme: 'workplace', rounds: SLICE_ROUNDS.length });
  }

  onExit(): void {
    this.container.removeChildren();
    this._tiles.clear();
  }

  update(dt: number): void {
    const before = this._state.phase;
    this._state = tick(this._state, dt);
    this._pulse += dt;
    if (this._state.phase !== before || this._state.phase === 'play') {
      this._sync(false);
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

    const g = new PIXI.Graphics();
    g.beginFill(C.bg).drawRect(0, 0, 750, H).endFill();
    g.beginFill(0x241e18).drawRect(0, 0, 750, top).endFill();
    this.container.addChild(g);

    const title = label('对上了', 26, C.paper, '700');
    title.position.set(88, headerY);
    this.container.addChild(title);

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

    const paperY = top + 56;
    const paperH = 248;
    this._paperMidY = paperY + 148;
    const paper = new PIXI.Graphics();
    paper.beginFill(C.paper).drawRoundedRect(40, paperY, 670, paperH, 12).endFill();
    paper.lineStyle(2, C.paperLine);
    paper.drawRoundedRect(40, paperY, 670, paperH, 12);
    this.container.addChild(paper);

    this._sourceText = label('', 18, C.inkSoft);
    this._sourceText.anchor.set(0, 0.5);
    this._sourceText.position.set(64, paperY + 28);
    this.container.addChild(this._sourceText);

    this._goalText = label('', 20, C.accent, '700');
    this._goalText.anchor.set(1, 0.5);
    this._goalText.position.set(686, paperY + 28);
    this.container.addChild(this._goalText);

    this._noticeLayer.position.set(375, this._paperMidY);
    this.container.addChild(this._noticeLayer);

    this._lineText = label('', 24, C.accent, '700');
    this._lineText.position.set(375, paperY + paperH - 28);
    this.container.addChild(this._lineText);

    this._comboText = label('', 20, C.good, '700');
    this._comboText.position.set(375, paperY + paperH + 28);
    this.container.addChild(this._comboText);

    this._hintText = label('', 22, C.mute);
    this._hintText.position.set(375, paperY + paperH + 62);
    this.container.addChild(this._hintText);

    const slotY = paperY + paperH + 118;
    this._buildSlots(slotY);

    const poolLabel = label('你的字', 18, C.mute);
    poolLabel.position.set(375, slotY + 78);
    this.container.addChild(poolLabel);

    this._tileLayer.position.set(0, slotY + 100);
    this.container.addChild(this._tileLayer);
    this.container.addChild(this._flyLayer);

    this._settle.visible = false;
    this.container.addChild(this._settle);
  }

  private _buildSlots(y: number): void {
    const drawBox = (x: number, caption: string): PIXI.Text => {
      const box = new PIXI.Graphics();
      box.beginFill(C.tile).drawRoundedRect(x - 56, y - 40, 112, 80, 10).endFill();
      box.lineStyle(2, C.paperLine);
      box.drawRoundedRect(x - 56, y - 40, 112, 80, 10);
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
    this._progressText.text = `已改 ${s.history.length} / ${total} 句`;
    this._livesText.text = `还能错 ${s.missesLeft} 次`;
    this._timerText.text = `这句 ${Math.ceil(s.timeLeft)} 秒`;
    this._sourceText.text = s.source;
    this._goalText.text = s.goal;

    this._paintNotice();

    this._lineText.text = s.phase === 'react' ? (s.lastAttempt?.line ?? '') : '';
    this._lineText.style.fill = s.lastAttempt?.kind === 'rewrite' ? C.good
      : s.lastAttempt?.kind === 'trap' ? C.trap
        : C.accent;

    this._comboText.text = s.combo >= 2 ? `连对 ×${s.combo}` : '';
    this._hintText.text = hintFor(s);
    this._hintText.visible = s.phase === 'play';

    this._slotA.text = s.selected ?? '';
    this._slotB.text = '';
    this._slotWord.text = '？';
    this._slotWord.style.fill = C.accent;

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

    if ((s.phase === 'won' || s.phase === 'lost') && this._lastPhase !== s.phase) {
      this._showSettle();
    }
    this._lastPhase = s.phase;
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
    const mark = label(parts.mark, 44, C.trap, '700');
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
    this._noticeLayer.addChild(pre, mark, post);
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
      box.addChild(label(ch, tileH > 110 ? 52 : 44, C.ink, '700'));
      bindPointerTap(box, () => this._onTap(ch), {
        guard: () => this._state.phase === 'play' && !this._state.used.includes(ch),
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
      bg.beginFill(used ? C.tileDown : isSel ? C.accentSoft : C.tile);
      bg.drawRoundedRect(-w / 2, -h / 2, w, h, 12);
      bg.endFill();
      const stroke = isSel ? C.accent : isPartner ? C.good : C.paperLine;
      bg.lineStyle(isSel || isPartner ? 5 : 2, stroke);
      bg.drawRoundedRect(-w / 2, -h / 2, w, h, 12);
      box.alpha = used ? 0.28 : 1;
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
    const s = 1 + Math.sin(this._pulse * 5) * 0.04;
    box.scale.set(s);
  }

  private _onTap(ch: string): void {
    if (this._state.phase !== 'play') return;
    if (!this._state.selected || this._state.selected === ch) {
      this._state = selectChar(this._state, ch);
      this._sync(false);
      Game.syncFrameToScreen();
      return;
    }
    const a = this._state.selected;
    const { state, attempt } = tryPair(this._state, a, ch);
    this._state = state;
    console.log('[pair] pair_try', { a: attempt.a, b: attempt.b, result: attempt.kind, word: attempt.word });
    this._fillSlot(attempt);
    this._fly(attempt, a, ch);
    this._sync(false);
    Game.syncFrameToScreen();
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
        const word = label(attempt.word || (a + b), 56, color, '700');
        word.position.set(mid.x, mid.y);
        word.alpha = 0;
        this._flyLayer.addChild(word);
        if (attempt.kind === 'rewrite') {
          const stamp = label('改', 88, C.accent, '700');
          stamp.alpha = 0;
          stamp.position.set(mid.x + 150, mid.y - 20);
          stamp.rotation = -0.28;
          this._flyLayer.addChild(stamp);
          TweenManager.to({
            target: stamp,
            props: { alpha: 0.85 },
            duration: 0.18,
            ease: Ease.easeOutBack,
          });
        }
        TweenManager.to({
          target: word,
          props: { alpha: 1, y: mid.y - 12 },
          duration: 0.28,
          ease: Ease.easeOutBack,
          onComplete: () => {
            TweenManager.to({
              target: this._flyLayer,
              props: { alpha: 1 },
              duration: 0.2,
              delay: 0.45,
              onComplete: () => {
                this._flyLayer.removeChildren();
                this._flyLayer.alpha = 1;
              },
            });
          },
        });
      },
    });
  }

  private _showSettle(): void {
    const H = this._H();
    this._settle.removeChildren();
    this._settle.visible = true;
    this._settle.eventMode = 'static';

    const dim = new PIXI.Graphics();
    dim.beginFill(0x120f0c, 0.84).drawRect(0, 0, 750, H).endFill();
    this._settle.addChild(dim);

    const won = this._state.phase === 'won';
    const words = winningWords(this._state);
    const panelH = Math.min(760, 360 + words.length * 40);
    const py = Math.max(Game.safeTop + 40, (H - panelH) / 2);
    const panel = new PIXI.Graphics();
    panel.beginFill(C.paper).drawRoundedRect(48, py, 654, panelH, 16).endFill();
    this._settle.addChild(panel);

    const title = label(won ? '这一天是你改的' : '这句没改完', 36, won ? C.good : C.trap, '700');
    title.position.set(375, py + 52);
    this._settle.addChild(title);

    const why = label(
      words.length > 0 ? `本局靠 ${words.join(' · ')}` : '一对都没对上',
      22,
      C.ink,
      '600',
    );
    why.position.set(375, py + 104);
    this._settle.addChild(why);

    const sub = label(
      won ? `改了 ${words.length} 句通知` : (this._state.lastAttempt?.line || '换顺序，或换两个字'),
      20,
      C.inkSoft,
    );
    sub.position.set(375, py + 142);
    this._settle.addChild(sub);

    this._state.history.forEach((h, i) => {
      const row = label(`${h.from} → ${h.to}`, 20, C.ink);
      row.position.set(375, py + 188 + i * 36);
      this._settle.addChild(row);
    });

    const btn = new PIXI.Container();
    const bg = new PIXI.Graphics();
    bg.beginFill(C.accent).drawRoundedRect(-140, -36, 280, 72, 12).endFill();
    btn.addChild(bg);
    btn.addChild(label('再来一天', 26, C.paper, '700'));
    btn.position.set(375, py + panelH - 56);
    btn.eventMode = 'static';
    bindPointerTap(btn, () => {
      console.log('[pair] run_end', {
        cleared: won,
        words,
        misses: 4 - this._state.missesLeft,
      });
      this.onEnter();
    });
    this._settle.addChild(btn);
    Game.syncFrameToScreen();
  }
}
