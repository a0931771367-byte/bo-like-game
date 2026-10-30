#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace BoLike.Editor
{
    /// <summary>
    /// Info.plist additions for the exported Xcode project. ITSAppUsesNonExemptEncryption = NO declares that the app
    /// adds no encryption of its own (it only opens HTTPS links in the system browser), so App Store Connect stops
    /// asking the export-compliance question on every TestFlight build.
    /// </summary>
    public static class IosPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
