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
        /// <summary>Stage to play (S1); null = the first chapter's stage.</summary>
        public string stageId;
        /// <summary>When set, the run continues from this snapshot instead of starting fresh (S5).</summary>
        public RunSnapshot resumeFrom;

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
        /// <summary>Room affix in effect (design doc 08 §2.6), null when none; extra perk picks 寶箱房 still owes; which spawn (by order) becomes the elite.</summary>
        RoomAffixDef affix;
        int extraPerkPicks, spawnSerial, eliteSerial = -1;
        bool AffixIs(string kind) => affix != null && affix.effect.kind == kind;
        /// <summary>Chapter this run's boss kill just opened, shown on the result screen.</summary>
        ChapterDef unlockedChapter;
        /// <summary>Epic card this run's boss kill dropped (S2), revealed on the result screen.</summary>
        Cards.DropResult cardDrop;
        /// <summary>Stardust already paid out by 遺跡秘寶 (killStardust) this run.</summary>
        int killStardustGiven;
        Canvas overlayCanvas;
        GameObject overlay;
        bool paused;

        public static GameController Launch(string classId, ulong seed = 0) => Launch(classId, null, seed);

        /// <summary>Continues the run saved in <paramref name="snap"/> from the start of its current room.</summary>
        public static GameController Resume(RunSnapshot snap)
        {
            var go = new GameObject("Game");
            go.SetActive(false);
            var gc = go.AddComponent<GameController>();
            gc.classId = snap.classId;
            gc.stageId = snap.stageId;
            gc.seedOverride = Rng.CodeToSeed(snap.seedCode);
            gc.resumeFrom = snap;
            go.SetActive(true);
            return gc;
        }

        public static GameController Launch(string classId, string stageId, ulong seed = 0)
        {
            var go = new GameObject("Game");
            go.SetActive(false); // Awake runs on AddComponent for an active object; set the fields first
            var gc = go.AddComponent<GameController>();
            gc.classId = classId;
            gc.stageId = stageId;
            gc.seedOverride = seed;
            go.SetActive(true);
            return gc;
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            save = SaveSystem.Current ?? SaveSystem.Load();
            run = RunState.New(classId, save, stageId, seedOverride == 0 ? null : seedOverride);
            stageId = run.stageId;
            save.lastSeedCode = run.SeedCode;
            world = new World { Rng = run.rng, Inv = run.inv, Mods = run.mods };
            worldRoot = new GameObject("World").transform;

            var cls = GameDb.Class(classId);
            var snap = resumeFrom;
            var prog = snap?.prog ?? ClassProgress.New(cls);
            prog.skillLv ??= new Dictionary<string, int>();
            prog.stats ??= cls.baseStats.Clone();
            if (snap != null)
            {
                // Replay the perks in the order they were taken: each offer is drawn first so the perk rng advances exactly
                // as it did in the original session and the next offers come out the same (QA #9 still holds after a resume).
                foreach (var id in snap.perks)
                {
                    run.DrawPerks(run.Rules.perkChoices, RunState.RareChanceBonus(save));
                    run.TakePerk(GameDb.Perk(id));
                }
                run.mods.reviveUsed = snap.reviveUsed;
                world.Mods = run.mods;
                run.inv.red = snap.red; run.inv.blue = snap.blue; run.inv.gold = snap.gold;
                run.stardustEarned = snap.stardustEarned;
                run.roomsCleared = snap.roomsCleared;
                runLog.rooms.AddRange(snap.rooms ?? new List<RoomLogEntry>());
                runLog.skillUps.AddRange(snap.skillUps ?? new List<string>());
                runLog.resumes = snap.resumes + 1;
            }
            world.Player = new Player(world, 200, (Units.GroundTopPx + Units.GroundBottomPx) / 2, cls, prog);
            if (snap == null)
            {
                // Growth curve v2 §1: the 3 starting stat points are spent at once and skills unlock by level.
                AllocateStats(world.Player, 1);
                prog.skillPoints = run.mods.startSkillPoints; // skill 1 is known from the start; every level up adds one point (talents may add more)
                world.Player.Recompute(true);
            }
            else
            {
                world.Player.Recompute(false);
                world.Player.hp = Mathf.Clamp(snap.hp, 1, world.Player.maxHp);
                world.Player.sp = Mathf.Clamp(snap.sp, 0, world.Player.maxSp);
            }
            hintsOn = snap == null && save.runsPlayed < 2;

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
            world.ZoneSpawned += zn => ZoneView.Create(zn, worldRoot);
            world.EnemyKilled += OnEnemyKilled;
            world.PlayerDied += OnPlayerDead;
            world.PlayerHurt += () => Hint("hint.dodge");
            world.Toasted += hud.Toast;
            world.ComboChanged += hud.Combo;

            GameInput.Enable();
            run.startedAt = snap != null ? -snap.elapsedMs : 0; // world.Now starts at 0; the snapshot carries the time already played
            var chapter = run.Chapter;
            AudioManager.Instance.PlayMusic(chapter.music ?? "field");
            if (!string.IsNullOrEmpty(chapter.ambience)) AudioManager.Instance.PlayAmbience(chapter.ambience, 0.35f); else AudioManager.Instance.StopAmbience();
            EnterRoom(snap != null ? Mathf.Clamp(snap.roomIndex, 0, run.waveSetByRoom.Count - 1) : 0);
            if (snap != null) hud.Toast(GameDb.Str("chapter.resumed"), 2.5f);
            else ShowStory(run.Stage.story?.before, "before", null);
        }

        // ---------- story (S4) ----------

        /// <summary>
        /// Plays a story beat once per account: the stage's `before` key when a fresh run starts, `after` when its boss
        /// falls. The world is paused underneath; <paramref name="then"/> runs when the player finishes or skips.
        /// </summary>
        void ShowStory(string storyKey, string slot, System.Action then)
        {
            var story = string.IsNullOrEmpty(storyKey) ? null : GameDb.Story(storyKey);
            var lines = story == null ? null : slot == "before" ? story.before : story.after;
            string seenKey = $"{run.stageId}:{slot}";
            if (lines == null || lines.Count == 0 || save.storySeen.Contains(seenKey)) { then?.Invoke(); return; }
            save.storySeen.Add(seenKey);
            SaveSystem.Save();
            var layer = NewOverlay(0.35f);
            var cls = GameDb.Class(classId);
            DialogueBox.Show(layer, lines, cls.name, $"{classId}_front", () =>
            {
                ClearOverlay();
                then?.Invoke();
            });
        }

        void OnDestroy() => GameInput.Disable();

        // ---------- resume snapshot (S5) ----------

        /// <summary>Writes the run as it stands at the start of the current room; the title screen offers to continue it.</summary>
        void SaveSnapshot()
        {
            if (finished) return;
            var p = world.Player;
            save.runSnapshot = new RunSnapshot
            {
                classId = classId, stageId = run.stageId, seedCode = run.SeedCode,
                roomIndex = run.roomIndex,
                perks = run.perks.ConvertAll(pk => pk.id),
                prog = p.prog,
                hp = Mathf.RoundToInt(p.hp), sp = Mathf.RoundToInt(p.sp),
                red = run.inv.red, blue = run.inv.blue, gold = run.inv.gold,
                stardustEarned = run.stardustEarned, roomsCleared = run.roomsCleared,
                reviveUsed = run.mods.reviveUsed,
                elapsedMs = (int)(world.Now - run.startedAt),
                rooms = runLog.rooms.FindAll(r => r != roomLog), // the current room is logged again when it is replayed
                skillUps = new List<string>(runLog.skillUps),
                resumes = runLog.resumes,
                savedAt = System.DateTime.UtcNow.ToString("o"),
            };
            SaveSystem.Save();
        }

        void ClearSnapshot()
        {
            if (save.runSnapshot == null) return;
            save.runSnapshot = null;
            SaveSystem.Save();
        }

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
            // Room affix and elite for this room were rolled with the run (RunState.RollAffixes); boss rooms have neither.
            affix = !run.InBossRoom && run.AffixAt(index) != null ? GameDb.Affix(run.AffixAt(index)) : null;
            extraPerkPicks = AffixIs("treasure") ? affix.effect.extraPerkPicks : 0;
            spawnSerial = 0; eliteSerial = -1;
            if (!run.InBossRoom && run.EliteAt(index))
            {
                var eligible = new List<int>();
                int serial = 0;
                foreach (var w in waves) foreach (var s in w.spawns) { if (GameDb.Elite.Eligible(GameDb.Enemy(s.enemy))) eligible.Add(serial); serial++; }
                if (eligible.Count > 0) eliteSerial = eligible[run.EliteSpawnPickAt(index) % eligible.Count];
            }
            roomLog.affix = affix?.id;
            roomLog.elite = eliteSerial >= 0;
            CrashLog.Context = $"{classId} {run.stageId} seed {run.SeedCode} room {index + 1} {run.CurrentRoomId}/{run.CurrentWaveSet}";

            foreach (var e in world.Enemies) if (views.TryGetValue(e, out var v)) Destroy(v.gameObject);
            foreach (var e in world.Enemies) views.Remove(e);
            world.Enemies.Clear();
            world.ClearHazards();
            if (background != null) Destroy(background.gameObject);
            background = Background.Create(worldRoot, room.bg, room.bgGroundY, room.width, room.tint);
            world.StageWidth = room.width;
            world.LockRight = room.width;
            hud.SetFog(AffixIs("fog") ? 0.45f : 0f);
            if (AffixIs("poisonGround"))
            {
                // 毒沼: patches across the room the player has to fight around; cleared with the room's hazards.
                int n = System.Math.Max(1, affix.effect.zones);
                for (int k = 0; k < n; k++)
                {
                    float zx = room.width * (k + 1) / (n + 1f) + (k % 2 == 0 ? -120 : 120);
                    float zy = k % 2 == 0 ? Units.GroundTopPx + 50 : Units.GroundBottomPx - 50;
                    world.SpawnZone(new ZoneOpts { x = zx, y = zy, radius = 130, duration = 1e8f, color = 0x7bd13a, what = "poisonGround", hurtsPlayer = true, playerDpsFraction = affix.effect.dpsFraction });
                }
            }

            var p = world.Player;
            p.x = 200; p.y = (Units.GroundTopPx + Units.GroundBottomPx) / 2; p.z = 0; p.vx = p.vy = p.vz = 0; p.facing = 1;
            p.struck.Clear();
            p.OnRoomStart();
            if (p.state != FighterState.Dead) p.SetState(FighterState.Idle);
            cam.SnapTo(p.x);
            string label = run.InBossRoom ? room.name : $"第 {index + 1} 房  {room.name}";
            SaveSnapshot();
            // The chapter title card on the first room is the cheapest "something new" cue when a scene unlocks (S1).
            if (index == 0) fx.Banner($"{GameDb.Str("chapter.start").Replace("{n}", run.Chapter.order.ToString())}  {run.Chapter.name}", 0xfff4de, 56);
            world.Delay(index == 0 ? 1400 : 0, () => fx.Banner(label, 0xffffff, 48));
            if (affix != null)
            {
                var a = affix;
                world.Delay(index == 0 ? 2600 : 1200, () =>
                {
                    fx.Banner(GameDb.Str("affix.enter").Replace("{name}", a.name), 0xc9a0ff, 40);
                    world.Toast(a.desc);
                });
            }
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
                int serial = spawnSerial++;
                float y = run.rng.Between((int)Units.GroundTopPx + 20, (int)Units.GroundBottomPx - 20);
                if (AffixIs("fog") && affix.effect.warnMs > 0)
                {
                    // 濃霧: a purple glow marks where the enemy will step in, warnMs before it does.
                    world.Delay(Mathf.Max(0, spawn.delay - affix.effect.warnMs), () =>
                    {
                        if (finished) return;
                        float wx = Mathf.Clamp(spawn.side == Side.Right ? world.CamX + world.ViewW - 60 : world.CamX + 60, 0, room.width);
                        fx.Ring(wx, y, 70, 0xc080ff, y - 1);
                        fx.Aura(wx, y, 0xc080ff);
                    });
                }
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
                    var scaling = run.Scaling;
                    float hpMult = run.InBossRoom ? scaling.boss.hp : scaling.HpAt(slot);
                    float atkMult = run.InBossRoom ? scaling.boss.atk : scaling.AtkAt(slot);
                    if (run.InBossRoom && !def.boss) { hpMult = scaling.HpAt(scaling.hp.Count - 1); atkMult = scaling.AtkAt(scaling.atk.Count - 1); }
                    if (AffixIs("rush")) hpMult *= affix.effect.hpMult; // 急襲: faster waves, softer enemies
                    bool makeElite = serial == eliteSerial && GameDb.Elite.Eligible(def);
                    string eliteMod = null;
                    if (makeElite)
                    {
                        var mods = GameDb.Elite.modifiers;
                        eliteMod = mods != null && mods.Count > 0 ? mods[run.rng.Range(0, mods.Count)] : "shield";
                    }
                    System.Action<Enemy> setup = null;
                    if (makeElite) setup = en => en.MakeElite(GameDb.Elite, eliteMod);
                    var e = world.SpawnEnemy(def, x, y, hpMult, atkMult, setup);
                    if (makeElite)
                    {
                        fx.Banner($"{GameDb.Str("elite.appear")}  {GameDb.Str("elite." + eliteMod)}", 0xffd36b, 44);
                        fx.Ring(x, y, 120, 0xffd36b, y - 1);
                        fx.Aura(x, y, 0xffd36b);
                        world.Sfx.Play("eliteAppear");
                        cam.Shake(0.2f, 0.006f);
                    }
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
                        AudioManager.Instance.PlayMusic(run.Chapter.bossTrack ?? "boss", 0.3f);
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
                    : !waveActive || world.Now - lastWaveStart >= waves[waveIndex].timeout * (AffixIs("rush") ? affix.effect.waveTimeoutMult : 1f);
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
                // First kill of *this* boss pays the bonus and unlocks the next chapter (S1); the save remembers both.
                string bossId = run.BossEnemyId ?? (boss != null ? boss.def.id : null);
                bool firstKill = bossId != null && !save.bossFirstKills.Contains(bossId);
                if (firstKill) { reward += run.Rules.stardust.firstClearBonus; save.bossFirstKills.Add(bossId); save.bossCleared = true; }
                if (!save.stagesCleared.Contains(run.stageId)) save.stagesCleared.Add(run.stageId);
                unlockedChapter = firstKill ? Progress.ChapterUnlockedBy(bossId) : null;
                // Epic card drop (S2): the signature card on a first kill, otherwise one from the boss's pool.
                cardDrop = bossId != null ? Cards.Drop(save, bossId, firstKill, run.rng) : null;
                if (cardDrop != null) runLog.cardDrop = cardDrop.card.id;
                AddStardust(reward);
                fx.Banner("RUN CLEAR!", Colors.Crit, 72);
                AudioManager.Instance.StopMusic(1.5f);
                world.Sfx.Play("clear");
                finished = true;
                FinishRunLog("win");
                world.Delay(2200, () => ShowStory(run.Stage.story?.after, "after", () => ShowResult(true)));
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
            if (run.mods.stardust != 1f) n = Mathf.RoundToInt(n * run.mods.stardust);
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
            if (run.mods.killStardustAdd > 0 && killStardustGiven < run.mods.killStardustMax && !e.def.boss)
            {
                // 遺跡秘寶 (killStardust): a little stardust per kill, capped per run.
                int give = Mathf.Min(run.mods.killStardustAdd, run.mods.killStardustMax - killStardustGiven);
                killStardustGiven += give;
                AddStardust(give);
                fx.Number(e.x, e.y - e.h - 90, $"✦ +{give}", 0xcfe8ff);
                world.Sfx.Play("card_ruinTreasure");
            }
            if (e.elite)
            {
                // Elite kill: fixed stardust (×2 in 精英房); 爆裂 elites blow up on death and hurt the player in range.
                int give = Mathf.RoundToInt(GameDb.Elite.stardust * (AffixIs("elite") ? affix.effect.stardustMult : 1f));
                if (give > 0)
                {
                    AddStardust(give);
                    fx.Number(e.x, e.y - e.h - 120, GameDb.Str("elite.stardust").Replace("{n}", give.ToString()), 0xffd36b, true);
                }
                if (e.eliteMod == "volatile")
                {
                    float radius = GameDb.Elite.volatileRadius;
                    fx.Ring(e.x, e.y, radius, 0xff6b3a, e.y - 1);
                    fx.Spark(e.x, e.y - e.h / 2, 0xff6b3a, 2.2f);
                    fx.Dust(e.x, e.y, 14, 1.6f);
                    cam.Shake(0.25f, 0.01f);
                    world.Sfx.Play("hitHeavy");
                    if (!p.dead && World.InCircle(p.x, p.y, e.x, e.y, radius + p.w / 2))
                        p.TakeHit(e.ExplosionHit(GameDb.Elite.volatilePower, System.Math.Sign(p.x - e.x) == 0 ? 1 : System.Math.Sign(p.x - e.x)));
                }
            }
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
            ClearSnapshot();
            runLog.build = BuildInfo.Number;
            runLog.classId = classId;
            runLog.chapterId = run.Chapter.id;
            runLog.cardsEquipped = new List<string>(save.equippedCards);
            runLog.talents = Talents.OwnedFor(save, classId).ConvertAll(t => t.id);
            runLog.stageId = run.stageId;
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
            if (extraPerkPicks > 0)
            {
                // 寶箱房: one more three-card pick before the door opens.
                extraPerkPicks--;
                fx.Banner(GameDb.Str("affix.enter").Replace("{name}", affix?.name ?? ""), 0xc9a0ff, 40);
                world.Delay(500, ShowPerkChoice);
                return;
            }
            OpenDoor();
        }

        void ShowResult(bool win)
        {
            var layer = NewOverlay(0.75f);
            UiKit.Text(layer, 0, 110, win ? GameDb.Str("result.clear") : GameDb.Str("result.dead"), 48, win ? Colors.Crit : 0xff9a9a, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            if (!win) UiKit.Text(layer, 0, 168, GameDb.Str("result.deadSub"), 18, 0xcfd4e6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            string body = $"{run.Chapter.name}  清除房間 {run.roomsCleared} / {run.CombatRooms}" + (run.bossKilled ? "  ＋ Boss" : "") +
                          $"\n獲得星塵 ✦ +{run.stardustEarned}    累計 ✦ {save.stardust}" +
                          $"\n強化：{(run.perks.Count == 0 ? "無" : string.Join("、", run.perks.ConvertAll(p => p.name)))}" +
                          $"\n用時 {RunLog.FormatMs(runLog.totalMs)}    種子 {run.SeedCode}";
            var t = UiKit.Text(layer, 0, 205, body, 22, 0xffffff, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            t.rectTransform.sizeDelta = new Vector2(900, 200);
            string next = unlockedChapter != null ? GameDb.Str("chapter.unlocked").Replace("{name}", unlockedChapter.name) : NextUnlockLine(save);
            if (next != null) UiKit.Text(layer, 0, 330, next, 22, 0xffd36b, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            if (cardDrop != null) ShowCardDrop(layer, cardDrop);
            var (owned, total) = Cards.Collection(save);
            if (total > 0) UiKit.Text(layer, 0, 366, $"{GameDb.Str("card.title")} {owned}/{total}", 16, 0xcfd4e6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            UiKit.Button(layer, -130, 480, 240, 60, "再來一局", 0xf28c28, () => Restart(classId), 24, UiKit.TopCenter, UiKit.TopCenter);
            UiKit.Button(layer, 130, 480, 240, 60, "回標題", 0x2f6fb5, () => Bootstrap.ToTitle(), 24, UiKit.TopCenter, UiKit.TopCenter);
            FeedbackButton(layer, 0, 560);
        }

        /// <summary>
        /// The card that just dropped, shown beside the result text: epic frame, boss portrait, name and whether it is new,
        /// a shard or a star-up (design doc 07 §3.1). Tapping it opens the card book on the title later.
        /// </summary>
        void ShowCardDrop(RectTransform layer, Cards.DropResult drop)
        {
            var card = drop.card;
            float cw = 170, chh = cw * 520 / 360, k = cw / 360f;
            var frameSprite = Resources.Load<Sprite>("UI/card_epic");
            Image frame = frameSprite != null
                ? UiKit.Sprite(layer, frameSprite, 520, 150, cw, chh, UiKit.TopCenter)
                : UiKit.Rect(layer, 520, 150, cw, chh, 0x8a4fd1, 0.95f, UiKit.TopCenter);
            if (frameSprite != null) { frame.type = Image.Type.Sliced; frame.preserveAspect = false; }
            var portrait = SpriteBank.Get(string.IsNullOrEmpty(card.icon) ? GameDb.Enemy(card.boss).SpriteId + "_side" : card.icon);
            if (portrait != null) UiKit.Sprite(frame.transform, portrait, 0, 40 * k, 200 * k, 200 * k, UiKit.TopCenter, UiKit.TopCenter).preserveAspect = true;
            UiKit.Text(frame.transform, 0, 262 * k, card.name, (int)(44 * k), 0x2b2140, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true, 0);
            UiKit.Text(frame.transform, 0, 320 * k, new string('★', drop.progress?.stars ?? 1), (int)(40 * k), 0xf2a81d, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, false, 0);
            string line = drop.outcome switch
            {
                Cards.DropOutcome.New => GameDb.Str("card.drop"),
                Cards.DropOutcome.StarUp => GameDb.Str("card.starUp"),
                Cards.DropOutcome.Shard => GameDb.Str("card.dropDup").Replace("{n}", drop.shardsAdded.ToString()),
                _ => GameDb.Str("card.dropDup").Replace("{n}", "0"),
            };
            UiKit.Text(layer, 520, 118, line, 20, 0xffd36b, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            frame.transform.localScale = Vector3.zero;
            StartCoroutine(PopIn(frame.transform));
            world.Sfx.Play(drop.outcome == Cards.DropOutcome.StarUp ? "cardStarUp" : "cardAppear");
        }

        System.Collections.IEnumerator PopIn(Transform t)
        {
            float d = 0;
            while (d < 0.45f)
            {
                d += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(d / 0.45f);
                float s = k < 0.7f ? Mathf.Lerp(0f, 1.12f, k / 0.7f) : Mathf.Lerp(1.12f, 1f, (k - 0.7f) / 0.3f);
                if (t != null) t.localScale = new Vector3(s, s, 1);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
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
            UiKit.Text(layer, 0, 230, $"種子 {run.SeedCode}   第 {Mathf.Min(run.roomIndex + 1, run.CombatRooms + 1)} 房", 20, 0xcfd4e6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
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
            Launch(cls, stageId);
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
