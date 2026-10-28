using System.Collections.Generic;
using BoLike.Core;
using BoLike.Sim;
using BoLike.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace BoLike.UI
{
    /// <summary>Port of ui/Hud.ts: bars, skill cooldowns, potions, boss bar, toast, GO arrow, combo and banners.</summary>
    public class Hud : MonoBehaviour
    {
        World world;
        Canvas canvas;
        RectTransform layer;
        Image hpBar, spBar, expBar, bossBar, bossFrame, flashImg;
        Text hpText, spText, lvText, goldText, potText, buffText, toastText, goText, comboText, bossName, stardustText;
        readonly List<(Image bg, Image cover, Image label, Text key)> skillCd = new();
        float toastUntil, comboUntil, comboPop, flashUntil, flashDur, flashAlpha;
        Enemy boss;
        readonly List<(Text t, float until, float start)> banners = new();
        public System.Func<int> Stardust;

        public static Hud Create(World world, bool touch)
        {
            var go = new GameObject("Hud");
            var h = go.AddComponent<Hud>();
            h.world = world;
            h.Build(touch);
            return h;
        }

        Player P => world.Player;

        void Build(bool touch)
        {
            canvas = UiKit.Canvas("HudCanvas", 10);
            canvas.transform.SetParent(transform, false);
            layer = UiKit.Layer(canvas, "Layer");
            float x0 = 24, y0 = 18;
            UiKit.Rect(layer, x0, y0, 330, 86, 0x0b0d14, 0.55f);
            UiKit.Rect(layer, x0 + 10, y0 + 32, 300, 16, 0x3a1020);
            hpBar = UiKit.Rect(layer, x0 + 10, y0 + 32, 300, 16, 0xff4d5a);
            UiKit.Rect(layer, x0 + 10, y0 + 54, 300, 10, 0x10203a);
            spBar = UiKit.Rect(layer, x0 + 10, y0 + 54, 300, 10, 0x4da3ff);
            UiKit.Rect(layer, x0 + 10, y0 + 70, 300, 5, 0x2a2a1a);
            expBar = UiKit.Rect(layer, x0 + 10, y0 + 70, 0, 5, Colors.Gold);
            lvText = UiKit.Text(layer, x0 + 10, y0 + 4, "", 18, 0xffffff, bold: true);
            hpText = UiKit.Text(layer, x0 + 310, y0 + 31, "", 13, 0xffffff, pivot: UiKit.TopRight);
            spText = UiKit.Text(layer, x0 + 310, y0 + 51, "", 11, 0xffffff, pivot: UiKit.TopRight);
            goldText = UiKit.Text(layer, x0 + 200, y0 + 6, "", 15, Colors.Gold);
            stardustText = UiKit.Text(layer, x0 + 260, y0 + 6, "", 15, 0xcfe8ff);
            potText = UiKit.Text(layer, -24, 22, "", 16, 0xffffff, anchor: UiKit.TopRight, pivot: UiKit.TopRight, align: TextAnchor.UpperRight);
            buffText = UiKit.Text(layer, x0 + 4, y0 + 92, "", 14, Colors.Buff);
            comboText = UiKit.Text(layer, -40, 250, "", 40, Colors.Crit, anchor: UiKit.TopRight, pivot: UiKit.TopRight, align: TextAnchor.UpperRight, bold: true, outline: 3);
            UiKit.SetAlpha(comboText, 0);

            var cls = P.cls;
            string[] keys = { "U", "I", "O", "P" };
            for (int i = 0; i < cls.skills.Count; i++)
            {
                float bx = -150 + i * 76 - 32, by = touch ? -200 : 20; // off-screen when touch controls draw their own
                var bg = UiKit.Rect(layer, bx, by, 64, 64, 0x000000, 0.4f, UiKit.TopCenter);
                var label = UiKit.Sprite(layer, Game.SpriteBank.Icon(cls.id, i), bx + 2, by + 2, 60, 60, UiKit.TopCenter);
                var cover = UiKit.Rect(layer, bx, by + 64, 64, 0, 0x000000, 0.65f, UiKit.TopCenter, UiKit.BottomLeft);
                var key = UiKit.Text(layer, bx + 4, by + 2, keys[i], 12, 0xffffff, UiKit.TopCenter);
                skillCd.Add((bg, cover, label, key));
            }
            if (!touch) UiKit.Text(layer, 0, 92, "J 攻擊  K 跳躍  L 防禦(+方向閃躲)  1/2 藥水  Esc 選單", 13, 0xcfd4e6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);

            toastText = UiKit.Text(layer, 0, 150, "", 20, 0xfff2b3, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter);
            UiKit.SetAlpha(toastText, 0);
            goText = UiKit.Text(layer, -60, 300, "GO →", 44, Colors.Crit, UiKit.TopRight, UiKit.TopRight, TextAnchor.UpperRight, true);
            UiKit.SetAlpha(goText, 0);

            flashImg = UiKit.Rect(layer, 0, 0, 10, 10, 0xffffff, 0f);
            var frt = flashImg.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        }

        public void ShowBoss(Enemy e)
        {
            boss = e;
            float y = 118;
            bossFrame = UiKit.Rect(layer, 0, y, 620, 22, 0x000000, 0.6f, UiKit.TopCenter, UiKit.TopCenter);
            bossBar = UiKit.Rect(layer, -308, y + 3, 616, 16, 0xc0304a, 1f, UiKit.TopCenter, UiKit.TopLeft);
            bossName = UiKit.Text(layer, 0, y - 26, e.def.name, 18, 0xffb3c6, UiKit.TopCenter, UiKit.TopCenter, TextAnchor.UpperCenter, true);
        }

        public void Toast(string msg)
        {
            toastText.text = msg;
            UiKit.SetAlpha(toastText, 1);
            toastUntil = Time.time + 2.0f;
        }

        public void ShowGo(bool show) => UiKit.SetAlpha(goText, show ? 0.6f + Mathf.PingPong(Time.time * 2f, 0.4f) : 0);

        /// <summary>Shows the hit counter with a pop; fades when the chain breaks.</summary>
        public void Combo(int n)
        {
            if (n < 2) return;
            comboText.text = $"{n} HITS";
            UiKit.SetAlpha(comboText, 1);
            comboPop = 1.35f;
            comboUntil = Time.time + 1.8f;
        }

        public void Banner(string text, int color, int size)
        {
            var t = UiKit.Text(layer, 0, UiKit.H * 0.36f, text, size, color, UiKit.TopCenter, UiKit.Center, TextAnchor.MiddleCenter, true, 4);
            t.transform.SetAsLastSibling();
            banners.Add((t, Time.time + 1.9f, Time.time));
        }

        public void ScreenFlash(float alpha, float seconds)
        {
            flashAlpha = alpha; flashDur = seconds; flashUntil = Time.time + seconds;
            flashImg.transform.SetAsLastSibling();
        }

        void Update()
        {
            if (P == null) return;
            var p = P;
            hpBar.rectTransform.sizeDelta = new Vector2(300 * Mathf.Clamp01(p.hp / p.maxHp), 16);
            spBar.rectTransform.sizeDelta = new Vector2(300 * Mathf.Clamp01(p.sp / p.maxSp), 10);
            expBar.rectTransform.sizeDelta = new Vector2(300 * Mathf.Clamp01((float)p.prog.exp / StatFormulas.ExpToNext(p.prog.level)), 5);
            hpText.text = $"{Mathf.CeilToInt(p.hp)} / {Mathf.RoundToInt(p.maxHp)}";
            spText.text = $"{Mathf.FloorToInt(p.sp)} / {Mathf.RoundToInt(p.maxSp)}";
            lvText.text = $"{p.cls.name}  Lv.{p.prog.level}";
            goldText.text = $"{world.Inv.gold} G";
            stardustText.text = Stardust != null ? $"✦ {Stardust()}" : "";
            var sb = new System.Text.StringBuilder();
            foreach (var b in p.buffs)
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(b.name).Append(' ').Append(Mathf.CeilToInt((float)(b.until - p.Now) / 1000f)).Append('s');
                if (b.hits.HasValue) sb.Append(" ×").Append(b.hits.Value);
            }
            buffText.text = sb.ToString();
            potText.text = $"[1] 紅藥水 ×{world.Inv.red}    [2] 藍藥水 ×{world.Inv.blue}";
            string[] keys = { "U", "I", "O", "P" };
            for (int i = 0; i < skillCd.Count; i++)
            {
                var s = p.cls.skills[i];
                var (bg, cover, label, key) = skillCd[i];
                bool learned = p.SkillLv(s) > 0;
                UiKit.SetAlpha(bg, learned ? 0.4f : 0.2f);
                UiKit.SetAlpha(label, learned ? 1f : 0.3f);
                float left = p.CooldownLeft(s);
                cover.rectTransform.sizeDelta = new Vector2(64, left > 0 ? 64 * Mathf.Min(1, left / p.CooldownTotal(s)) : 0);
                key.text = learned ? $"{keys[i]} Lv{p.SkillLv(s)}" : keys[i];
            }
            if (boss != null && bossBar != null)
            {
                bossBar.rectTransform.sizeDelta = new Vector2(616 * Mathf.Clamp01(boss.hp / boss.maxHp), 16);
                if (boss.dead) { bossBar.enabled = false; bossFrame.enabled = false; bossName.enabled = false; }
            }
            if (toastUntil > 0 && Time.time > toastUntil - 0.4f) UiKit.SetAlpha(toastText, Mathf.Clamp01((toastUntil - Time.time) / 0.4f));
            if (comboUntil > 0)
            {
                comboPop = Mathf.Lerp(comboPop, 1f, Time.deltaTime * 15f);
                comboText.transform.localScale = Vector3.one * comboPop;
                if (Time.time > comboUntil - 0.3f) UiKit.SetAlpha(comboText, Mathf.Clamp01((comboUntil - Time.time) / 0.3f));
            }
            for (int i = banners.Count - 1; i >= 0; i--)
            {
                var (t, until, start) = banners[i];
                float age = Time.time - start;
                float inK = Mathf.Clamp01(age / 0.22f);
                float back = 1 + 2.7f * Mathf.Pow(inK - 1, 3) + 1.7f * Mathf.Pow(inK - 1, 2);
                t.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, back);
                float a = age < 0.22f ? inK : Mathf.Clamp01((until - Time.time) / 0.4f);
                UiKit.SetAlpha(t, a);
                if (Time.time > until) { Destroy(t.gameObject); banners.RemoveAt(i); }
            }
            float fl = flashUntil > Time.time ? flashAlpha * (flashUntil - Time.time) / flashDur : 0;
            UiKit.SetAlpha(flashImg, fl);
        }
    }
}
