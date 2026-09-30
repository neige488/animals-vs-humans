using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
namespace AvH.Editor {
 public static partial class ConfigureOwnedAssets {
  const string Root="Assets/ThirdParty/";
  const string Generated=Root+"Generated/";
  static readonly Dictionary<Material,Material> Converted = new Dictionary<Material,Material>();
  public static void Configure() {
   Converted.Clear();
   Directory.CreateDirectory(Generated);Directory.CreateDirectory(Root+"Resources");AssetDatabase.Refresh();
   // The purchased Synty shader graph uses its own albedo property. Preserve texture mapping in built-in rendering.
   foreach(var file in Directory.GetFiles(Root,"*.mat",SearchOption.AllDirectories).Where(p=>!p.StartsWith(Generated))) {
    var mat=AssetDatabase.LoadAssetAtPath<Material>(file);if(mat==null)continue;
    var text=File.ReadAllText(file);var match=Regex.Match(text,@"- _Albedo_Map:\s*m_Texture: \{fileID: \d+, guid: ([a-f0-9]{32})");
    if(!match.Success)continue;
    var tex=AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value));
    var convertedPath=Generated+Path.GetFileNameWithoutExtension(file)+"_Builtin.mat";
    var converted=AssetDatabase.LoadAssetAtPath<Material>(convertedPath);
    if(converted==null){converted=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(converted,convertedPath);}
    converted.mainTexture=tex;converted.color=Color.white;converted.SetFloat("_Glossiness",.15f);EditorUtility.SetDirty(converted);Converted[mat]=converted;
   }
   var humanFiles=new[]{"man_casual","woman_casual","man_fire","woman_police","man_construction_worker","woman_explorer"};
   var humanNames=new[]{"산책객","여행객","소방관","경찰관","건설 작업자","탐험가"};
   var animalFiles=new[]{"Fox","Wolf","Bear_Grizzly","Boar","Rabbit_Brown","Penguin"};
   var animalNames=new[]{"여우","늑대","불곰","멧돼지","토끼","펭귄"};
   var humans=humanFiles.Select((file,i)=>Owned(file,humanNames[i],true)).ToArray();
   var animals=animalFiles.Select((file,i)=>Owned(file,animalNames[i],false)).ToArray();
   var map=BuildVillage();
   var village=PrefabUtility.SaveAsPrefabAsset(map,Generated+"Village.prefab");UnityEngine.Object.DestroyImmediate(map);
   var path=Root+"Resources/OwnedAssetCatalog.asset";
   var catalog=AssetDatabase.LoadAssetAtPath<OwnedAssetCatalog>(path);
   if(catalog==null){catalog=ScriptableObject.CreateInstance<OwnedAssetCatalog>();AssetDatabase.CreateAsset(catalog,path);}
   catalog.ShelterPoints=PrototypeVillage.DefaultShelters.ToArray();
   catalog.Humans=humans;catalog.Animals=animals;catalog.Human=humans[0].Prefab;catalog.Animal=animals[0].Prefab;catalog.Village=village;
   catalog.AnimalDisplayName="여우";catalog.Rarity="일반";catalog.RarityColor=new Color(.85f,.95f,1);
   catalog.HumanSource="Polyperfect Low Poly Animated People 3.02 / man_casual";
   catalog.AnimalSource="Polyperfect Low Poly Animated Animals 4.1.1 / Fox";
   catalog.VillageSource="Synty POLYGON Adventure 1.8.2 / village 01/02, wall 01, tree 05 + Generic base floor 01";
   EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();Debug.Log("Owned assets configured: six humans, six animals and Synty village; source assets remain Git ignored.");
  }
  static OwnedCharacter Owned(string file,string display,bool human) {
   var source=human?"polyperfect/Low Poly Animated People/- Prefabs/":"polyperfect/Low Poly Animated Animals/Prefabs/Animals/";
   return new OwnedCharacter{Id=(human?"human-":"animal-")+file.ToLowerInvariant(),DisplayName=display,Source=source+file,Prefab=Character(source+file+".prefab",(human?"Human_":"Animal_")+file,human?1.8f:file=="Rabbit_Brown"?.65f:(float?)null)};
  }
  static GameObject Load(string path) {var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+path);if(prefab==null)throw new InvalidOperationException("Missing owned prefab: "+path);return prefab;}
  static GameObject Character(string path,string name,float? height) {
   var instance=UnityEngine.Object.Instantiate(Load(path));Clean(instance);
   foreach(var animator in instance.GetComponentsInChildren<Animator>()) {
    if(name.StartsWith("Human_") && animator.runtimeAnimatorController is AnimatorOverrideController demo)
     animator.runtimeAnimatorController=demo.runtimeAnimatorController;
    animator.fireEvents=false;animator.applyRootMotion=false;animator.Rebind();animator.Update(0);animator.Play("Idle",0,0);animator.Update(.1f);
   }
   var bounds=PoseBounds(instance);
   var holder=new GameObject(name);
   instance.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
   instance.transform.SetParent(holder.transform,true);
   // Preserve the fox rig's authored metre scale; its skinned node already has a 100x import conversion.
   // Normalize humans outside the Animator hierarchy.
   holder.transform.localScale=Vector3.one*(height.HasValue?height.Value/bounds.size.y:1f);
   foreach(var animator in holder.GetComponentsInChildren<Animator>()){animator.fireEvents=false;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
   foreach(var collider in holder.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
   foreach(var body in holder.GetComponentsInChildren<Rigidbody>())UnityEngine.Object.DestroyImmediate(body);
   var result=PrefabUtility.SaveAsPrefabAsset(holder,Generated+name+".prefab");UnityEngine.Object.DestroyImmediate(holder);return result;
  }
  static void Clean(GameObject go){
   // Some vendor rigs nest an unused Animator below the actual controller.
   foreach(var animator in go.GetComponentsInChildren<Animator>(true))if(animator.runtimeAnimatorController==null)UnityEngine.Object.DestroyImmediate(animator);
   foreach(var component in go.GetComponentsInChildren<Component>(true))
    if(component!=null && component.GetType().FullName=="UnityEngine.AI.NavMeshAgent")UnityEngine.Object.DestroyImmediate(component);
   foreach(var renderer in go.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m!=null && Converted.TryGetValue(m,out var converted)?converted:m).ToArray();
   foreach(var t in go.GetComponentsInChildren<Transform>(true)){GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);foreach(var behaviour in t.GetComponents<MonoBehaviour>())UnityEngine.Object.DestroyImmediate(behaviour);}}
  static Bounds PoseBounds(GameObject go) {
   var meshes=go.GetComponentsInChildren<SkinnedMeshRenderer>();if(meshes.Length==0)return Bounds(go);
   Bounds bounds=default;bool first=true;
   foreach(var renderer in meshes) {
    var mesh=new Mesh();renderer.BakeMesh(mesh,true);
    foreach(var vertex in mesh.vertices){var world=renderer.transform.TransformPoint(vertex);if(first){bounds=new Bounds(world,Vector3.zero);first=false;}else bounds.Encapsulate(world);}
    UnityEngine.Object.DestroyImmediate(mesh);
   }
   return bounds;
  }
  static Bounds Bounds(GameObject go){var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new InvalidOperationException("Owned prefab has no renderer");var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
 }
}
