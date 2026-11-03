using System;
using System.Collections.Generic;
using BoLike.Data;
using BoLike.Game;
using UnityEngine;
using UnityEngine.UI;

namespace BoLike.UI
{
    /// <summary>
    /// Story dialogue (S4, design doc 07 §6): a box along the bottom with the speaker's portrait, name and text typed
    /// out two characters at a time; tap to finish the line or go to the next, 跳過 at the top right skips the rest.
    /// Narrator lines have no portrait; the player's portrait is the class front view, enemies use their side sprite.
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        List<StoryLine> lines;
        string playerName, playerPortrait;
        Action onDone;
        int index = -1;
        Text body, speaker;
        Image portraitImg, box, hint;
        string full = "";
        int shown;
        float nextCharAt;
        const float CharsPerSecond = 40f;
        bool finished;

        public static DialogueBox Show(RectTransform layer, List<StoryLine> lines, string playerName, string playerPortrait, Action onDone)
        {
            var go = new GameObject("Dialogue");
            go.transform.SetParent(layer, false);
            var d = go.AddComponent<DialogueBox>();
            d.lines = lines;
            d.playerName = playerName;
            d.playerPortrait = playerPortrait;
            d.onDone = onDone;
            d.Build(layer);
            d.Next();
            return d;
        }

        void Build(RectTransform layer)
        {
            // Tap anywhere: finish the current line, then advance.
            var tap = UiKit.Button(layer, 0, 0, 10, 10, "", 0x000000, Advance, 12);
            var tr = tap.image.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            tap.image.color = new Color(0, 0, 0, 0);
            tap.transition = Selectable.Transition.None;

            // Soft shadow under the box, then the box itself with an accent rule along the top.
            var shadow = UiKit.Rect(layer, 0, -16, 1196, 212, 0x000000, 0.35f, UiKit.BottomCenter, UiKit.BottomCenter);
            shadow.raycastTarget = false;
            box = UiKit.Rect(layer, 0, -24, 1180, 200, 0x14121f, 0.94f, UiKit.BottomCenter, UiKit.BottomCenter);
            box.raycastTarget = false;
            UiKit.Rect(box.transform, 0, 0, 1180, 3, 0xf28c28, 1f, UiKit.TopCenter, UiKit.TopCenter).raycastTarget = false;
            UiKit.Rect(box.transform, 0, 3, 1180, 24, 0xf28c28, 0.10f, UiKit.TopCenter, UiKit.TopCenter).raycastTarget = false;
            portraitImg = UiKit.Sprite(layer, SpriteBank.Pixel, -470, -40, 240, 240, UiKit.BottomCenter, UiKit.BottomCenter);
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;
            speaker = UiKit.Text(box.transform, 230, 18, "", 24, 0xffd36b, UiKit.TopLeft, UiKit.TopLeft, TextAnchor.UpperLeft, true);
            speaker.raycastTarget = false;
            body = UiKit.Text(box.transform, 230, 58, "", 26, 0xfff4de, UiKit.TopLeft, UiKit.TopLeft, TextAnchor.UpperLeft);
            body.rectTransform.sizeDelta = new Vector2(880, 130);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.lineSpacing = 1.15f;
            body.raycastTarget = false;
            hint = UiKit.Rect(box.transform, -24, -16, 14, 14, 0xffd36b, 1f, UiKit.BottomRight, UiKit.BottomRight);
            hint.raycastTarget = false;
            UiKit.Button(layer, -24, 24, 120, 40, GameDb.Str("story.skip"), 0x3a3a4a, Skip, 16, UiKit.TopRight, UiKit.TopRight);
        }

        void Advance()
        {
            if (finished) return;
            if (shown < full.Length) { shown = full.Length; body.text = full; return; }
            Next();
        }

        void Next()
        {
            index++;
            if (index >= lines.Count) { Finish(); return; }
            var line = lines[index];
            string name = line.who == "player" ? playerName : line.who == "narrator" ? "" : line.name ?? "";
            speaker.text = name;
            full = (line.text ?? "").Replace("{name}", playerName);
            shown = 0;
            body.text = "";
            nextCharAt = Time.unscaledTime;
            string key = line.who == "player" ? playerPortrait : line.who == "narrator" ? null : line.portrait;
            var sprite = string.IsNullOrEmpty(key) ? null : SpriteBank.Get(key);
            portraitImg.enabled = sprite != null;
            if (sprite != null) portraitImg.sprite = sprite;
            // Enemies speak from the right, the player and NPCs from the left; the text column follows. Lines without a
            // portrait (narrator, NPCs with no art yet) use the full width instead of leaving a blank column.
            bool right = line.who == "enemy";
            float textX = sprite == null ? 48 : right ? 60 : 230;
            float textW = sprite == null ? 1084 : 880;
            portraitImg.rectTransform.anchoredPosition = new Vector2(right ? 470 : -470, 40);
            speaker.rectTransform.anchoredPosition = new Vector2(textX, -18);
            body.rectTransform.anchoredPosition = new Vector2(textX, -58);
            body.rectTransform.sizeDelta = new Vector2(textW, 130);
            AudioManager.Instance.Play(line.who == "narrator" ? "dialogNarrator" : "dialogBlip");
        }

        void Update()
        {
            if (finished) return;
            if (shown < full.Length && Time.unscaledTime >= nextCharAt)
            {
                shown = Mathf.Min(full.Length, shown + 2);
                body.text = full.Substring(0, shown);
                nextCharAt = Time.unscaledTime + 2f / CharsPerSecond;
                AudioManager.Instance.Play("dialogBlip");
            }
            hint.enabled = shown >= full.Length && (Time.unscaledTime * 2f) % 1f < 0.6f;
        }

        void Skip() { if (!finished) Finish(); }

        void Finish()
        {
            finished = true;
            var cb = onDone;
            onDone = null;
            cb?.Invoke();
        }
    }
}
