using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEngine;
namespace AvH.Editor {
 public static class ConfigureFishNet {
  public static void Configure(){
   const string resources="Assets/AvH/Resources";
   if(!AssetDatabase.IsValidFolder(resources))AssetDatabase.CreateFolder("Assets/AvH","Resources");
   var player=new GameObject("FishNetPlayer");var nob=player.AddComponent<NetworkObject>();player.AddComponent<FishPredictedMotor>();
   var serialized=new SerializedObject(nob);serialized.FindProperty("_enablePrediction").boolValue=true;serialized.FindProperty("_enableStateForwarding").boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();
   var prefab=PrefabUtility.SaveAsPrefabAsset(player,resources+"/FishNetPlayer.prefab");Object.DestroyImmediate(player);
   var objects=AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(resources+"/FishNetPrefabs.asset");if(objects==null){objects=ScriptableObject.CreateInstance<SinglePrefabObjects>();AssetDatabase.CreateAsset(objects,resources+"/FishNetPrefabs.asset");}
   objects.Clear();objects.AddObject(prefab.GetComponent<NetworkObject>());EditorUtility.SetDirty(objects);
   var root=new GameObject("FishNetRoomManager");root.SetActive(false);root.AddComponent<Tugboat>();var manager=root.AddComponent<NetworkManager>();manager.SpawnablePrefabs=objects;
   serialized=new SerializedObject(manager);serialized.FindProperty("_dontDestroyOnLoad").boolValue=false;serialized.FindProperty("_persistence").enumValueIndex=2;serialized.ApplyModifiedPropertiesWithoutUndo();
   root.SetActive(true);PrefabUtility.SaveAsPrefabAsset(root,resources+"/FishNetRoomManager.prefab");Object.DestroyImmediate(root);
   AssetDatabase.SaveAssets();Debug.Log("AVH FishNet prediction prefabs configured.");
  }
 }
}
