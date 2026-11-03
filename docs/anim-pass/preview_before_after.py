#!/usr/bin/env python3
"""Renders a before/after preview of FighterView's player animation math on the real sprite frames.

"before" = FighterView as of PR #23 (80 ms frames, no cross-fade, k=0.35 procedural, instant state changes,
bob applied downward, fixed shadow). "after" = the animation pass in this branch (AnimCurves springs, frame
cross-fade, speed-tied gait, footsteps, landing/hit impulses, hit-stop shiver, shadow by lift, ground shade).
Both run the same scripted state sequence at 60 fps; the simulation clock, hit-stop and frame choice follow
World.Step / FighterView exactly. Usage: preview.py <repo> <out_dir>
"""
import math, os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter

REPO, OUT = sys.argv[1], sys.argv[2]
ANIM = os.path.join(REPO, 'unity/Assets/Resources/Sprites/anim')
FONT = '/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc'
PLAYER_H = 132.0
PANEL_W, PANEL_H, GROUND_Y = 300, 240, 205
FPS = 60

# ---------------- AnimCurves port ----------------
class Spring:
    def __init__(self, v): self.value, self.velocity = v, 0.0
    def step(self, target, dt, hz=14.0, damping=0.75):
        if dt <= 0: return
        w = 2 * math.pi * hz
        n = max(1, math.ceil(dt / 0.004)); h = dt / n
        for _ in range(n):
            a = w * w * (target - self.value) - 2 * damping * w * self.velocity
            self.velocity += a * h; self.value += self.velocity * h
    def snap(self, v): self.value, self.velocity = v, 0.0

def gait_lift(sp):
    u = sp - math.floor(sp)
    p = u / 0.45 if u < 0.45 else 1 - (u - 0.45) / 0.55
    return math.sin(p * math.pi / 2)
def cross_fade(k): k = min(1, max(0, k)); return k * k * (3 - 2 * k)
def ease_out_quad(k): k = min(1, max(0, k)); return 1 - (1 - k) ** 2
def delta_angle(a, b):
    d = math.fmod(b - a, 360)
    if d > 180: d -= 360
    if d <= -180: d += 360
    return d
clamp = lambda v, a, b: max(a, min(b, v))

# ---------------- sprites ----------------
def load(cls, label, n): return [Image.open(f'{ANIM}/{cls}_{label}_{i}.png').convert('RGBA') for i in range(n)]
CLASSES = {
    'swordsman': dict(idle=4, walk=8, attack=4, combo=(90, 90, 160), move=240, accent=0xd8e0e8),
    'archer': dict(idle=4, walk=8, attack=4, combo=(120, 60, 200), move=260, accent=0xe6c05a, roll=320),
}

def roll(u, pose, h):
    crouch_end, tumble_end = 0.14, 0.82
    if u < crouch_end:
        c = u / crouch_end; pose['sx'] = 1 + .10 * c; pose['sy'] = 1 - .20 * c; pose['lean'] = 12 * c
    elif u < tumble_end:
        r = (u - crouch_end) / (tumble_end - crouch_end); e = .5 - .5 * math.cos(r * math.pi)
        pose['lean'] = 12 + 348 * e; tuck = math.sin(r * math.pi)
        pose['sx'] = 1.06 - .10 * tuck; pose['sy'] = .80 - .14 * tuck; pose['bob'] = math.sin(r * math.pi) * 26
        pose['pivot'] = h * pose['sy'] * .5
    else:
        l = (u - tumble_end) / (1 - tumble_end); s = math.sin(l * math.pi)
        pose['sx'] = 1 + .14 * s; pose['sy'] = 1 - .16 * s; pose['lean'] = 360 + 6 * (1 - l)

