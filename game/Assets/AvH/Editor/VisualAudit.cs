using System.IO;
using System.Linq;
using UnityEngine;
namespace AvH.Editor {
 // Render the actual owned prefabs for scale/layout inspection. This is not an interactive gameplay gate.
 public static class VisualAudit {
  static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/VisualAudit"));
  public static void GenerateAndCapture() {ConfigureOwnedAssets.Configure();Capture();}
  public static void BuildMac() {GenerateAndCapture();BuildPlaytest.Mac();}
  public static void Capture() {
   Directory.CreateDirectory(Output);
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   var root=new GameObject("Visual audit");Object.Instantiate(catalog.Village,root.transform);
   var light=new GameObject("Audit sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-35,0);RenderSettings.ambientLight=new Color(.55f,.55f,.55f);
   var camera=new GameObject("Audit camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.55f,.73f,.8f);camera.farClipPlane=200;
   TownLighting.Apply(camera,light);
   foreach(var p in catalog.ShelterPoints)Pose(catalog.Human,root.transform,p);
   Pose(catalog.Animal,root.transform,TownLayout.Roof+new Vector3(1,0,-1));
   Shot(camera,new Vector3(66,59,-72),Vector3.zero,"map");
   Shot(camera,TownLayout.Roof+new Vector3(10,5,-12),TownLayout.Roof+Vector3.up,"shelter");
   Shot(camera,new Vector3(4,2.8f,-12),new Vector3(-3,2,13),"market-street");
   Shot(camera,TownLayout.Workshop+new Vector3(-10,3.2f,12),TownLayout.Workshop+Vector3.up,"workshop");
   Shot(camera,TownLayout.Courtyard+new Vector3(-9,3.2f,-11),TownLayout.Courtyard+Vector3.up,"courtyard");
   Physics.SyncTransforms();
   var surface=Physics.RaycastAll(new Vector3(-7,12,13),Vector3.down,20).OrderBy(h=>h.distance).First();
   Pose(catalog.Human,root.transform,surface.point);
   Shot(camera,new Vector3(0,7,7),surface.point+Vector3.up*.6f,"prop-contact");
   var effects=root.AddComponent<PrimitiveEffects>();effects.Initialize();
   foreach(var p in new[]{new Vector3(-.4f,1.4f,-4),new Vector3(.8f,1.7f,-1.5f),new Vector3(.25f,1.55f,1)}){var bubble=effects.Bubble(root.transform,.35f);bubble.transform.position=p;}
   effects.Emit(new Vector3(0,1.4f,3),new Color(.66f,.92f,1,.9f),12,1.9f);effects.Advance(.12f);
   Shot(camera,new Vector3(2,2.6f,-7),new Vector3(0,1.6f,1),"bubble-effects");root.SetActive(false);
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.025f,0);floor.transform.localScale=new Vector3(12,.05f,12);
   foreach(var character in catalog.Humans.Concat(catalog.Animals)) {
    var prefab=character.Prefab;
    var model=Pose(prefab,null,Vector3.zero);
    foreach(var r in model.GetComponentsInChildren<Renderer>())Debug.Log("AUDIT "+prefab.name+" bounds "+r.bounds+" scale "+r.transform.lossyScale);
    if(character.Id.StartsWith("human-")){var gun=effects.Gun(model.transform);gun.GetComponent<BubbleGunPose>().Bind(model.transform);gun.GetComponent<BubbleGunPose>().SetAim(0,-10);gun.GetComponent<BubbleGunPose>().ApplyPose();}
    Shot(camera,new Vector3(3,2,4),Vector3.up*.7f,character.Id);Object.DestroyImmediate(model);
   }
   Object.DestroyImmediate(floor);Object.DestroyImmediate(root);Object.DestroyImmediate(light.gameObject);Object.DestroyImmediate(camera.gameObject);
  }
  static GameObject Pose(GameObject prefab,Transform parent,Vector3 position) {
   var model=Object.Instantiate(prefab,parent);model.transform.position=position;
   foreach(var a in model.GetComponentsInChildren<Animator>()){a.Rebind();a.Update(0);a.Play("Idle",0,0);a.Update(.1f);}return model;
  }
  static void Shot(Camera camera,Vector3 eye,Vector3 target,string name) {
   camera.transform.position=eye;camera.transform.LookAt(target);var rt=new RenderTexture(1200,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
   var tex=new Texture2D(1200,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);
  }
 }
}
