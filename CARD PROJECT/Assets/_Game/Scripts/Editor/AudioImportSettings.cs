#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LordOfTheRealms.EditorTools
{
    // Postavlja import postavke za sve klipove u Resources/Audio, da se ne moraju
    // klikati u Inspectoru. Radi se automatski pri uvozu datoteke.
    //
    // Glazba je duga i pusta se jednom, pa ide Streaming: Unity je cita s diska
    // umjesto da drzi cijelu pjesmu u memoriji. Osmominutni komad u punom PCM-u
    // zauzeo bi vise od osamdeset megabajta RAM-a.
    //
    // Zvucni efekti su kratki i pustaju se cesto, pa idu DecompressOnLoad uz
    // preload: dekodiranje u trenutku klika culo bi se kao kasnjenje.
    public class AudioImportSettings : AssetPostprocessor
    {
        private const string AudioDir = "Assets/Resources/Audio/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioDir)) return;

            var importer = (AudioImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool isMusic = name.StartsWith("Music_");

            var s = importer.defaultSampleSettings;
            s.loadType = isMusic
                ? AudioClipLoadType.Streaming
                : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = isMusic
                ? AudioCompressionFormat.Vorbis
                : AudioCompressionFormat.PCM;
            s.quality = 0.7f;
            s.preloadAudioData = !isMusic;
            importer.defaultSampleSettings = s;

            // glazba ostaje stereo, efekti su ionako mono
            importer.forceToMono = false;
            importer.loadInBackground = isMusic;
            importer.ambisonic = false;
        }
    }
}
#endif
