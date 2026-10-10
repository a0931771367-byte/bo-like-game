/**
 * Procedural sound effects and a light background loop, synthesised with WebAudio.
 * No audio files needed; everything is generated from oscillators and noise.
 */
export type SfxName =
  | 'swing' | 'hit' | 'hitHeavy' | 'crit' | 'jump' | 'land' | 'dodge' | 'hurt' | 'guard' | 'cast' | 'skill'
  | 'fire' | 'ice' | 'thunder' | 'heal' | 'buff' | 'arrow' | 'potion' | 'gold' | 'levelup' | 'enemyDie' | 'bossDie'
  | 'select' | 'text' | 'clear' | 'lose' | 'bossAppear';

class Sfx {
  private ctx?: AudioContext;
  private master?: GainNode;
  private musicGain?: GainNode;
  private noise?: AudioBuffer;
  private musicTimer?: number;
  private musicStep = 0;
  private musicNextTime = 0;
  private lastPlay = new Map<SfxName, number>();
  muted = false;
  musicOn = true;

  /** Must be called from a user gesture at least once. */
  unlock() {
    if (!this.ctx) {
      try {
        const AC = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
        this.ctx = new AC();
        this.master = this.ctx.createGain();
        this.master.gain.value = 0.5;
        this.master.connect(this.ctx.destination);
        this.musicGain = this.ctx.createGain();
        this.musicGain.gain.value = 0.16;
        this.musicGain.connect(this.master);
        const len = this.ctx.sampleRate;
        this.noise = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
        const d = this.noise.getChannelData(0);
        for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
        try { localStorage.getItem('rbo-like-mute') === '1' && (this.muted = true); } catch { /* ignore */ }
      } catch { return; }
    }
    if (this.ctx.state === 'suspended') void this.ctx.resume();
  }

  toggleMute() {
    this.muted = !this.muted;
    try { localStorage.setItem('rbo-like-mute', this.muted ? '1' : '0'); } catch { /* ignore */ }
    if (this.master) this.master.gain.value = this.muted ? 0 : 0.5;
    return this.muted;
  }

  play(name: SfxName, volume = 1) {
    if (!this.ctx || !this.master || this.muted) return;
    const now = performance.now();
    if (now - (this.lastPlay.get(name) ?? 0) < 35) return; // avoid stacking identical sounds in one frame
    this.lastPlay.set(name, now);
    const t = this.ctx.currentTime;
    const g = this.ctx.createGain();
    g.gain.value = volume;
    g.connect(this.master);
    switch (name) {
      case 'swing': this.noiseBurst(g, t, 0.12, 1800, 0.35, 'bandpass'); this.tone(g, t, 'triangle', 420, 180, 0.08, 0.15); break;
      case 'hit': this.noiseBurst(g, t, 0.09, 900, 0.5, 'lowpass'); this.tone(g, t, 'square', 220, 90, 0.07, 0.3); break;
      case 'hitHeavy': this.noiseBurst(g, t, 0.2, 500, 0.8, 'lowpass'); this.tone(g, t, 'square', 140, 50, 0.16, 0.5); this.tone(g, t, 'sine', 60, 30, 0.22, 0.6); break;
      case 'crit': this.noiseBurst(g, t, 0.16, 1400, 0.6, 'bandpass'); this.tone(g, t, 'sawtooth', 880, 220, 0.14, 0.3); this.tone(g, t + 0.02, 'square', 1320, 660, 0.1, 0.15); break;
      case 'jump': this.tone(g, t, 'square', 260, 620, 0.14, 0.18); break;
      case 'land': this.noiseBurst(g, t, 0.08, 400, 0.5, 'lowpass'); break;
      case 'dodge': this.noiseBurst(g, t, 0.18, 2400, 0.3, 'highpass'); this.tone(g, t, 'sine', 700, 300, 0.12, 0.1); break;
      case 'hurt': this.tone(g, t, 'sawtooth', 300, 80, 0.18, 0.3); this.noiseBurst(g, t, 0.1, 700, 0.4, 'lowpass'); break;
      case 'guard': this.tone(g, t, 'triangle', 1500, 900, 0.08, 0.3); this.noiseBurst(g, t, 0.05, 3000, 0.3, 'highpass'); break;
      case 'cast': this.tone(g, t, 'sine', 440, 880, 0.25, 0.15); this.tone(g, t + 0.05, 'sine', 660, 1320, 0.25, 0.1); break;
      case 'skill': this.tone(g, t, 'sawtooth', 200, 600, 0.18, 0.25); this.noiseBurst(g, t, 0.15, 1200, 0.4, 'bandpass'); break;
      case 'fire': this.noiseBurst(g, t, 0.3, 600, 0.6, 'lowpass'); this.tone(g, t, 'sawtooth', 180, 60, 0.25, 0.25); break;
      case 'ice': this.tone(g, t, 'sine', 1800, 2600, 0.2, 0.15); this.tone(g, t + 0.04, 'triangle', 1200, 2200, 0.2, 0.12); this.noiseBurst(g, t, 0.15, 5000, 0.2, 'highpass'); break;
      case 'thunder': this.noiseBurst(g, t, 0.35, 2500, 0.9, 'lowpass'); this.tone(g, t, 'square', 90, 40, 0.3, 0.4); break;
      case 'heal': [523, 659, 784, 1047].forEach((f, i) => this.tone(g, t + i * 0.07, 'sine', f, f, 0.25, 0.18)); break;
      case 'buff': [392, 523, 659].forEach((f, i) => this.tone(g, t + i * 0.06, 'triangle', f, f * 1.01, 0.3, 0.18)); break;
      case 'arrow': this.noiseBurst(g, t, 0.1, 3000, 0.35, 'highpass'); this.tone(g, t, 'triangle', 900, 400, 0.06, 0.12); break;
      case 'potion': this.tone(g, t, 'sine', 600, 900, 0.12, 0.2); this.tone(g, t + 0.1, 'sine', 900, 1200, 0.12, 0.2); break;
      case 'gold': this.tone(g, t, 'square', 1568, 1568, 0.05, 0.12); this.tone(g, t + 0.06, 'square', 2093, 2093, 0.12, 0.12); break;
      case 'levelup': [523, 659, 784, 1047, 1319].forEach((f, i) => this.tone(g, t + i * 0.09, 'square', f, f, 0.22, 0.14)); break;
      case 'enemyDie': this.tone(g, t, 'square', 500, 120, 0.22, 0.2); this.noiseBurst(g, t, 0.15, 800, 0.4, 'lowpass'); break;
      case 'bossDie': this.noiseBurst(g, t, 0.9, 900, 0.9, 'lowpass'); this.tone(g, t, 'sawtooth', 200, 30, 0.9, 0.35); [262, 330, 392, 523].forEach((f, i) => this.tone(g, t + 0.5 + i * 0.1, 'square', f, f, 0.3, 0.12)); break;
      case 'bossAppear': this.tone(g, t, 'sawtooth', 80, 40, 0.8, 0.4); this.noiseBurst(g, t, 0.5, 300, 0.5, 'lowpass'); break;
      case 'select': this.tone(g, t, 'square', 880, 1320, 0.07, 0.12); break;
      case 'text': this.tone(g, t, 'square', 1200, 1200, 0.025, 0.05); break;
      case 'clear': [523, 659, 784, 1047, 784, 1047, 1319].forEach((f, i) => this.tone(g, t + i * 0.12, 'square', f, f, 0.25, 0.14)); break;
      case 'lose': [440, 415, 392, 370].forEach((f, i) => this.tone(g, t + i * 0.25, 'sawtooth', f, f * 0.98, 0.3, 0.14)); break;
    }
  }

