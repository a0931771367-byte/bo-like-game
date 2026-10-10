export type Action = 'attack' | 'jump' | 'skill0' | 'skill1' | 'skill2' | 'skill3' | 'pot0' | 'pot1' | 'menu';

const BUFFER_MS = 220;

/** Unified input: keyboard and touch both write here, gameplay only reads here. */
class Controls {
  kbX = 0;
  kbY = 0;
  kbGuard = false;
  touchX = 0;
  touchY = 0;
  touchGuard = false;
  private presses = new Map<Action, number>();

  get moveX() { return this.kbX !== 0 ? this.kbX : this.touchX; }
  get moveY() { return this.kbY !== 0 ? this.kbY : this.touchY; }
  get guard() { return this.kbGuard || this.touchGuard; }

  press(a: Action) { this.presses.set(a, performance.now()); }

  /** Returns true once per press, if pressed within the buffer window. */
  consume(a: Action) {
    const t = this.presses.get(a);
    if (t === undefined) return false;
    this.presses.delete(a);
    return performance.now() - t <= BUFFER_MS;
  }

  peek(a: Action) {
    const t = this.presses.get(a);
    return t !== undefined && performance.now() - t <= BUFFER_MS;
  }

  reset() {
    this.presses.clear();
    this.kbX = this.kbY = this.touchX = this.touchY = 0;
    this.kbGuard = this.touchGuard = false;
  }
}

export const controls = new Controls();
