using UnityEditor;
using UnityEngine;
namespace AvH.Editor {
 /// <summary>
 /// Import settings for the committed CC0 sounds: positional effects are forced to mono (clean 3D panning, half the memory);
 /// music and ambience loops stream compressed instead of being decoded into memory.
 /// </summary>
 public sealed class AudioImportRules : AssetPostprocessor {
  const string Folder="Assets/AvH/Resources/Audio/";
  public override uint GetVersion()=>2;
  void OnPreprocessAudio() {
   if(!assetPath.StartsWith(Folder))return;
   var importer=(AudioImporter)assetImporter;var name=System.IO.Path.GetFileNameWithoutExtension(assetPath);
   bool bed=name.StartsWith("music-")||name.StartsWith("ambience");
   var settings=importer.defaultSampleSettings;settings.compressionFormat=AudioCompressionFormat.Vorbis;
   settings.loadType=bed?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;settings.quality=bed?.5f:.7f;settings.preloadAudioData=!bed;
   importer.defaultSampleSettings=settings;importer.forceToMono=!bed;importer.loadInBackground=bed;
  }
  /// <summary>Batch entry point: reimports the folder so the rules apply to existing files.</summary>
  public static void Reimport(){foreach(var guid in AssetDatabase.FindAssets("t:AudioClip",new[]{Folder.TrimEnd('/')}))AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),ImportAssetOptions.ForceUpdate);AssetDatabase.SaveAssets();}
 }
}
