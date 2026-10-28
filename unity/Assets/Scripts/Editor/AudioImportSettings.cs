using UnityEditor;
using UnityEngine;

namespace BoLike.Editor
{
    /// <summary>
    /// Resources/Audio/music and amb: streamed Vorbis (long tracks, low memory).
    /// Resources/Audio/sfx: decompressed on load (short, latency-free), mono.
    /// </summary>
    public class AudioImportSettings : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            bool longForm = assetPath.Contains("/music/") || assetPath.Contains("/amb/");
            var s = importer.defaultSampleSettings;
            if (longForm)
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.forceToMono = false;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
                s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                s.sampleRateOverride = 44100;
                importer.forceToMono = true;
            }
            s.preloadAudioData = !longForm;
            importer.defaultSampleSettings = s;
            importer.loadInBackground = longForm;
        }
    }
}