  private tone(out: AudioNode, t: number, type: OscillatorType, f0: number, f1: number, dur: number, vol: number) {
    if (!this.ctx) return;
    const o = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    o.type = type;
    o.frequency.setValueAtTime(f0, t);
    o.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t + dur);
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(vol, t + 0.008);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g).connect(out);
    o.start(t);
    o.stop(t + dur + 0.02);
  }

  private noiseBurst(out: AudioNode, t: number, dur: number, freq: number, vol: number, type: BiquadFilterType) {
    if (!this.ctx || !this.noise) return;
    const s = this.ctx.createBufferSource();
    s.buffer = this.noise;
    const f = this.ctx.createBiquadFilter();
    f.type = type;
    f.frequency.value = freq;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    s.connect(f).connect(g).connect(out);
    s.start(t);
    s.stop(t + dur + 0.02);
  }

  // ---------- background loop ----------

  /** Starts a gentle looping tune. `mood` picks the scale / tempo. */
  startMusic(mood: 'field' | 'night' | 'boss' | 'title' = 'field') {
    this.stopMusic();
    if (!this.ctx || !this.musicGain || !this.musicOn) return;
    const scales: Record<string, number[]> = {
      title: [0, 2, 4, 7, 9, 12, 14, 16],
      field: [0, 2, 4, 7, 9, 12, 14],
      night: [0, 3, 5, 7, 10, 12, 15],
      boss: [0, 1, 3, 6, 7, 8, 11, 12],
    };
    const root = mood === 'boss' ? 55 : mood === 'night' ? 65.4 : 73.4;
    const step = mood === 'boss' ? 0.17 : mood === 'title' ? 0.3 : 0.24;
    const scale = scales[mood];
    const seq: number[] = [];
    let n = 0;
    for (let i = 0; i < 32; i++) {
      n = Math.max(0, Math.min(scale.length - 1, n + (Math.random() < 0.5 ? 1 : -1) * (Math.random() < 0.7 ? 1 : 2)));
      seq.push(i % 8 === 0 ? 0 : n);
    }
    this.musicStep = 0;
    this.musicNextTime = this.ctx.currentTime + 0.1;
    const tick = () => {
      if (!this.ctx || !this.musicGain) return;
      while (this.musicNextTime < this.ctx.currentTime + 0.4) {
        const i = this.musicStep % seq.length;
        const t = this.musicNextTime;
        const semis = scale[seq[i]];
        const f = root * 4 * Math.pow(2, semis / 12);
        this.tone(this.musicGain, t, mood === 'boss' ? 'sawtooth' : 'triangle', f, f, step * 0.9, 0.5);
        if (i % 4 === 0) this.tone(this.musicGain, t, 'sine', root * (i % 16 === 8 ? 1.5 : 1), root, step * 3.5, 0.7);
        if (mood === 'boss' && i % 2 === 0) this.noiseBurst(this.musicGain, t, 0.06, 300, 0.25, 'lowpass');
        this.musicNextTime += step;
        this.musicStep++;
      }
    };
    tick();
    this.musicTimer = window.setInterval(tick, 150);
  }

  stopMusic() {
    if (this.musicTimer !== undefined) { clearInterval(this.musicTimer); this.musicTimer = undefined; }
  }

  toggleMusic() {
    this.musicOn = !this.musicOn;
    if (!this.musicOn) this.stopMusic();
    return this.musicOn;
  }
}

export const sfx = new Sfx();
