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
            var layer = UiKit.Layer(canvas, "Layer");
            UiKit.Text(layer, 0, 90, "口袋冒險團", 64, 0xfff4de, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true, 4);
            UiKit.Text(layer, 0, 170, "Pocket Adventurers · 封測版", 20, 0xcfd4e6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            UiKit.Text(layer, 0, 215, $"✦ 星塵 {save.stardust}", 22, 0xcfe8ff, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);

            var unlocked = new System.Collections.Generic.List<string>(GameDb.StartingClasses);
            foreach (var u in GameDb.Unlocks)
                if (u.kind == UnlockKind.Class && save.unlocks.Contains(u.id) && !unlocked.Contains(u.ValueId)) unlocked.Add(u.ValueId);
            float x = -(unlocked.Count * 220 - 20) / 2f;
            foreach (var id in unlocked)
            {
                var cls = GameDb.Class(id);
                var btn = UiKit.Button(layer, x, 290, 200, 220, "", 0x1b1830, () => StartRun(id), 22, UiKit.TopCenter);
                var portrait = SpriteBank.Get($"{id}_front");
                if (portrait != null) UiKit.Sprite(btn.transform, portrait, 0, 14, 150, 140, UiKit.TopCenter, UiKit.TopCenter);
                UiKit.Text(btn.transform, 0, 165, cls.name, 24, 0xfff4de, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
                UiKit.Text(btn.transform, 0, 195, cls.role, 12, 0xcfd4e6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
                x += 220;
            }
            ShowUnlockShop(layer, save);
            if (Debug.isDebugBuild || Application.isEditor)
                UiKit.Button(layer, 0, -12, 150, 32, "種子開局", 0x3a3a4a, () => ShowSeedPanel(layer, unlocked), 14, UiKit.BottomCenter, new Vector2(0.5f, 0));
            UiKit.Text(layer, -16, -12, $"v{Application.version} ({BuildInfo.Number}) {BuildInfo.Commit}", 12, 0x9aa0b4, UiKit.BottomRight, UiKit.BottomRight, TextAnchor.LowerRight, false, 1);
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
                    if (owned || save.stardust < unlock.cost) return;
                    save.stardust -= unlock.cost;
                    save.unlocks.Add(unlock.id);
                    SaveSystem.Save();
                    Destroy(title);
                    ShowTitle();
                }, 16, UiKit.TopCenter);
                UiKit.Text(btn.transform, 0, 8, u.name, 16, 0xffffff, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true, 1);
                UiKit.Text(btn.transform, 0, 30, owned ? "已解鎖" : $"✦ {u.cost}", 13, 0xffffff, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, false, 1);
                UiKit.Text(btn.transform, 0, 48, u.desc, 10, 0xe0e0e0, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, false, 1);
                i++;
            }
        }

        /// <summary>Debug/QA: start a run from a seed code so a reported run can be replayed (editor and development builds).</summary>
        void ShowSeedPanel(RectTransform layer, System.Collections.Generic.List<string> classes)
        {
            var dim = UiKit.Dim(layer, 0.75f);
            var panel = dim.transform;
            UiKit.Text(panel, 0, 200, "輸入 8 碼種子碼", 24, 0xfff4de, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
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
                    if (code.Length == 0) return;
                    Destroy(title);
                    GameController.Launch(cid, Rng.CodeToSeed(code));
                }, 20, UiKit.TopCenter);
                x += 170;
            }
            UiKit.Button(panel, 0, 400, 160, 44, "取消", 0x7a2a2a, () => Destroy(dim.gameObject), 18, UiKit.TopCenter);
            field.ActivateInputField();
        }

        void StartRun(string classId)
        {
            Destroy(title);
            GameController.Launch(classId);
        }
    }

    /// <summary>Build number and commit shown on the title screen; filled in by the build script.</summary>
    public static class BuildInfo
    {
        public static string Number => Resources.Load<TextAsset>("build-number")?.text.Trim() ?? "dev";
        public static string Commit => Resources.Load<TextAsset>("build-commit")?.text.Trim() ?? "";
    }
}
