using System.Collections.Generic;
using BoLike.Core;
using BoLike.Data;
using BoLike.Sim;
using BoLike.Systems;
using BoLike.UI;
using UnityEngine;
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
        Enemy boss;
        Canvas overlayCanvas;
        GameObject overlay;
        bool paused;

        public static GameController Launch(string classId, ulong seed = 0)
        {
            var go = new GameObject("Game");
            var gc = go.AddComponent<GameController>();
            gc.classId = classId;
            gc.seedOverride = seed;
            return gc;
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            save = SaveSystem.Current ?? SaveSystem.Load();
            run = RunState.New(classId, save, seedOverride == 0 ? null : seedOverride);
            world = new World { Rng = run.rng, Inv = run.inv, Mods = run.mods };
            worldRoot = new GameObject("World").transform;

            var cls = GameDb.Class(classId);
            var prog = ClassProgress.New(cls);
            world.Player = new Player(world, 200, (Units.GroundTopPx + Units.GroundBottomPx) / 2, cls, prog);
            world.Player.Recompute(true);

            cam = GameCamera.Create(world);
            bool isTouch = TouchControls.IsTouchDevice;
            hud = Hud.Create(world, isTouch);
            hud.Stardust = () => save.stardust + run.stardustEarned;
            if (isTouch) touch = TouchControls.Create(world);
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
            waves = room.waveSets[run.rng.Range(0, room.waveSets.Count)];
            waveIndex = 0;
            pendingSpawns = 0;
            roomCleared = doorOpen = transitioning = false;
            boss = null;
            roomEnteredAt = world.Now;

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
        }

        void SpawnWave()
        {
            var wave = waves[waveIndex];
            waveIndex++;
            lastWaveStart = world.Now;
            pendingSpawns += wave.spawns.Count;
            int slot = run.CurrentSlot;
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
            float heal = run.Rules.roomClearHeal + run.mods.roomClearHeal;
            if (run.InBossRoom)
            {
                run.bossKilled = true;
                int reward = run.Rules.stardust.bossKill + run.Rules.stardust.runClear;
                if (!save.bossCleared) { reward += run.Rules.stardust.firstClearBonus; save.bossCleared = true; }
                AddStardust(reward);
                fx.Banner("RUN CLEAR!", Colors.Crit, 72);
                AudioManager.Instance.StopMusic(1.5f);
                world.Sfx.Play("clear");
                finished = true;
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
            int gold = (int)StatFormulas.JsRound(e.def.gold * (1 + p.PassiveValue(Passive.GoldFind)));
            run.inv.gold += gold;
            fx.Number(e.x + 30, e.y - e.h - 30, $"+{gold} G", Colors.Gold);
            int gained = StatFormulas.AddExp(p.prog, e.def.exp);
            fx.Number(e.x, e.y - e.h - 60, $"+{e.def.exp} EXP", Colors.Gold);
            world.Sfx.Play(e.def.boss ? "bossDie" : "enemyDie");
            world.Delay(150, () => world.Sfx.Play("gold", 0.6f));
            p.OnKilled(e);
            if (gained > 0)
            {
                // Run levels auto-allocate: +1 to the class's two best stats per level, every skill +1 every 3 levels.
                AutoAllocate(p, gained);
                p.Recompute(true);
                world.Sfx.Play("levelup");
                fx.Banner("LEVEL UP!", Colors.Crit, 72);
                fx.Aura(p.x, p.y, Colors.Crit);
                fx.Ring(p.x, p.y, 160, Colors.Crit, p.y - 1);
            }
        }

        static void AutoAllocate(Player p, int levels)
        {
            var s = p.prog.stats;
            var b = p.cls.baseStats;
            // Put points where the class already leans (its two highest base stats), one into vit.
            var order = new List<(StatKey k, int v)> { (StatKey.Str, b.str), (StatKey.Agi, b.agi), (StatKey.Vit, b.vit), (StatKey.Int, b.@int), (StatKey.Dex, b.dex), (StatKey.Luk, b.luk) };
            order.Sort((a, c) => c.v.CompareTo(a.v));
            for (int i = 0; i < levels; i++)
            {
                s[order[0].k] += 1; s[order[1].k] += 1; s.vit += 1;
                p.prog.statPoints = 0;
            }
            while (p.prog.skillPoints > 0)
            {
                bool spent = false;
                foreach (var sk in p.cls.skills)
                {
                    int lv = p.prog.skillLv.TryGetValue(sk.id, out var l) ? l : 0;
                    if (lv < sk.maxLv && p.prog.skillPoints > 0) { p.prog.skillLv[sk.id] = lv + 1; p.prog.skillPoints--; spent = true; }
                }
                if (!spent) { p.prog.skillPoints = 0; break; }
            }
        }

        void OnPlayerDead()
        {
            if (finished) return;
            finished = true;
            save.gold = run.inv.gold;
            SaveSystem.Save();
            fx.Banner("你倒下了…", 0xff8080, 64);
            AudioManager.Instance.StopMusic(1.0f);
            world.Sfx.Play("lose");
            world.Delay(1400, () => ShowResult(false));
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
            var layer = NewOverlay(0.6f);
            UiKit.Text(layer, 0, 110, "選擇一個強化", 34, 0xfff4de, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            float cardW = 300, gap = 30;
            float startX = -(choices.Count * cardW + (choices.Count - 1) * gap) / 2;
            for (int i = 0; i < choices.Count; i++)
            {
                var perk = choices[i];
                int frame = perk.rarity == PerkRarity.Epic ? 0x8a4fd1 : perk.rarity == PerkRarity.Rare ? 0x2f6fb5 : 0x5c6b7a;
                var card = UiKit.Rect(layer, startX + i * (cardW + gap), 180, cardW, 340, frame, 0.95f, UiKit.TopCenter);
                UiKit.Rect(card.transform, 6, 6, cardW - 12, 328, 0x1b1830, 0.95f);
                string rarity = perk.rarity == PerkRarity.Epic ? "史詩" : perk.rarity == PerkRarity.Rare ? "稀有" : "普通";
                UiKit.Text(card.transform, 0, 24, rarity, 16, frame == 0x5c6b7a ? 0xcfd4e6 : 0xffffff, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
                UiKit.Text(card.transform, 0, 60, perk.name, 30, 0xfff4de, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
                var desc = UiKit.Text(card.transform, 0, 130, perk.desc, 20, 0xffffff, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
                desc.rectTransform.sizeDelta = new Vector2(cardW - 40, 120);
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                if (run.perkStacks.TryGetValue(perk.id, out var n) && n > 0)
                    UiKit.Text(card.transform, 0, 250, $"已持有 ×{n}", 14, 0xcfd4e6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
                UiKit.Button(card.transform, 0, -20, 200, 50, $"選擇 ({i + 1})", 0xf28c28, () => PickPerk(perk), 20, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            }
            perkChoices = choices;
        }

        List<PerkDef> perkChoices;

        void PickPerk(PerkDef perk)
        {
            run.TakePerk(perk);
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
            UiKit.Text(layer, 0, 120, win ? "通關！" : "本局結束", 48, win ? Colors.Crit : 0xff9a9a, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            string body = $"清除房間 {run.roomsCleared} / {run.Rules.combatRooms}" + (run.bossKilled ? "  ＋ Boss" : "") +
                          $"\n獲得星塵 ✦ {run.stardustEarned}    累計 ✦ {save.stardust}" +
                          $"\n強化：{(run.perks.Count == 0 ? "無" : string.Join("、", run.perks.ConvertAll(p => p.name)))}" +
                          $"\n種子 {run.SeedCode}";
            var t = UiKit.Text(layer, 0, 210, body, 22, 0xffffff, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            t.rectTransform.sizeDelta = new Vector2(900, 200);
            UiKit.Button(layer, -130, 480, 240, 60, "再來一局", 0xf28c28, () => Restart(classId), 24, UiKit.TopCenter, UiKit.TopCenter);
            UiKit.Button(layer, 130, 480, 240, 60, "回標題", 0x2f6fb5, () => Bootstrap.ToTitle(), 24, UiKit.TopCenter, UiKit.TopCenter);
        }

        void ShowPause()
        {
            paused = true;
            var layer = NewOverlay(0.6f);
            UiKit.Text(layer, 0, 160, "暫停", 44, 0xfff4de, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            UiKit.Text(layer, 0, 230, $"種子 {run.SeedCode}   第 {Mathf.Min(run.roomIndex + 1, run.Rules.combatRooms + 1)} 房", 20, 0xcfd4e6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            UiKit.Button(layer, 0, 320, 260, 60, "繼續", 0xf28c28, ClearOverlay, 24, UiKit.TopCenter, UiKit.TopCenter);
            UiKit.Button(layer, 0, 400, 260, 60, "放棄本局", 0x7a2a2a, () => { run.abandoned = true; finished = true; ClearOverlay(); ShowResult(false); }, 24, UiKit.TopCenter, UiKit.TopCenter);
        }

        void Restart(string cls)
        {
            Destroy(gameObject);
            Destroy(worldRoot.gameObject);
            Destroy(cam.gameObject);
            Destroy(hud.gameObject);
            if (touch != null) Destroy(touch.gameObject);
            Start(cls);
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

            float dt = Time.deltaTime;
            world.Step(dt);
            UpdateRoomFlow();
            UpdateDoor();
            cam.Follow(dt);
            background?.Parallax(world.CamX);
            foreach (var kv in views) kv.Value.Sync();
        }
    }
}
