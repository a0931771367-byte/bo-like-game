using System.Collections.Generic;
using BoLike.Core;
using BoLike.Data;
using BoLike.Sim;
using BoLike.Systems;
using BoLike.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace BoLike.Game
{
    /// <summary>
    /// Runs one Roguelite run: builds the world and views, steps the simulation, and drives the room flow
    /// (waves → clear → perk choice → next room → boss → result). Port of the scene half of GameScene.ts.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public string classId = "swordsman";
        public ulong seedOverride;

        World world;
        RunState run;
        SaveData save;
        GameCamera cam;
        Background background;
        Hud hud;
        TouchControls touch;
        FxSystem fx;
        Transform worldRoot;
        readonly Dictionary<Fighter, FighterView> views = new();
        FighterView playerView;

        // room flow
        RoomDef room;
        List<RoomWaveDef> waves;
        int waveIndex;
        int pendingSpawns;
        double roomEnteredAt, lastWaveStart;
        bool roomCleared, doorOpen, finished, transitioning;
        readonly RunLogEntry runLog = new();
        RoomLogEntry roomLog;
        /// <summary>Tutorial hints run in a player's first two runs only (growth curve v2 E6).</summary>
        bool hintsOn;
        Enemy boss;
        Canvas overlayCanvas;
        GameObject overlay;
        bool paused;

        public static GameController Launch(string classId, ulong seed = 0)
        {
            var go = new GameObject("Game");
            go.SetActive(false); // Awake runs on AddComponent for an active object; set the fields first
            var gc = go.AddComponent<GameController>();
            gc.classId = classId;
            gc.seedOverride = seed;
            go.SetActive(true);
            return gc;
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            save = SaveSystem.Current ?? SaveSystem.Load();
            run = RunState.New(classId, save, seedOverride == 0 ? null : seedOverride);
            save.lastSeedCode = run.SeedCode;
            world = new World { Rng = run.rng, Inv = run.inv, Mods = run.mods };
            worldRoot = new GameObject("World").transform;

            var cls = GameDb.Class(classId);
            var prog = ClassProgress.New(cls);
            world.Player = new Player(world, 200, (Units.GroundTopPx + Units.GroundBottomPx) / 2, cls, prog);
            // Growth curve v2 §1: the 3 starting stat points are spent at once and skills unlock by level.
            AllocateStats(world.Player, 1);
            prog.skillPoints = 0; // skill 1 is known from the start; every level up adds one point the player spends
            world.Player.Recompute(true);
            hintsOn = save.runsPlayed < 2;

            cam = GameCamera.Create(world);
            bool isTouch = TouchControls.IsTouchDevice;
            hud = Hud.Create(world, isTouch);
            hud.Stardust = () => save.stardust + run.stardustEarned;
            hud.OpenSkillPanel = ShowSkillPanel;
            if (isTouch) { touch = TouchControls.Create(world); touch.SkillLongPressed += ShowSkillPanel; }
            fx = new FxSystem(worldRoot, cam, hud, world) { views = views };
            world.Fx = fx;
            world.Sfx = AudioManager.Instance;
            overlayCanvas = UiKit.Canvas("Overlay", 30);
            overlayCanvas.transform.SetParent(transform, false);

            playerView = FighterView.Create(world.Player, worldRoot);
            views[world.Player] = playerView;

            world.EnemySpawned += e => views[e] = FighterView.Create(e, worldRoot);
            world.EnemyRemoved += e => { if (views.TryGetValue(e, out var v)) { Destroy(v.gameObject); views.Remove(e); } };
            world.ProjectileSpawned += p => ProjectileView.Create(p, worldRoot);
            world.WallSpawned += w => WallView.Create(w, worldRoot);
            world.EnemyKilled += OnEnemyKilled;
            world.PlayerDied += OnPlayerDead;
            world.PlayerHurt += () => Hint("hint.dodge");
            world.Toasted += hud.Toast;
            world.ComboChanged += hud.Combo;

            GameInput.Enable();
            run.startedAt = 0;
            AudioManager.Instance.PlayMusic("field");
            AudioManager.Instance.PlayAmbience("amb_ch1_grassland", 0.35f);
            EnterRoom(0);
        }

        void OnDestroy() => GameInput.Disable();

        // ---------- rooms ----------

        void EnterRoom(int index)
        {
            run.roomIndex = index;
            room = GameDb.Room(run.CurrentRoomId);
            waves = room.waveSets[System.Math.Min(run.CurrentWaveSet, room.waveSets.Count - 1)];
            waveIndex = 0;
            pendingSpawns = 0;
            roomCleared = doorOpen = transitioning = false;
            boss = null;
            roomEnteredAt = world.Now;
            roomLog = new RoomLogEntry { roomId = run.CurrentRoomId, waveSet = run.CurrentWaveSet };
            runLog.rooms.Add(roomLog);
            CrashLog.Context = $"{classId} seed {run.SeedCode} room {index + 1} {run.CurrentRoomId}/{run.CurrentWaveSet}";

            foreach (var e in world.Enemies) if (views.TryGetValue(e, out var v)) Destroy(v.gameObject);
            foreach (var e in world.Enemies) views.Remove(e);
            world.Enemies.Clear();
            world.ClearHazards();
            if (background != null) Destroy(background.gameObject);
            background = Background.Create(worldRoot, room.bg, room.bgGroundY, room.width, room.tint);
            world.StageWidth = room.width;
            world.LockRight = room.width;

            var p = world.Player;
            p.x = 200; p.y = (Units.GroundTopPx + Units.GroundBottomPx) / 2; p.z = 0; p.vx = p.vy = p.vz = 0; p.facing = 1;
            p.struck.Clear();
            if (p.state != FighterState.Dead) p.SetState(FighterState.Idle);
            cam.SnapTo(p.x);
            string label = run.InBossRoom ? room.name : $"第 {index + 1} 房  {room.name}";
            fx.Banner(label, 0xffffff, 48);
            hud.ShowGo(false);
            if (index == 0) world.Delay(700, () => Hint("hint.move"));
        }

        void SpawnWave()
        {
            var wave = waves[waveIndex];
            waveIndex++;
            lastWaveStart = world.Now;
            pendingSpawns += wave.spawns.Count;
            int slot = run.CurrentSlot;
            if (slot == 0 && waveIndex == 1) world.Delay(1800, () => Hint("hint.attack"));
            foreach (var s in wave.spawns)
            {
                var spawn = s;
                world.Delay(spawn.delay, () =>
                {
                    pendingSpawns--;
                    if (finished) return;
                    var def = GameDb.Enemy(spawn.enemy);
                    bool rooted = def.speed == 0;
                    float camX = world.CamX, viewW = world.ViewW;
                    float px = world.Player.x;
                    // Rooted enemies (plants) can't walk in, so they sprout inside the view instead.
                    float x = rooted
                        ? Mathf.Clamp(spawn.side == Side.Right ? camX + viewW - 160 : camX + 160, px - 500, px + 500)
                        : spawn.side == Side.Right ? camX + viewW + 60 : camX - 60;
                    x = Mathf.Clamp(x, -80, room.width + 80);
                    float y = run.rng.Between((int)Units.GroundTopPx + 20, (int)Units.GroundBottomPx - 20);
                    var scaling = run.Rules.roomScaling;
                    float hpMult = run.InBossRoom ? scaling.boss.hp : scaling.HpAt(slot);
                    float atkMult = run.InBossRoom ? scaling.boss.atk : scaling.AtkAt(slot);
                    if (run.InBossRoom && !def.boss) { hpMult = scaling.HpAt(scaling.hp.Count - 1); atkMult = scaling.AtkAt(scaling.atk.Count - 1); }
                    var e = world.SpawnEnemy(def, x, y, hpMult, atkMult);
                    if (rooted)
                    {
                        fx.Dust(x, y, 10, 1.4f);
                        fx.Ring(x, y, 90, def.color, y - 1);
                        e.attackReadyAt = world.Now + 900;
                    }
                    if (def.boss)
                    {
                        boss = e;
                        hud.ShowBoss(e);
                        fx.Banner($"{def.name} 出現！", 0xffb3c6, 56);
                        cam.Shake(0.3f, 0.01f);
                        world.Sfx.Play("bossAppear");
                        AudioManager.Instance.PlayMusic("boss", 0.3f);
                    }
                });
            }
        }

        void UpdateRoomFlow()
        {
            if (finished || roomCleared) return;
            int alive = world.AliveEnemies;
            bool waveActive = alive > 0 || pendingSpawns > 0;
            if (waveIndex < waves.Count)
            {
                bool start = waveIndex == 0
                    ? world.Now - roomEnteredAt >= 600
                    : !waveActive || world.Now - lastWaveStart >= waves[waveIndex].timeout;
                if (start) SpawnWave();
            }
            else if (run.InBossRoom ? boss != null && boss.dead : !waveActive)
            {
                RoomClear();
            }
        }

        void RoomClear()
        {
            roomCleared = true;
            var p = world.Player;
            MarkRoomCleared();
            float heal = run.Rules.roomClearHeal + run.mods.roomClearHeal;
            if (run.InBossRoom)
            {
                run.bossKilled = true;
                int reward = run.Rules.stardust.bossKill + run.Rules.stardust.runClear + run.Rules.stardust.runEnd;
                if (!save.bossCleared) { reward += run.Rules.stardust.firstClearBonus; save.bossCleared = true; }
                AddStardust(reward);
                fx.Banner("RUN CLEAR!", Colors.Crit, 72);
                AudioManager.Instance.StopMusic(1.5f);
                world.Sfx.Play("clear");
                finished = true;
                FinishRunLog("win");
                world.Delay(2200, () => ShowResult(true));
                return;
            }
            run.roomsCleared++;
            AddStardust(run.Rules.stardust.roomClear);
            int v = (int)StatFormulas.JsRound(p.maxHp * heal);
            p.hp = Mathf.Min(p.maxHp, p.hp + v);
            fx.Number(p.x, p.y - 130, $"+{v}", Colors.Heal, true);
            fx.Aura(p.x, p.y, Colors.Heal);
            fx.Banner("ROOM CLEAR", Colors.Crit, 56);
            world.Sfx.Play("clear");
            world.Delay(900, ShowPerkChoice);
        }

        void AddStardust(int n)
        {
            run.stardustEarned += n;
            save.stardust += n;
            save.gold = run.inv.gold;
            SaveSystem.Save();
        }

        void OpenDoor()
        {
            doorOpen = true;
            world.LockRight = room.width + 400;
            hud.ShowGo(true);
            Hint("hint.door");
        }

        void UpdateDoor()
        {
            if (!doorOpen || transitioning) return;
            if (world.Player.x >= room.width - 80)
            {
                transitioning = true;
                hud.ScreenFlash(1f, run.Rules.roomTransition / 1000f * 0.5f);
                world.Delay(run.Rules.roomTransition * 0.4f, () => EnterRoom(run.roomIndex + 1));
            }
        }

        // ---------- kills, death, result ----------

        void OnEnemyKilled(Enemy e)
        {
            var p = world.Player;
            // Gold is still tracked for the merchant room later, but the beta shows neither the number nor the HUD counter (v2 E7).
            int gold = (int)StatFormulas.JsRound(e.def.gold * (1 + p.PassiveValue(Passive.GoldFind)));
            run.inv.gold += gold;
            int exp = ExpFor(e);
            int gained = StatFormulas.AddExp(p.prog, exp);
            fx.Number(e.x, e.y - e.h - 60, $"+{exp} EXP", Colors.Gold);
            world.Sfx.Play(e.def.boss ? "bossDie" : "enemyDie");
            p.OnKilled(e);
            if (gained > 0) LevelUp(p, gained);
        }

        /// <summary>EXP scaled by room slot so levels keep coming at about one per room (growth curve v2 §1).</summary>
        int ExpFor(Enemy e)
        {
            var lv = run.Rules.levelUp;
            float mult = e.def.boss ? lv.bossExpMult
                : run.InBossRoom ? lv.ExpMultAt(lv.expScalingBySlot.Count - 1)
                : lv.ExpMultAt(run.CurrentSlot);
            return System.Math.Max(1, (int)StatFormulas.JsRound(e.def.exp * mult));
        }

        void LevelUp(Player p, int levels)
        {
            var rules = run.Rules.levelUp;
            AllocateStats(p, levels);
            // 罐子 10/30: skill points are the player's to spend (long-press a skill button), one per level.
            p.prog.skillPoints += levels;
            // Keep the HP ratio, then heal a fraction of the new max per level (not a full heal: v2 §1).
            p.Recompute(false);
            int heal = (int)StatFormulas.JsRound(p.maxHp * rules.heal * levels);
            p.hp = Mathf.Min(p.maxHp, p.hp + heal);
            p.glowUntil = world.Now + 1500;
            world.Sfx.Play("levelup");
            fx.Banner($"{GameDb.Str("levelup.banner")}　{GameDb.Str("levelup.point").Replace("{n}", levels.ToString())}", Colors.Crit, 56);
            if (heal > 0) fx.Number(p.x, p.y - 130, $"+{heal}", Colors.Heal, true);
            fx.Aura(p.x, p.y, Colors.Crit);
            fx.Ring(p.x, p.y, 160, Colors.Crit, p.y - 1);
            if (!skillPointHintShown) { skillPointHintShown = true; world.Delay(1400, () => hud.Toast(GameDb.Str("hint.skillPoint"), 3.5f)); }
        }

        /// <summary>Spends one level's worth of stat points per level: +1 to the class's two highest base stats, +1 VIT.</summary>
        static void AllocateStats(Player p, int levels)
        {
            var s = p.prog.stats;
            var b = p.cls.baseStats;
            var order = new List<(StatKey k, int v)> { (StatKey.Str, b.str), (StatKey.Agi, b.agi), (StatKey.Vit, b.vit), (StatKey.Int, b.@int), (StatKey.Dex, b.dex), (StatKey.Luk, b.luk) };
            order.Sort((a, c) => c.v.CompareTo(a.v));
            for (int i = 0; i < levels; i++)
            {
                s[order[0].k] += 1; s[order[1].k] += 1; s.vit += 1;
            }
            p.prog.statPoints = 0;
        }

        bool skillPointHintShown;

        /// <summary>Run level a skill slot needs before a point can be spent on it (<c>levelUp.skillUnlockLevels</c>).</summary>
        public static int SkillGate(int index)
        {
            var levels = GameDb.Rules.levelUp.skillUnlockLevels;
            if (levels == null || levels.Count == 0) return 1;
            return levels[System.Math.Min(index, levels.Count - 1)];
        }

        /// <summary>Points to learn (0→1) or raise a skill, from <c>levelUp.skillLearnCost / skillRaiseCost</c>.</summary>
        public static int SkillCost(Player p, SkillDef s) =>
            System.Math.Max(1, BaseSkillLv(p, s) == 0 ? GameDb.Rules.levelUp.skillLearnCost : GameDb.Rules.levelUp.skillRaiseCost);

        public static int BaseSkillLv(Player p, SkillDef s) => p.prog.skillLv.TryGetValue(s.id, out var l) ? l : 0;

        /// <summary>Why a point can't go into slot <paramref name="index"/> right now, or null when it can.</summary>
        public static string SkillUpBlocker(Player p, int index)
        {
            if (index >= p.cls.skills.Count) return "—";
            var s = p.cls.skills[index];
            if (p.prog.level < SkillGate(index)) return $"Lv{SkillGate(index)} 解鎖";
            int lv = BaseSkillLv(p, s);
            if (lv >= s.maxLv) return GameDb.Str("skill.max");
            if (p.prog.skillPoints < SkillCost(p, s)) return "沒有技能點";
            return null;
        }

        /// <summary>Spends one skill point on slot <paramref name="index"/>: learns it (0→1) or raises it. False when blocked.</summary>
        public static bool SpendSkillPoint(Player p, int index)
        {
            if (SkillUpBlocker(p, index) != null) return false;
            var s = p.cls.skills[index];
            p.prog.skillPoints -= SkillCost(p, s);
            p.prog.skillLv[s.id] = BaseSkillLv(p, s) + 1;
            p.Recompute(false);
            return true;
        }

        /// <summary>Shows a tutorial hint once per save, and only in the first two runs (v2 E6).</summary>
        void Hint(string key)
        {
            if (!hintsOn || finished || save.hintsSeen.Contains(key)) return;
            save.hintsSeen.Add(key);
            hud.Toast(GameDb.Str(key), 3.5f);
        }

        void OnPlayerDead()
        {
            if (finished) return;
            finished = true;
            AddStardust(run.Rules.stardust.runEnd); // every finished run pays a little, so the first runs always buy something (v2 §3)
            FinishRunLog("lose");
            fx.Banner("你倒下了…", 0xff8080, 64);
            AudioManager.Instance.StopMusic(1.0f);
            world.Sfx.Play("lose");
            world.Delay(1400, () => ShowResult(false));
        }

        // ---------- run log (local balance data for design) ----------

        void MarkRoomCleared()
        {
            if (roomLog == null) return;
            var p = world.Player;
            roomLog.clearMs = (int)(world.Now - roomEnteredAt);
            roomLog.hpAtClear = Mathf.RoundToInt(p.hp);
            roomLog.maxHp = Mathf.RoundToInt(p.maxHp);
            roomLog.potionsLeft = run.inv.red;
            roomLog.levelAtClear = p.prog.level;
        }

        bool runLogged;
        void FinishRunLog(string result)
        {
            if (runLogged) return;
            runLogged = true;
            runLog.build = BuildInfo.Number;
            runLog.classId = classId;
            runLog.seed = run.SeedCode;
            runLog.result = result;
            runLog.roomsCleared = run.roomsCleared;
            runLog.bossKilled = run.bossKilled;
            runLog.totalMs = (int)(world.Now - run.startedAt);
            runLog.stardust = run.stardustEarned;
            runLog.level = world.Player.prog.level;
            runLog.perks.Clear();
            foreach (var pk in run.perks) runLog.perks.Add(pk.id);
            RunLog.Append(runLog);
            save.runsPlayed++;
            save.bestRoom = Mathf.Max(save.bestRoom, run.roomsCleared + (run.bossKilled ? 1 : 0));
            SaveSystem.Save();
        }

        // ---------- overlays ----------

        void ClearOverlay()
        {
            if (overlay != null) Destroy(overlay);
            overlay = null;
            world.Paused = false;
            paused = false;
        }

        RectTransform NewOverlay(float dim = 0.7f)
        {
            ClearOverlay();
            var layer = UiKit.Layer(overlayCanvas, "Panel");
            overlay = layer.gameObject;
            UiKit.Dim(layer, dim);
            world.Paused = true;
            return layer;
        }

        void ShowPerkChoice()
        {
            var choices = run.DrawPerks(run.Rules.perkChoices, RunState.RareChanceBonus(save));
            if (choices.Count == 0) { OpenDoor(); return; }
            if (roomLog != null) { roomLog.offered.Clear(); foreach (var c in choices) roomLog.offered.Add(c.id); }
            var layer = NewOverlay(0.6f);
            UiKit.Text(layer, 0, 110, "選擇一個強化", 34, 0xfff4de, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            // Art department card frames: 360×520 originals with a 56 px 9-slice border; layout from beta-ui/README.md scaled to the card width.
            float cardW = 300, cardH = 300f * 520 / 360, gap = 30, k = cardW / 360f;
            float startX = -(choices.Count * cardW + (choices.Count - 1) * gap) / 2;
            for (int i = 0; i < choices.Count; i++)
            {
                var perk = choices[i];
                string rarityKey = perk.rarity == PerkRarity.Epic ? "epic" : perk.rarity == PerkRarity.Rare ? "rare" : "common";
                int frame = perk.rarity == PerkRarity.Epic ? 0x8a4fd1 : perk.rarity == PerkRarity.Rare ? 0x2f6fb5 : 0x5c6b7a;
                var frameSprite = Resources.Load<Sprite>("UI/card_" + rarityKey);
                Image card;
                if (frameSprite != null)
                {
                    card = UiKit.Sprite(layer, frameSprite, startX + i * (cardW + gap), 140, cardW, cardH, UiKit.TopCenter);
                    card.type = Image.Type.Sliced;
                    card.preserveAspect = false;
                }
                else
                {
                    card = UiKit.Rect(layer, startX + i * (cardW + gap), 140, cardW, cardH, frame, 0.95f, UiKit.TopCenter);
                    UiKit.Rect(card.transform, 6, 6, cardW - 12, cardH - 12, 0xf3ecdc, 0.95f);
                }
                var icon = Resources.Load<Sprite>("Icons/perk_" + perk.id);
                if (icon != null) UiKit.Sprite(card.transform, icon, 0, 40 * k, 220 * k, 220 * k, UiKit.TopCenter, UiKit.TopCenter);
                UiKit.Text(card.transform, 0, 280 * k, perk.name, (int)(40 * k), 0x2b2140, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true, 0);
                var desc = UiKit.Text(card.transform, 0, 340 * k, perk.desc, (int)(26 * k), 0x4a4060, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, false, 0);
                desc.rectTransform.sizeDelta = new Vector2(260 * k, 130 * k);
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                string rarity = perk.rarity == PerkRarity.Epic ? "史詩" : perk.rarity == PerkRarity.Rare ? "稀有" : "普通";
                string held = run.perkStacks.TryGetValue(perk.id, out var n) && n > 0 ? $"  已持有 ×{n}" : "";
                UiKit.Text(card.transform, 0, 478 * k, rarity + held, (int)(20 * k), 0x4a4060, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, false, 0);
                UiKit.Button(card.transform, 0, -52, 200, 48, $"選擇 ({i + 1})", 0xf28c28, () => PickPerk(perk), 20, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            }
            perkChoices = choices;
        }

        List<PerkDef> perkChoices;

        void PickPerk(PerkDef perk)
        {
            run.TakePerk(perk);
            if (roomLog != null) roomLog.perk = perk.id;
            world.Mods = run.mods;
            world.Player.Recompute();
            perkChoices = null;
            ClearOverlay();
            fx.Number(world.Player.x, world.Player.y - 140, perk.name, Colors.Buff, true);
            fx.Aura(world.Player.x, world.Player.y, Colors.Buff);
            world.Sfx.Play("buff");
            OpenDoor();
        }

        void ShowResult(bool win)
        {
            var layer = NewOverlay(0.75f);
            UiKit.Text(layer, 0, 110, win ? GameDb.Str("result.clear") : GameDb.Str("result.dead"), 48, win ? Colors.Crit : 0xff9a9a, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            if (!win) UiKit.Text(layer, 0, 168, GameDb.Str("result.deadSub"), 18, 0xcfd4e6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            string body = $"清除房間 {run.roomsCleared} / {run.Rules.combatRooms}" + (run.bossKilled ? "  ＋ Boss" : "") +
                          $"\n獲得星塵 ✦ +{run.stardustEarned}    累計 ✦ {save.stardust}" +
                          $"\n強化：{(run.perks.Count == 0 ? "無" : string.Join("、", run.perks.ConvertAll(p => p.name)))}" +
                          $"\n用時 {RunLog.FormatMs(runLog.totalMs)}    種子 {run.SeedCode}";
            var t = UiKit.Text(layer, 0, 205, body, 22, 0xffffff, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            t.rectTransform.sizeDelta = new Vector2(900, 200);
            string next = NextUnlockLine(save);
            if (next != null) UiKit.Text(layer, 0, 330, next, 22, 0xffd36b, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            UiKit.Button(layer, -130, 480, 240, 60, "再來一局", 0xf28c28, () => Restart(classId), 24, UiKit.TopCenter, UiKit.TopCenter);
            UiKit.Button(layer, 130, 480, 240, 60, "回標題", 0x2f6fb5, () => Bootstrap.ToTitle(), 24, UiKit.TopCenter, UiKit.TopCenter);
            FeedbackButton(layer, 0, 560);
        }

        /// <summary>"再 N 星塵解鎖：X" for the cheapest unlock not yet owned, or a nudge back to base when one is affordable (v2 §3).</summary>
        public static string NextUnlockLine(SaveData save)
        {
            UnlockDef best = null;
            foreach (var u in GameDb.Unlocks)
            {
                if (save.unlocks.Contains(u.id)) continue;
                if (best == null || u.cost < best.cost) best = u;
            }
            if (best == null) return null;
            int need = best.cost - save.stardust;
            if (need <= 0) return $"星塵夠了！回基地解鎖：{best.name}";
            return GameDb.Str("result.nextUnlock").Replace("{n}", need.ToString()).Replace("{name}", best.name);
        }

        void ShowPause()
        {
            paused = true;
            var layer = NewOverlay(0.6f);
            UiKit.Text(layer, 0, 160, "暫停", 44, 0xfff4de, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            UiKit.Text(layer, 0, 230, $"種子 {run.SeedCode}   第 {Mathf.Min(run.roomIndex + 1, run.Rules.combatRooms + 1)} 房", 20, 0xcfd4e6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            UiKit.Button(layer, 0, 320, 260, 60, "繼續", 0xf28c28, ClearOverlay, 24, UiKit.TopCenter, UiKit.TopCenter);
            UiKit.Button(layer, -140, 400, 260, 60, world.Player.prog.skillPoints > 0 ? $"技能（{world.Player.prog.skillPoints} 點可用）" : "技能", 0x2f6fb5, () => ShowSkillPanel(0), 22, UiKit.TopCenter, UiKit.TopCenter);
            UiKit.Button(layer, 140, 400, 260, 60, "放棄本局", 0x7a2a2a, () => { run.abandoned = true; finished = true; FinishRunLog("abandon"); ClearOverlay(); ShowResult(false); }, 24, UiKit.TopCenter, UiKit.TopCenter);
            FeedbackButton(layer, 0, 480);
        }

        /// <summary>
        /// Skill panel (long-press a skill button, or pause → 技能): every skill with its description, the numbers at the
        /// current and next level, and a button to spend a skill point. The world is paused while it is open.
        /// </summary>
        public void ShowSkillPanel(int focus)
        {
            if (finished) return;
            var p = world.Player;
            var ui = GameDb.SkillUi;
            var layer = NewOverlay(0.72f);
            paused = true;
            UiKit.Text(layer, 0, 30, $"{GameDb.Str("skill.panel.title")}　　{GameDb.Str("skill.panel.points").Replace("{n}", p.prog.skillPoints.ToString())}", 30, 0xfff4de, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            int n = p.cls.skills.Count;
            float cardW = 250, cardH = 430, gap = 14;
            float startX = -((n + 1) * cardW + n * gap) / 2; // + one card for the class dodge
            string[] keys = { "U", "I", "O", "P" };
            for (int i = 0; i < n; i++)
            {
                var s = p.cls.skills[i];
                int idx = i;
                int lv = BaseSkillLv(p, s);
                bool learned = lv > 0;
                var card = UiKit.Rect(layer, startX + i * (cardW + gap), 80, cardW, cardH, i == focus ? 0x2a2550 : 0x1b1830, 0.96f, UiKit.TopCenter);
                if (i == focus) UiKit.Rect(card.transform, 0, 0, cardW, 4, 0xf28c28, 1f);
                var icon = UiKit.Sprite(card.transform, SpriteBank.Icon(p.cls.id, i), 14, 14, 64, 64);
                UiKit.SetAlpha(icon, learned ? 1f : 0.4f);
                if (!learned)
                {
                    var lockSprite = Resources.Load<Sprite>("Icons/skill_locked");
                    if (lockSprite != null) UiKit.Sprite(card.transform, lockSprite, 24, 24, 44, 44);
                }
                UiKit.Text(card.transform, 88, 14, s.name, 22, 0xfff4de, bold: true);
                string type = ui.typeLabel.TryGetValue(s.type.ToString().ToLowerInvariant(), out var tl) ? tl : s.type.ToString();
                string elem = ui.elementLabel.TryGetValue(s.element.ToString().ToLowerInvariant(), out var el) ? el : s.element.ToString();
                UiKit.Text(card.transform, 88, 42, GameDb.Str("skill.level").Replace("{n}", lv.ToString()).Replace("{max}", s.maxLv.ToString()) + $"　[{(i < keys.Length ? keys[i] : "")}]", 14, 0xcfd4e6);
                UiKit.Text(card.transform, 88, 60, GameDb.Str("skill.type").Replace("{type}", type).Replace("{element}", elem), 13, 0xcfd4e6);
                var desc = UiKit.Text(card.transform, 14, 88, s.desc ?? "", 15, 0xe8e4f0);
                desc.rectTransform.sizeDelta = new Vector2(cardW - 28, 64);
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                var stats = UiKit.Text(card.transform, 14, 158, SkillNumbers(s, lv, ui), 14, 0xcfe8ff);
                stats.rectTransform.sizeDelta = new Vector2(cardW - 28, 190);
                stats.horizontalOverflow = HorizontalWrapMode.Wrap;
                string blocker = SkillUpBlocker(p, i);
                string label = blocker ?? GameDb.Str(learned ? "skill.raise" : "skill.learn");
                var btn = UiKit.Button(card.transform, 0, -14, cardW - 36, 48, label, blocker == null ? 0xf28c28 : 0x3a3a4a, () =>
                {
                    if (!SpendSkillPoint(p, idx)) { hud.Toast(GameDb.Str("skill.noPoints")); return; }
                    runLog.skillUps.Add(s.id);
                    world.Sfx.Play("buff");
                    ShowSkillPanel(idx);
                }, 18, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
                btn.interactable = blocker == null;
            }
            // Class dodge card (design doc 06): what the dodge button does for this class.
            var dd = p.cls.Dodge;
            var dcard = UiKit.Rect(layer, startX + n * (cardW + gap), 80, cardW, cardH, 0x16202e, 0.96f, UiKit.TopCenter);
            UiKit.Text(dcard.transform, 14, 14, GameDb.Str("dodge.title").Replace("{name}", dd.name), 20, 0x9be15d, bold: true);
            var ddesc = UiKit.Text(dcard.transform, 14, 50, dd.desc ?? "", 15, 0xe8e4f0);
            ddesc.rectTransform.sizeDelta = new Vector2(cardW - 28, 90);
            ddesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var dstats = UiKit.Text(dcard.transform, 14, 150,
                $"距離：{dd.distance:0} px\n無敵：{dd.invuln / 1000f:0.##} 秒\n冷卻：{dd.cooldown / 1000f:0.#} 秒" +
                (dd.knock > 0 ? $"\n撞開敵人：{dd.knock:0}" : "") + (dd.onAvoidHeal > 0 ? $"\n閃過攻擊回復：{dd.onAvoidHeal * 100:0}% HP" : "") +
                (dd.cancelWindow > 0 ? $"\n結束前 {dd.cancelWindow:0} ms 可接攻擊" : "") + (dd.passThrough ? "\n穿過敵人" : ""), 14, 0xcfe8ff);
            dstats.rectTransform.sizeDelta = new Vector2(cardW - 28, 200);
            dstats.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Text(layer, 0, 520, GameDb.Str("skill.panel.hint"), 14, 0x9aa0b4, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            UiKit.Button(layer, 0, 548, 220, 54, "關閉", 0x2f6fb5, ClearOverlay, 22, UiKit.TopCenter, UiKit.TopCenter);
        }

        static Scaling? ScalingOf(SkillDef s, string field) => field switch
        {
            "power" => s.power,
            "sp" => s.sp,
            "count" => s.count,
            "duration" => s.duration,
            "stunChance" => s.stunChance,
            "freezeChance" => s.freezeChance,
            "defDown" => s.defDown,
            "goldCost" => s.goldCost,
            "poison" => s.poison,
            "heal" => s.heal,
            "passiveValue" => s.passiveValue,
            "dashDistance" => s.dashDistance,
            _ => null,
        };

        static string Fmt(float v, string format) => format switch
        {
            "mult" => $"×{v:0.00}",
            "percent" => $"{v * 100:0}%",
            "seconds" => $"{v / 1000f:0.#} 秒",
            "px" => $"{v:0} px",
            _ => $"{v:0}",
        };

        static string FmtBuff(BuffEffect b, string field, string format)
        {
            if (b == null) return null;
            float? f = field switch { "atkMult" => b.atkMult, "matkMult" => b.matkMult, "aspdMult" => b.aspdMult, "moveMult" => b.moveMult, "allStats" => b.allStats, "shieldHits" => (float?)b.shieldHits, _ => null };
            bool? flag = field switch { "endure" => b.endure, "hidden" => b.hidden, _ => null };
            if (format == "flag") return flag == true ? "✓" : null;
            if (!f.HasValue) return null;
            return format == "multPercent" ? $"+{(f.Value - 1) * 100:0}%" : $"{f.Value:0}";
        }

        /// <summary>"現在 → 下一級" rows from skillUi.json (SKILL_STAT_ROWS / BUFF_STAT_ROWS), only the fields the skill uses.</summary>
        static string SkillNumbers(SkillDef s, int lv, SkillUiTable ui)
        {
            int cur = System.Math.Max(1, lv), next = System.Math.Min(s.maxLv, cur + 1);
            bool showNext = lv < s.maxLv;
            string typeKey = s.type.ToString().ToLowerInvariant();
            var lines = new List<string>();
            foreach (var row in ui.statRows)
            {
                if (row.onlyTypes != null && row.onlyTypes.Count > 0 && !row.onlyTypes.Contains(typeKey)) continue;
                var sc = ScalingOf(s, row.field);
                if (!sc.HasValue) continue;
                if (row.field == "power" && sc.Value.Base <= 0) continue;
                if (row.field == "sp" && s.type == SkillType.Passive) continue;
                string a = Fmt(sc.Value.At(cur), row.format), b = Fmt(sc.Value.At(next), row.format);
                lines.Add(showNext && a != b ? $"{row.label}：{a} → {b}" : $"{row.label}：{a}");
            }
            if (s.buffByLevel != null && s.buffByLevel.Count > 0)
                foreach (var row in ui.buffRows)
                {
                    string a = FmtBuff(s.BuffAt(cur), row.field, row.format), b = FmtBuff(s.BuffAt(next), row.field, row.format);
                    if (a == null && b == null) continue;
                    lines.Add(showNext && a != b ? $"{row.label}：{a ?? "—"} → {b ?? "—"}" : $"{row.label}：{a ?? b}");
                }
            if (s.cooldown > 0) lines.Add(GameDb.Str("skill.cooldown").Replace("{s}", (s.cooldown / 1000f).ToString("0.#")));
            if (lv == 0) lines.Add("（尚未學習，顯示 Lv1 數值）");
            return string.Join("\n", lines);
        }

        /// <summary>"回報問題" opens the beta feedback form / mail with build, class, seed and room pre-filled; hidden when none is configured.</summary>
        void FeedbackButton(RectTransform layer, float x, float y)
        {
            if (!Feedback.Available) return;
            UiKit.Button(layer, x, y, 260, 48, "回報問題", 0x3a3a4a, () => Feedback.Open(BuildInfo.Number, classId, run.SeedCode, run.roomIndex + 1), 20, UiKit.TopCenter, UiKit.TopCenter);
        }

        void Restart(string cls)
        {
            Destroy(gameObject);
            Destroy(worldRoot.gameObject);
            Destroy(cam.gameObject);
            Destroy(hud.gameObject);
            if (touch != null) Destroy(touch.gameObject);
            Launch(cls);
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && !finished && overlay == null) ShowPause();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && !finished && overlay == null && Application.isMobilePlatform) ShowPause();
        }

        // ---------- frame ----------

        readonly float[] skillKeyHeld = new float[4];

        /// <summary>Holding U/I/O/P for half a second opens the skill panel on that slot (touch buttons do the same via TouchControls).</summary>
        bool KeyboardSkillLongPress(out int slot)
        {
            slot = -1;
            var kb = Keyboard.current;
            if (kb == null) return false;
            var keys = new[] { kb.uKey, kb.iKey, kb.oKey, kb.pKey };
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].isPressed) skillKeyHeld[i] += Time.unscaledDeltaTime; else skillKeyHeld[i] = 0;
                if (skillKeyHeld[i] >= 0.5f) { skillKeyHeld[i] = 0; slot = i; return true; }
            }
            return false;
        }

        void Update()
        {
            if (overlay != null)
            {
                var kb = Keyboard.current;
                if (perkChoices != null && kb != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame && perkChoices.Count > 0) PickPerk(perkChoices[0]);
                    else if (kb.digit2Key.wasPressedThisFrame && perkChoices.Count > 1) PickPerk(perkChoices[1]);
                    else if (kb.digit3Key.wasPressedThisFrame && perkChoices.Count > 2) PickPerk(perkChoices[2]);
                }
                if (paused && GameInput.Consume(GameAction.Menu)) ClearOverlay();
                GameInput.Reset();
                return;
            }
            if (GameInput.Consume(GameAction.Menu) && !finished) { ShowPause(); return; }
            if (KeyboardSkillLongPress(out int held)) { ShowSkillPanel(held); return; }

            float dt = Time.deltaTime;
            world.Step(dt);
            if (hintsOn && !world.Player.dead && world.Player.hp < world.Player.maxHp * 0.4f) Hint("hint.potion");
            UpdateRoomFlow();
            UpdateDoor();
            cam.Follow(dt);
            background?.Parallax(world.CamX);
            foreach (var kv in views) kv.Value.Sync();
        }
    }
}
