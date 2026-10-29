using UnityEngine;

namespace BoLike.Systems
{
    /// <summary>
    /// Beta feedback target from Resources/feedback-url.txt: a form URL or a mailto:. Placeholders {build} {class}
    /// {seed} {room} {info} are substituted URL-escaped; a URL without placeholders gets "?entry_info=…" appended.
    /// The info line is also copied to the clipboard so testers can paste it anywhere.
    /// </summary>
    public static class Feedback
    {
        static string url;
        static bool loaded;

        public static string Url
        {
            get
            {
                if (!loaded) { loaded = true; url = Resources.Load<TextAsset>("feedback-url")?.text.Trim(); }
                return string.IsNullOrEmpty(url) ? null : url;
            }
        }

        public static bool Available => Url != null;

        public static void Open(string build, string classId, string seed, int room)
        {
            var u = Url;
            if (u == null) return;
            string info = $"build {build} {classId} seed {seed} room {room}";
            string full = u.Contains("{")
                ? u.Replace("{build}", System.Uri.EscapeDataString(build))
                   .Replace("{class}", System.Uri.EscapeDataString(classId))
                   .Replace("{seed}", System.Uri.EscapeDataString(seed))
                   .Replace("{room}", room.ToString())
                   .Replace("{info}", System.Uri.EscapeDataString(info))
                : u + (u.Contains("?") ? "&" : "?") + "entry_info=" + System.Uri.EscapeDataString(info);
            GUIUtility.systemCopyBuffer = info;
            Application.OpenURL(full);
        }
    }
}
