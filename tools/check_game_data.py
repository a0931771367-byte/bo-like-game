#!/usr/bin/env python3
"""Data integrity check for the Unity build's game data (unity/Assets/Resources/Data/*.json).

Fails when a table points at another table's entry that does not exist: a room spawning an unknown
enemy, a stage listing a missing room, a chapter unlocked by an unknown boss, an unlock for an unknown
class. These are the bugs that otherwise surface as an empty room or a run that cannot start.

Warnings are gaps that do not break the game but show as unfinished content: art, icons or music
not delivered yet, a boss with no lines. They are expected while content is in production, so they
only fail the check with --strict, which the release candidate must pass.

  python3 tools/check_game_data.py            # errors fail, warnings are printed
  python3 tools/check_game_data.py --strict   # warnings fail too (use for the release candidate)
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, 'unity', 'Assets', 'Resources')
DATA = os.path.join(RES, 'Data')

errors, warnings = [], []


def load(name):
    with open(os.path.join(DATA, name + '.json'), encoding='utf-8') as f:
        return json.load(f)


def res_exists(rel):
    """A Resources.Load path (no extension) that has a file behind it."""
    d, base = os.path.split(os.path.join(RES, rel))
    return os.path.isdir(d) and any(os.path.splitext(f)[0] == base and not f.endswith('.meta') for f in os.listdir(d))


def need(cond, msg):
    if not cond:
        errors.append(msg)


def want(cond, msg):
    if not cond:
        warnings.append(msg)


def check():
    classes = {c['id']: c for c in load('classes')}
    enemies = {e['id']: e for e in load('enemies')}
    perks = load('perks')
    rooms_t = load('rooms')
    rooms = {r['id']: r for r in rooms_t['rooms']}
    meta = load('meta')
    strings = load('strings')
    anims = {a['classId'] for a in load('anims')}

    # classes: animation sets, skill icons, side sprite
    for cid, c in classes.items():
        want(res_exists(f'Sprites/{cid}_side'), f'職業 {cid}：缺 Sprites/{cid}_side')
        if c.get('playable'):
            need(cid in anims, f'職業 {cid}：anims.json 沒有動畫設定')
        for i, s in enumerate(c['skills']):
            want(res_exists(f'Icons/skill_{cid}_{i}'), f'職業 {cid} 技能 {s["id"]}：缺圖示 Icons/skill_{cid}_{i}')
            want(s.get('name') and s.get('desc'), f'職業 {cid} 技能 {s["id"]}：缺名稱或說明')

    # enemies: sprite, bosses have lines
    for eid, e in enemies.items():
        sid = e.get('sprite') or eid
        want(res_exists(f'Sprites/{sid}_side'), f'敵人 {eid}：缺 Sprites/{sid}_side')
        if e.get('boss'):
            want(eid in strings.get('bossLines', {}), f'Boss {eid}：strings.json 沒有登場 / 死亡台詞')

    # rooms: enemies, backgrounds, wave sets
    for rid, r in rooms.items():
        want(res_exists('Bg/' + r['bg'].replace('bg_', '')), f'房間 {rid}：缺背景 Bg/{r["bg"].replace("bg_", "")}')
        need(r.get('waveSets'), f'房間 {rid}：沒有任何波次組合')
        for wi, ws in enumerate(r.get('waveSets') or []):
            need(ws, f'房間 {rid} 波次組合 {wi}：是空的')
            for wave in ws:
                for sp in wave['spawns']:
                    need(sp['enemy'] in enemies, f'房間 {rid}：生成不存在的敵人 {sp["enemy"]}')
        if r['kind'] == 'boss':
            spawned = {sp['enemy'] for ws in r.get('waveSets') or [] for w in ws for sp in w['spawns']}
            need(any(enemies.get(x, {}).get('boss') for x in spawned), f'Boss 房 {rid}：沒有生成任何 Boss')

    used = set()
    chapters_path = os.path.join(DATA, 'chapters.json')
    if os.path.exists(chapters_path):
        used |= check_chapters(load('chapters'), rooms, enemies)

    # scenes (pre-chapters format): pools and bosses exist
    scenes = {s['id']: s for s in rooms_t.get('scenes', [])}
    for sid in rooms_t.get('sceneOrder', []):
        need(sid in scenes, f'場景順序列了不存在的場景 {sid}')
    for sid, s in scenes.items():
        need(len(s['pool']) >= meta['rules']['combatRooms'],
             f'場景 {sid}：房池只有 {len(s["pool"])} 間，少於一局需要的 {meta["rules"]["combatRooms"]} 間')
        for rid in s['pool']:
            need(rid in rooms and rooms[rid]['kind'] == 'combat', f'場景 {sid}：房池列了不存在或非戰鬥房 {rid}')
            used.add(rid)
        need(s['boss'] in rooms and rooms[s['boss']]['kind'] == 'boss', f'場景 {sid}：Boss 房 {s["boss"]} 不存在')
        used.add(s['boss'])
    for rid in rooms:
        want(rid in used, f'房間 {rid}：沒有被任何關卡使用')

    # perks: icons, text, class restrictions
    ids = set()
    for p in perks:
        need(p['id'] not in ids, f'強化 {p["id"]}：id 重複')
        ids.add(p['id'])
        want(res_exists(f'Icons/perk_{p["id"]}'), f'強化 {p["id"]}：缺圖示 Icons/perk_{p["id"]}')
        need(p.get('name') and p.get('desc'), f'強化 {p["id"]}：缺名稱或說明')
        for c in p.get('classes') or []:
            need(c in classes, f'強化 {p["id"]}：限定了不存在的職業 {c}')
        need(p.get('effects'), f'強化 {p["id"]}：沒有任何效果')

    # unlocks and starting classes
    for u in meta['unlocks']:
        if u['kind'] == 'class':
            need(u['value'] in classes, f'解鎖 {u["id"]}：職業 {u["value"]} 不存在')
            need(classes.get(u['value'], {}).get('playable'), f'解鎖 {u["id"]}：職業 {u["value"]} 不能玩')
        need(u.get('cost', 0) > 0, f'解鎖 {u["id"]}：價格必須大於 0')
    for c in meta.get('startingClasses', []):
        need(c in classes and classes[c].get('playable'), f'起始職業 {c} 不存在或不能玩')

    # strings: placeholders like {name} stay well-formed
    for k, v in strings['strings'].items():
        want(v.strip(), f'文字 {k}：是空字串')
        need(v.count('{') == v.count('}'), f'文字 {k}：大括號不成對')
        want(not re.search(r'TODO|TBD|待補|XXX', v), f'文字 {k}：還有佔位字樣')

    # story portraits
    for st in load('story'):
        for part in ('before', 'after'):
            for line in st.get(part) or []:
                if line.get('portrait'):
                    want(res_exists(f'Sprites/{line["portrait"]}'), f'劇情 {st["stageId"]}：缺立繪 Sprites/{line["portrait"]}')
                want(line.get('text', '').strip(), f'劇情 {st["stageId"]}：有空白台詞')


def music_keys():
    """Logical music names AudioManager knows (its `music` table)."""
    path = os.path.join(ROOT, 'unity', 'Assets', 'Scripts', 'Runtime', 'Game', 'AudioManager.cs')
    with open(path, encoding='utf-8') as f:
        return set(re.findall(r'\["([\w-]+)"\]\s*=\s*\("bgm_', f.read()))


def check_chapters(t, rooms, enemies):
    """chapters.json: chapter -> stages -> room pools. Returns the rooms the stages use."""
    chapters = {c['id']: c for c in t['chapters']}
    stages = {s['id']: s for s in t['stages']}
    music = music_keys()
    used = set()
    for cid, c in chapters.items():
        want(res_exists('Bg/' + c['bg'].replace('bg_', '')), f'章節 {cid}：缺背景 Bg/{c["bg"].replace("bg_", "")}')
        want(c.get('stages'), f'章節 {cid}：還沒有任何關卡')
        for sid in c.get('stages') or []:
            need(sid in stages, f'章節 {cid}：列了不存在的關卡 {sid}')
        boss = (c.get('unlock') or {}).get('bossKill')
        if boss:
            need(enemies.get(boss, {}).get('boss'), f'章節 {cid}：解鎖條件的 Boss {boss} 不存在或不是 Boss')
        for key in ('music', 'bossTrack'):
            if c.get(key):
                want(c[key] in music, f'章節 {cid}：{key} 「{c[key]}」不在 AudioManager 曲目表')
        if c.get('ambience'):
            want(res_exists('Audio/amb/' + c['ambience']), f'章節 {cid}：缺環境音 Audio/amb/{c["ambience"]}')
    for sid, s in stages.items():
        need(s['chapterId'] in chapters, f'關卡 {sid}：章節 {s["chapterId"]} 不存在')
        need(sid in (chapters.get(s['chapterId'], {}).get('stages') or []), f'關卡 {sid}：沒有列在所屬章節的關卡清單')
        need(len(s['pool']) >= s['combatRooms'], f'關卡 {sid}：房池 {len(s["pool"])} 間，少於一局需要的 {s["combatRooms"]} 間')
        for rid in s['pool']:
            r = rooms.get(rid)
            need(r and r['kind'] == 'combat', f'關卡 {sid}：房池列了不存在或非戰鬥房 {rid}')
            if r:
                need(r.get('scene') == s['chapterId'], f'關卡 {sid}：房間 {rid} 屬於 {r.get("scene")}，不是 {s["chapterId"]}')
            used.add(rid)
        fin = s.get('finale') or {}
        if fin.get('room'):
            need(rooms.get(fin['room'], {}).get('kind') == 'boss', f'關卡 {sid}：結尾房 {fin["room"]} 不存在或不是 Boss 房')
            used.add(fin['room'])
        if fin.get('enemy'):
            need(enemies.get(fin['enemy'], {}).get('boss'), f'關卡 {sid}：結尾 Boss {fin["enemy"]} 不存在或不是 Boss')
        sc = s.get('scaling') or {}
        for k in ('hp', 'atk'):
            if sc.get(k):
                need(len(sc[k]) >= s['combatRooms'], f'關卡 {sid}：scaling.{k} 只有 {len(sc[k])} 格，少於 {s["combatRooms"]} 房')
    return used


if __name__ == '__main__':
    check()
    for w in warnings:
        print('警告', w)
    for e in errors:
        print('錯誤', e)
    strict = '--strict' in sys.argv
    if errors or (strict and warnings):
        print(f'\n資料檢查失敗：{len(errors)} 個錯誤、{len(warnings)} 個警告')
        sys.exit(1)
    print(f'資料檢查通過（{len(warnings)} 個警告）')
