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
   foreach(var p in catalog.ShelterPoints)Pose(catalog.Human,root.transform,p);
   Pose(catalog.Animal,root.transform,new Vector3(-11,4,9));
   Shot(camera,new Vector3(35,32,-40),Vector3.zero,"map");
   Shot(camera,new Vector3(-5,8,3),new Vector3(-12,4.8f,10),"shelter");
   Physics.SyncTransforms();
   var surface=Physics.RaycastAll(new Vector3(-6,12,7),Vector3.down,20).OrderBy(h=>h.distance).First();
   Pose(catalog.Human,root.transform,surface.point);
   Shot(camera,new Vector3(0,7,2),surface.point+Vector3.up*.6f,"prop-contact");root.SetActive(false);
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.025f,0);floor.transform.localScale=new Vector3(12,.05f,12);
   foreach(var prefab in new[]{catalog.Human,catalog.Animal}) {
    var model=Pose(prefab,null,Vector3.zero);
    foreach(var r in model.GetComponentsInChildren<Renderer>())Debug.Log("AUDIT "+prefab.name+" bounds "+r.bounds+" scale "+r.transform.lossyScale);
    Shot(camera,new Vector3(3,2,4),Vector3.up*.7f,prefab.name);Object.DestroyImmediate(model);
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