class View:
    def __init__(self, cls, new):
        self.cls, self.d, self.new = cls, CLASSES[cls], new
        self.fr = {l: load(cls, l, self.d[l]) for l in ('idle', 'walk', 'attack')}
        self.canvas_h = self.fr['idle'][0].height
        self.scale = PLAYER_H * 1.08 / self.canvas_h
        self.leanS, self.sxS, self.syS = Spring(0), Spring(1), Spring(1)
        self.walk_phase = self.idle_phase = 0.0; self.last_walk = -1; self.kicked = False
        self.prev_state = 'idle'; self.prev_z = 0.0; self.prev_vz = 0.0
        self.motes, self.ghosts, self.last_ghost = [], [], -1e9
        self.scroll = 0.0

    def frame_and_pose(self, st, t, now, dt, vx, z, vz, in_hitstop):
        d = self.d; a = self.fr; new = self.new
        pose = dict(lean=0.0, sx=1.0, sy=1.0, bob=0.0, pivot=0.0, alpha=1.0)
        nxt, blend, snap = None, 0.0, False
        changed = st != self.prev_state
        k = 1.0 if new else 0.35
        breath = math.sin(now / 420)
        if new and changed:
            self.kicked = False
            if st == 'hurt': self.leanS.velocity -= 260; self.sxS.velocity -= 1.6
            if st == 'idle' and self.prev_state == 'walk': self.syS.velocity -= 1.0; self.leanS.velocity -= 60
        if st == 'walk':
            if new:
                if changed: self.walk_phase = 0; self.last_walk = -1
                sk = clamp(abs(vx) / d['move'], .5, 1.4)
                self.walk_phase += dt * 1000 / 75 * sk
                fi = int(self.walk_phase) % d['walk']; frac = self.walk_phase - math.floor(self.walk_phase)
                spr = a['walk'][fi]; nxt = a['walk'][(fi + 1) % d['walk']]; blend = cross_fade(frac)
                if fi != self.last_walk:
                    if self.last_walk >= 0 and fi in (0, d['walk'] // 2) and z <= 0:
                        self.motes += [dict(x=-6 - 8 * r, y=0, t=0, dur=.2 + .06 * r, r=3 + 2 * r, dx=-(10 + 10 * r), dy=6 + 8 * r) for r in (0.2, 0.8)]
                    self.last_walk = fi
                lift = gait_lift(self.walk_phase / max(1, d['walk'] // 2))
                pose.update(bob=lift * 6, sy=.975 + .045 * lift, sx=1.015 - .03 * lift,
                            lean=5 + 3.5 * sk + math.sin(self.walk_phase / 4 * 2 * math.pi) * 1.2)
            else:
                spr = a['walk'][int(t / 80) % d['walk']]
                ph = t / 75
                pose.update(bob=-abs(math.sin(ph)) * 6 * k, sy=1 + math.sin(ph) * .04 * k, sx=1 - math.sin(ph) * .03 * k, lean=(7 + math.sin(ph * 2) * 2.5) * k)
        elif st == 'attack':
            s, ac, rc = d['combo']
            i = 0 if t < s else 1 if t < s + ac else 2 if t < s + ac + rc * .45 else 3
            spr = a['attack'][min(i, d['attack'] - 1)]
            if new:
                if t < s:
                    u = ease_out_quad(t / s); pose.update(sx=1 - .08 * u, sy=1 + .04 * u, lean=-7 * u)
                elif t < s + ac:
                    if not self.kicked: self.kicked = True; self.sxS.velocity += 3.2; self.syS.velocity -= 1.2; self.leanS.velocity += 320
                    u = (t - s) / ac; pose.update(sx=1.12 - .06 * u, sy=.96, lean=9)
                else: pose.update(sx=1.02, lean=5)
            else:
                if t < s: pose.update(sx=1 - .1 * k * (1 - t / s), lean=-8 * k)
                elif t < s + ac: pose.update(sx=1 + .15 * k, sy=1 - .05 * k, lean=10 * k)
                else: pose.update(sx=1 + .05 * k, lean=6 * k)
        elif st == 'hurt':
            spr = a['idle'][0]
            if new: pose.update(lean=-12, sx=.94, sy=1.03)
            else: pose.update(lean=-14, sx=.92)
        elif st == 'dodge':
            spr = a['walk'][2]
            u = clamp(t / d['roll'], 0, 1)
            roll(u, pose, self.canvas_h * self.scale)
            if not new: pose['bob'] = -pose['bob']  # old code applied bob downward
            snap = True
        else:  # idle
            if new:
                self.idle_phase += dt * 1000 / 170
                fi = int(self.idle_phase) % d['idle']
                spr = a['idle'][fi]; nxt = a['idle'][(fi + 1) % d['idle']]; blend = cross_fade(self.idle_phase - math.floor(self.idle_phase))
                pose.update(sy=1 + breath * .022, sx=1 - breath * .011, lean=math.sin(now / 840) * 1.2)
            else:
                spr = a['idle'][int(now / 170) % d['idle']]
                pose.update(sy=1 + breath * .028 * k, sx=1 - breath * .014 * k, lean=math.sin(now / 840) * 1.5 * k)

        landed = self.prev_z > 0 and z <= 0
        if new:
            if landed and st != 'dodge':
                kk = clamp(-self.prev_vz / 900, .6, 1.6); self.syS.velocity -= 3.2 * kk; self.sxS.velocity += 2.2 * kk
            if snap: self.leanS.snap(pose['lean']); self.sxS.snap(pose['sx']); self.syS.snap(pose['sy'])
            else:
                self.leanS.value = pose['lean'] - delta_angle(self.leanS.value, pose['lean'])
                self.leanS.step(pose['lean'], dt, 13, .72); self.sxS.step(pose['sx'], dt, 15, .7); self.syS.step(pose['sy'], dt, 15, .7)
            L, SX, SY = self.leanS.value, clamp(self.sxS.value, .5, 1.8), clamp(self.syS.value, .4, 1.8)
        else:
            L, SX, SY = pose['lean'], pose['sx'], pose['sy']
        jitter = 0.0
        if new and in_hitstop and st == 'attack':
            jitter = 1.5 if self.frame_no % 2 == 0 else -1.5; SX *= 1.04; SY *= .97
        self.prev_state, self.prev_z, self.prev_vz = st, z, vz
        return spr, nxt, blend, L, SX, SY, pose['bob'], pose['pivot'], jitter

    def draw(self, spr, nxt, blend, L, SX, SY, bob, pivot, jitter, z, vx, dt_real, now, dodging):
        img = Image.new('RGBA', (PANEL_W, PANEL_H), (0, 0, 0, 0))
        dr = ImageDraw.Draw(img)
        # ground + scrolling marks so speed reads while the camera follows
        self.scroll = (self.scroll + vx * dt_real) % 60
        for gx in range(-60, PANEL_W + 60, 60):
            x = gx - self.scroll
            dr.line([(x, GROUND_Y + 1), (x + 22, GROUND_Y + 1)], fill=(60, 80, 70, 255), width=2)
        lift = max(0.0, z + bob) if self.new else max(0.0, z)
        ss = max(.45, 1 - lift / 400)
        sw, sh = 46 * 1.1 * ss * (1 + (SX - 1) * .5 if self.new else 1), max(12, 46 * .28) * ss
        sa = int(255 * (.3 * clamp(1 - lift / 300, .45, 1) if self.new else .28))
        shadow = Image.new('RGBA', (PANEL_W, PANEL_H), (0, 0, 0, 0))
        ImageDraw.Draw(shadow).ellipse([PANEL_W / 2 - sw / 2, GROUND_Y - sh / 2, PANEL_W / 2 + sw / 2, GROUND_Y + sh / 2], fill=(0, 0, 0, sa))
        img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(2)))
        # ghosts (roll afterimages, both versions)
        for g in self.ghosts:
            gi = g['img'].copy(); al = gi.getchannel('A').point(lambda v, f=.45 * (1 - g['t'] / .26): int(v * f)); gi.putalpha(al)
            img.alpha_composite(gi, (int(g['x']), 0))
        # body layer: feet at pivot point, then rotate about the view origin
        body = Image.new('RGBA', (PANEL_W, PANEL_H), (0, 0, 0, 0))
        W, H = spr.size; w, h = max(1, int(W * self.scale * SX)), max(1, int(H * self.scale * SY))
        feet_y = GROUND_Y - (z + bob)
        def place(s, alpha):
            im = s.resize((w, h), Image.LANCZOS)
            if self.new:  # ground shade: bottom 35% darkened up to 10%
                px = im.load()
                for yy in range(int(h * .65), h):
                    f = 1 - .10 * (1 - min(1, (h - yy) / (h * .35)))
                    for xx in range(w):
                        r, g, b, a = px[xx, yy]
                        if a: px[xx, yy] = (int(r * f), int(g * f), int(b * f), a)
            if alpha < 1: im.putalpha(im.getchannel('A').point(lambda v: int(v * alpha)))
            body.alpha_composite(im, (int(PANEL_W / 2 + jitter - w / 2), int(feet_y - h)))
        place(spr, 1.0)
        if nxt is not None and blend > .001 and nxt is not spr: place(nxt, blend)
        origin = (PANEL_W / 2, GROUND_Y - (z + bob + pivot))
        if abs(L) > .01: body = body.rotate(-L, resample=Image.BICUBIC, center=origin)
        if dodging and now - self.last_ghost > 40:
            self.last_ghost = now; self.ghosts.append(dict(img=body.copy(), x=0, t=0.0))
        img.alpha_composite(body)
        # footstep motes
        for m in self.motes:
            kk = m['t'] / m['dur']; e = 1 - (1 - kk) ** 3
            mx, my = PANEL_W / 2 + m['x'] + m['dx'] * e, GROUND_Y - m['dy'] * e
            rr = m['r'] * (1 + .6 * e)
            ImageDraw.Draw(img).ellipse([mx - rr, my - rr, mx + rr, my + rr], fill=(242, 234, 216, int(255 * .3 * (1 - e))))
        for m in self.motes: m['t'] += dt_real
        self.motes = [m for m in self.motes if m['t'] < m['dur']]
        for g in self.ghosts: g['t'] += dt_real; g['x'] -= vx * dt_real
        self.ghosts = [g for g in self.ghosts if g['t'] < .26]
        return img

def sequence(cls):
    d = CLASSES[cls]
    seq = [('idle', 700), ('walk', 1500), ('idle', 350), ('attack', sum(d['combo'])), ('idle', 300)]
    if 'roll' in d: seq += [('walk', 500), ('dodge', d['roll']), ('idle', 500)]
    seq += [('hurt', 300), ('idle', 600)]
    return seq

def render(cls):
    views = [View(cls, False), View(cls, True)]
    seq = sequence(cls); d = CLASSES[cls]
    font = ImageFont.truetype(FONT, 18); small = ImageFont.truetype(FONT, 13)
    frames = []
    now = 0.0; si = 0; st, dur = seq[0]; t = 0.0; hitstop_until = -1; frame_no = 0; z = vz = 0.0
    while si < len(seq):
        real = 1 / FPS
        in_hs = now < hitstop_until
        dt = real * (.08 if in_hs else 1)
        vx = d['move'] if st == 'walk' else (d['roll'] and 210 / (d['roll'] / 1000) * (1.2 * math.sin(math.pi * t / d['roll']) + .1)) if st == 'dodge' else 0
        if st == 'attack' and t < d['combo'][0] <= t + dt * 1000: hitstop_until = now + 55  # the hit lands
        out = Image.new('RGBA', (PANEL_W * 2 + 30, PANEL_H + 60), (24, 26, 34, 255))
        dr = ImageDraw.Draw(out)
        for i, v in enumerate(views):
            v.frame_no = frame_no
            res = v.frame_and_pose(st, t, now, dt, vx, z, vz, in_hs)
            img = v.draw(*res, z=z, vx=vx, dt_real=real, now=now, dodging=(st == 'dodge'))
            x0 = 10 + i * (PANEL_W + 10)
            dr.rectangle([x0, 40, x0 + PANEL_W, 40 + PANEL_H], fill=(38, 42, 54, 255))
            out.alpha_composite(img, (x0, 40))
            dr.text((x0 + 8, 10), ['修改前（PR #23）', '修改後（本次 PR）'][i], font=font, fill=(235, 235, 240, 255))
        label = {'idle': '待機', 'walk': '走路', 'attack': '攻擊', 'hurt': '受擊', 'dodge': '滾翻'}[st] + ('　打擊停頓' if in_hs else '')
        dr.text((12, PANEL_H + 46 - 8), f'{cls}　{label}', font=small, fill=(200, 200, 210, 255))
        cap = '60fps・同一套 FighterView 數學渲染'; tw = dr.textlength(cap, font=small)
        dr.text((PANEL_W * 2 + 30 - 12 - tw, PANEL_H + 46 - 8), cap, font=small, fill=(140, 140, 150, 255))
        frames.append(out.convert('RGB'))
        now += real * 1000; t += dt * 1000; frame_no += 1
        if t >= dur:
            si += 1
            if si < len(seq): st, dur = seq[si]; t = 0.0
    os.makedirs(OUT, exist_ok=True)
    for i, f in enumerate(frames): f.save(f'{OUT}/{cls}_{i:04d}.png')
    gif = [f.quantize(colors=128, method=Image.MEDIANCUT, dither=Image.FLOYDSTEINBERG) for f in frames[::2]]
    gif[0].save(f'{OUT}/{cls}_before_after.gif', save_all=True, append_images=gif[1:], duration=33, loop=0, optimize=True)
    print(cls, len(frames), 'frames')

for c in ('swordsman', 'archer'): render(c)
