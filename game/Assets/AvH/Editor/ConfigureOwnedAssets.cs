using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
namespace AvH.Editor {
 public static class ConfigureOwnedAssets {
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
   var human=Character("polyperfect/Low Poly Animated People/- Prefabs/man_casual.prefab","Human",1.8f);
   var animal=Character("polyperfect/Low Poly Animated Animals/Prefabs/Animals/Fox.prefab","Fox",1.0f);
   var map=new GameObject("Synty village playtest");PrototypeVillage.Build(map.transform,false);
   var layout=map.transform.GetChild(0);
   foreach(Transform block in layout.Cast<Transform>().ToArray()) {
    if(block.name=="Ground") {
     var groundPath=Generated+"Ground.mat";var ground=AssetDatabase.LoadAssetAtPath<Material>(groundPath);
     if(ground==null){ground=new Material(block.GetComponent<Renderer>().sharedMaterial);AssetDatabase.CreateAsset(ground,groundPath);}
     block.GetComponent<Renderer>().sharedMaterial=ground;continue;
    }
    var prefab=block.name.Contains("stair")||block.name.Contains("roof")||block.name.Contains("Rooftop")
     ?"Synty/PolygonGeneric/Prefabs/Base/SM_Bld_Base_Floor_01.prefab"
     :"Synty/PolygonAdventure/Prefabs/Buildings/SM_Bld_Wall_01.prefab";
    var visual=UnityEngine.Object.Instantiate(Load(prefab),map.transform);
    Clean(visual);Fit(visual,block.position,block.localScale);
    foreach(var collider in visual.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
    // Collision belongs to the simple, tested traversable footprint, decoration to the purchased mesh.
    UnityEngine.Object.DestroyImmediate(block.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(block.GetComponent<MeshFilter>());
   }
   Decoration(map,"Synty/PolygonAdventure/Prefabs/Buildings/SM_Bld_Village_01.prefab",new Vector3(-17,0,-15),new Vector3(6,6,6));
   Decoration(map,"Synty/PolygonAdventure/Prefabs/Buildings/SM_Bld_Village_02.prefab",new Vector3(17,0,-5),new Vector3(6,5,6));
   Decoration(map,"Synty/PolygonAdventure/Prefabs/Environments/SM_Env_Tree_05.prefab",new Vector3(-18,0,17),new Vector3(5,7,5));
   var village=PrefabUtility.SaveAsPrefabAsset(map,Generated+"Village.prefab");UnityEngine.Object.DestroyImmediate(map);
   var path=Root+"Resources/OwnedAssetCatalog.asset";
   var catalog=AssetDatabase.LoadAssetAtPath<OwnedAssetCatalog>(path);
   if(catalog==null){catalog=ScriptableObject.CreateInstance<OwnedAssetCatalog>();AssetDatabase.CreateAsset(catalog,path);}
   catalog.Human=human;catalog.Animal=animal;catalog.Village=village;
   catalog.AnimalDisplayName="여우";catalog.Rarity="일반";catalog.RarityColor=new Color(.85f,.95f,1);
   catalog.HumanSource="Polyperfect Low Poly Animated People 3.02 / man_casual";
   catalog.AnimalSource="Polyperfect Low Poly Animated Animals 4.1.1 / Fox";
   catalog.VillageSource="Synty POLYGON Adventure 1.8.2 / village 01/02, wall 01, tree 05 + Generic base floor 01";
   EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();Debug.Log("Owned assets configured: human, fox and Synty village; source assets remain Git ignored.");
  }
  static GameObject Load(string path) {var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+path);if(prefab==null)throw new InvalidOperationException("Missing owned prefab: "+path);return prefab;}
  static GameObject Character(string path,string name,float height) {
   var instance=UnityEngine.Object.Instantiate(Load(path));Clean(instance);
   foreach(var animator in instance.GetComponentsInChildren<Animator>()) {
    if(name=="Human" && animator.runtimeAnimatorController is AnimatorOverrideController demo)
     animator.runtimeAnimatorController=demo.runtimeAnimatorController;
    animator.applyRootMotion=false;animator.Rebind();animator.Update(0);animator.Play("Idle",0,0);animator.Update(0);
   }
   var bounds=PoseBounds(instance);instance.transform.localScale*=height/bounds.size.y;bounds=PoseBounds(instance);instance.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
   var holder=new GameObject(name);instance.transform.SetParent(holder.transform,true);
   foreach(var animator in holder.GetComponentsInChildren<Animator>()){animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
   foreach(var collider in holder.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
   foreach(var body in holder.GetComponentsInChildren<Rigidbody>())UnityEngine.Object.DestroyImmediate(body);
   var result=PrefabUtility.SaveAsPrefabAsset(holder,Generated+name+".prefab");UnityEngine.Object.DestroyImmediate(holder);return result;
  }
  static void Clean(GameObject go){
   foreach(var renderer in go.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m!=null && Converted.TryGetValue(m,out var converted)?converted:m).ToArray();
   foreach(var t in go.GetComponentsInChildren<Transform>(true)){GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);foreach(var behaviour in t.GetComponents<MonoBehaviour>())UnityEngine.Object.DestroyImmediate(behaviour);}}
  static Bounds PoseBounds(GameObject go) {
   var meshes=go.GetComponentsInChildren<SkinnedMeshRenderer>();if(meshes.Length==0)return Bounds(go);
   Bounds bounds=default;bool first=true;
   foreach(var renderer in meshes) {
    var mesh=new Mesh();renderer.BakeMesh(mesh);
    foreach(var vertex in mesh.vertices){var world=renderer.transform.TransformPoint(vertex);if(first){bounds=new Bounds(world,Vector3.zero);first=false;}else bounds.Encapsulate(world);}
    UnityEngine.Object.DestroyImmediate(mesh);
   }
   return bounds;
  }
  static Bounds Bounds(GameObject go){var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new InvalidOperationException("Owned prefab has no renderer");var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
  static void Fit(GameObject go,Vector3 center,Vector3 size){go.transform.rotation=Quaternion.identity;var b=Bounds(go);go.transform.localScale=Vector3.Scale(go.transform.localScale,new Vector3(size.x/Mathf.Max(.01f,b.size.x),size.y/Mathf.Max(.01f,b.size.y),size.z/Mathf.Max(.01f,b.size.z)));b=Bounds(go);go.transform.position+=center-b.center;}
  static void Decoration(GameObject map,string path,Vector3 floor,Vector3 size){var go=UnityEngine.Object.Instantiate(Load(path),map.transform);Clean(go);Fit(go,floor+Vector3.up*size.y/2,size);foreach(var c in go.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
   var collision=new GameObject("Decoration collision");collision.transform.SetParent(map.transform);collision.transform.position=floor+Vector3.up*size.y/2;
   collision.AddComponent<BoxCollider>().size=size;
  }
 }
}
