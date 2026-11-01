using BoLike.Core;
using BoLike.Data;
using BoLike.Systems;
using BoLike.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BoLike.Game
{
    /// <summary>
    /// The only component the scene needs. Shows a minimal title screen (class pick, stardust, build number)
    /// and starts a run. Everything else is created from code.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        static Bootstrap instance;
        Canvas canvas;
        GameObject title;

        void Awake()
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            CrashLog.Start(BuildInfo.Number);
            SaveSystem.Load();
            GameDb.Load();
            UiKit.EnsureEventSystem(); // UI buttons need one on every platform, not only touch devices
            AudioManager.Instance.transform.SetParent(transform, false); // survives ToTitle()'s root sweep
            // A always-on camera so the title (pure UI) still has something rendering; the game camera draws on top.
            var camGo = new GameObject("TitleCamera");
            camGo.transform.SetParent(transform, false);
            var tc = camGo.AddComponent<Camera>();
            tc.clearFlags = CameraClearFlags.SolidColor;
            tc.backgroundColor = Colors.Rgb(0x2b2140);
            tc.cullingMask = 0;
            tc.depth = -100;
            tc.orthographic = true;
            ShowTitle();
        }

        public static void ToTitle()
        {
            foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.transform.parent == null && go != instance.gameObject && go.GetComponent<UnityEngine.EventSystems.EventSystem>() == null) Destroy(go);
            instance.ShowTitle();
        }

        void ShowTitle()
        {
            AudioManager.Instance.PlayMusic("title");
            AudioManager.Instance.StopAmbience();
            var save = SaveSystem.Current;
            canvas = UiKit.Canvas("Title", 5);
            canvas.transform.SetParent(transform, false);
            title = canvas.gameObject;
            // Backdrop covers the whole screen (wider than the 1280×720 design layer on iPhone).
            UiKit.Dim(canvas.transform, 1f).color = Colors.Rgb(0x2b2140);
            var art = Resources.Load<Texture2D>("Bg/title");
            if (art != null)
            {
                var img = UiKit.Rect(canvas.transform, 0, 0, 10, 10, 0xffffff, 0.55f);
                img.sprite = Sprite.Create(art, new Rect(0, 0, art.width, art.height), new Vector2(0.5f, 0.5f), 100f);
                var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                img.preserveAspect = false;
                img.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>().aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
                img.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectRatio = (float)art.width / art.height;
            }
            var layer = titleLayer = UiKit.Layer(canvas, "Layer");
            var lockSprite0 = Resources.Load<Sprite>("Icons/skill_locked");
            UiKit.Text(layer, 0, 90, "口袋冒險團", 64, 0xfff4de, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true, 4);
            UiKit.Text(layer, 0, 170, "Pocket Adventurers · 封測版", 20, 0xcfd4e6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            var star = Resources.Load<Sprite>("UI/stardust");
            if (star != null) UiKit.Sprite(layer, star, -52, 196, 28, 28, UiKit.TopCenter, UiKit.TopCenter);
            UiKit.Text(layer, 14, 199, star != null ? $"星塵 {save.stardust}" : $"✦ 星塵 {save.stardust}", 20, 0xcfe8ff, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);

            // Chapter row (S1): every chapter in order. Locked ones say which boss opens them, chapters without data say
            // "coming soon"; tapping an open one selects it and the class buttons below start a run there.
            var chapter = Progress.DefaultChapter(save);
            var chapters = GameDb.Chapters;
            float cx = -(chapters.Count * 300 - 16) / 2f;
            foreach (var ch in chapters)
            {
                var c = ch;
                bool ready = Progress.ChapterReady(c);
                bool open = ready && Progress.ChapterUnlocked(save, c);
                bool selected = open && c.id == chapter.id;
                var cb = UiKit.Button(layer, cx, 232, 284, 64, "", selected ? 0xf28c28 : open ? 0x1b1830 : 0x14121f, () =>
                {
                    if (!open) { Flash(layer, ready ? GameDb.Str("chapter.locked").Replace("{boss}", UnlockBossName(c)) : GameDb.Str("chapter.soon")); return; }
                    save.lastChapterId = c.id;
                    SaveSystem.Save();
                    AudioManager.Instance.Play("select");
                    Destroy(title);
                    ShowTitle();
                }, 20, UiKit.TopCenter);
                string sub = !ready ? GameDb.Str("chapter.soon")
                    : !open ? GameDb.Str("chapter.locked").Replace("{boss}", UnlockBossName(c))
                    : Progress.ChapterCleared(save, c) ? $"{GameDb.Str("chapter.cleared")} ✓  {c.desc}" : c.desc;
                UiKit.Text(cb.transform, 0, 6, $"{GameDb.Str("chapter.start").Replace("{n}", c.order.ToString())}  {c.name}", 20, open ? 0xfff4de : 0x9aa0b4, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true, 1);
                var subText = UiKit.Text(cb.transform, 0, 34, sub, 12, open ? 0xe6e9f5 : 0xffd36b, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, false, 1);
                subText.rectTransform.sizeDelta = new Vector2(270, 24);
                if (!open && lockSprite0 != null) UiKit.Sprite(cb.transform, lockSprite0, -118, 16, 32, 32, UiKit.TopCenter, UiKit.TopCenter);
                cx += 300;
            }
            string stageId = Progress.NextStage(save, chapter).id;

            // Class row: starting classes plus every class that has a stardust unlock. Locked ones show a lock and the
            // price (growth curve v2 §5); tapping one buys it when the stardust is there, like the shop button below.
            var unlocked = new System.Collections.Generic.List<string>(GameDb.StartingClasses);
            var shown = new System.Collections.Generic.List<(string id, UnlockDef unlock)>();
            foreach (var id in unlocked) shown.Add((id, null));
            foreach (var u in GameDb.Unlocks)
            {
                if (u.kind != UnlockKind.Class || unlocked.Contains(u.ValueId)) continue;
                if (save.unlocks.Contains(u.id)) { unlocked.Add(u.ValueId); shown.Add((u.ValueId, null)); }
                else shown.Add((u.ValueId, u));
            }
            var lockSprite = lockSprite0;
            float x = -(shown.Count * 220 - 20) / 2f;
            foreach (var (id, lockedBy) in shown)
            {
                var cls = GameDb.Class(id);
                bool locked = lockedBy != null;
                var btn = UiKit.Button(layer, x, 312, 200, 200, "", locked ? 0x14121f : 0x1b1830, () =>
                {
                    if (!locked) { StartRun(id, stageId); return; }
                    if (save.stardust < lockedBy.cost) { Flash(layer, GameDb.Str("unlock.notEnough")); return; }
                    Buy(save, lockedBy);
                }, 22, UiKit.TopCenter);
                var portrait = SpriteBank.Get($"{id}_front");
                if (portrait != null)
                {
                    var img = UiKit.Sprite(btn.transform, portrait, 0, 8, 130, 122, UiKit.TopCenter, UiKit.TopCenter);
                    if (locked) img.color = new Color(0.35f, 0.35f, 0.4f, 1f);
                }
                if (locked && lockSprite != null) UiKit.Sprite(btn.transform, lockSprite, 0, 40, 72, 72, UiKit.TopCenter, UiKit.TopCenter);
                UiKit.Text(btn.transform, 0, 142, cls.name, 24, locked ? 0x9aa0b4 : 0xfff4de, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
                UiKit.Text(btn.transform, 0, 174, locked ? $"✦ {lockedBy.cost} 解鎖" : cls.role, 12, locked ? 0xffd36b : 0xcfd4e6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
                x += 220;
            }
            string nextLine = GameController.NextUnlockLine(save);
            if (nextLine != null) UiKit.Text(layer, 0, 300, nextLine, 14, 0xffd36b, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            ShowUnlockShop(layer, save);
            if (Debug.isDebugBuild || Application.isEditor)
                UiKit.Button(layer, 0, -12, 150, 32, "種子開局", 0x3a3a4a, () => ShowSeedPanel(layer, unlocked), 14, UiKit.BottomCenter, new Vector2(0.5f, 0));
            if (Feedback.Available)
                UiKit.Button(layer, -170, -12, 150, 32, "封測問卷", 0x2f6fb5, () => Feedback.Open(BuildInfo.Number, "title", save.lastSeedCode ?? "-", 0), 14, UiKit.BottomCenter, new Vector2(0.5f, 0));
            UiKit.Text(layer, -16, -12, $"v{Application.version} ({BuildInfo.Number}) {BuildInfo.Commit}", 12, 0x9aa0b4, UiKit.BottomRight, UiKit.BottomRight, TextAnchor.LowerRight, false, 1);
#if BOLIKE_QA || DEVELOPMENT_BUILD || UNITY_EDITOR
            // Hidden QA entry for beta builds only (BuildIos sets BOLIKE_QA; the App Store menu item does not, since
            // App Review guideline 2.3.1 forbids undocumented features): tap the version label five times within
            // three seconds to open the seed panel with every class, so testers can replay a seed or try the mage/archer.
            var secret = UiKit.Button(layer, 0, 0, 260, 40, "", 0x000000, () =>
            {
                if (Time.unscaledTime - secretTapAt > 3f) secretTaps = 0;
                secretTapAt = Time.unscaledTime;
                if (++secretTaps >= 5) { secretTaps = 0; ShowSeedPanel(layer, new System.Collections.Generic.List<string>(System.Linq.Enumerable.Select(GameDb.Classes, c => c.id))); }
            }, 12, UiKit.BottomRight, UiKit.BottomRight);
            secret.image.color = new Color(0, 0, 0, 0);
            secret.transition = UnityEngine.UI.Selectable.Transition.None;
#endif
            UiKit.Text(layer, 16, -12, TouchControls.IsTouchDevice ? "點選職業開始" : "點選職業開始 · 鍵盤 WASD 移動 J 攻擊 K 跳 L 防禦 UIOP 技能", 12, 0x9aa0b4, UiKit.BottomLeft, UiKit.BottomLeft, TextAnchor.LowerLeft, false, 1);
        }

        void ShowUnlockShop(RectTransform layer, SaveData save)
        {
            float y = 540;
            UiKit.Text(layer, 0, y - 30, "星塵解鎖", 18, 0xcfe8ff, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            int i = 0;
            foreach (var u in GameDb.Unlocks)
            {
                bool owned = save.unlocks.Contains(u.id);
                bool afford = save.stardust >= u.cost;
                var unlock = u;
                float bx = -(GameDb.Unlocks.Count * 180 - 20) / 2f + i * 180;
                var btn = UiKit.Button(layer, bx, y, 160, 70, "", owned ? 0x2f6fb5 : afford ? 0xf28c28 : 0x3a3a4a, () =>
                {
                    if (owned) return;
                    if (save.stardust < unlock.cost) { Flash(layer, GameDb.Str("unlock.notEnough")); return; }
                    Buy(save, unlock);
                }, 16, UiKit.TopCenter);
                UiKit.Text(btn.transform, 0, 8, u.name, 16, 0xffffff, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true, 1);
                UiKit.Text(btn.transform, 0, 30, owned ? "已解鎖" : $"✦ {u.cost}", 13, 0xffffff, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, false, 1);
                UiKit.Text(btn.transform, 0, 48, u.desc, 10, 0xe0e0e0, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, false, 1);
                i++;
            }
        }

        /// <summary>Spends stardust on an unlock and rebuilds the title with a short purchase flash.</summary>
        void Buy(SaveData save, UnlockDef unlock)
        {
            save.stardust -= unlock.cost;
            save.unlocks.Add(unlock.id);
            SaveSystem.Save();
            AudioManager.Instance.Play("buff");
            Destroy(title);
            ShowTitle();
            Flash(titleLayer, $"已解鎖：{unlock.name}");
        }

        RectTransform titleLayer;

        Text flashText;
        float flashUntil;

        /// <summary>One-line notice on the title screen (not enough stardust, purchase done) that fades after a moment.</summary>
        void Flash(RectTransform layer, string msg)
        {
            if (flashText == null || flashText.transform.parent != layer)
                flashText = UiKit.Text(layer, 0, 622, "", 18, 0xfff2b3, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true, 2);
            flashText.text = msg;
            UiKit.SetAlpha(flashText, 1);
            flashUntil = Time.unscaledTime + 2.2f;
        }

        void Update()
        {
            if (flashText != null && flashUntil > 0 && Time.unscaledTime > flashUntil - 0.5f)
                UiKit.SetAlpha(flashText, Mathf.Clamp01((flashUntil - Time.unscaledTime) / 0.5f));
        }

        /// <summary>Debug/QA: start a run from a seed code so a reported run can be replayed (editor and development builds).</summary>
        int secretTaps;
        float secretTapAt;

        void ShowSeedPanel(RectTransform layer, System.Collections.Generic.List<string> classes)
        {
            var dim = UiKit.Dim(layer, 0.75f);
            var panel = dim.transform;
            UiKit.Text(panel, 0, 200, "輸入 8 碼種子碼（留空＝隨機）", 24, 0xfff4de, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
            var box = UiKit.Rect(panel, 0, 250, 320, 56, 0xffffff, 0.95f, UiKit.TopCenter, UiKit.TopCenter);
            box.raycastTarget = true;
            var text = UiKit.Text(box.transform, 12, 0, "", 28, 0x111111, UiKit.TopLeft, UiKit.TopLeft, TextAnchor.MiddleLeft, true, 0);
            var textRt = text.rectTransform; textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one; textRt.offsetMin = new Vector2(12, 0); textRt.offsetMax = new Vector2(-12, 0);
            var field = box.gameObject.AddComponent<UnityEngine.UI.InputField>();
            field.textComponent = text;
            field.characterLimit = 8;
            field.contentType = UnityEngine.UI.InputField.ContentType.Alphanumeric;
            field.text = SaveSystem.Current.lastSeedCode ?? "";
            float x = -(classes.Count * 170 - 10) / 2f;
            foreach (var id in classes)
            {
                var cid = id;
                UiKit.Button(panel, x, 330, 160, 50, GameDb.Class(id).name, 0x2f6fb5, () =>
                {
                    var code = field.text.Trim();
                    Destroy(title);
                    GameController.Launch(cid, Progress.NextStage(SaveSystem.Current, Progress.DefaultChapter(SaveSystem.Current)).id, code.Length == 0 ? 0 : Rng.CodeToSeed(code));
                }, 20, UiKit.TopCenter);
                x += 170;
            }
            UiKit.Button(panel, 0, 400, 160, 44, "取消", 0x7a2a2a, () => Destroy(dim.gameObject), 18, UiKit.TopCenter);
            field.ActivateInputField();
        }

        void StartRun(string classId, string stageId)
        {
            Destroy(title);
            GameController.Launch(classId, stageId);
        }

        /// <summary>Name of the boss whose first kill opens a chapter, for the locked label.</summary>
        static string UnlockBossName(ChapterDef c)
        {
            string id = c.unlock?.bossKill;
            if (string.IsNullOrEmpty(id)) return "";
            try { return GameDb.Enemy(id).name; } catch (System.Collections.Generic.KeyNotFoundException) { return id; }
        }
    }

    /// <summary>Build number and commit shown on the title screen; filled in by the build script.</summary>
    public static class BuildInfo
    {
        public static string Number => Resources.Load<TextAsset>("build-number")?.text.Trim() ?? "dev";
        public static string Commit => Resources.Load<TextAsset>("build-commit")?.text.Trim() ?? "";
    }
}
