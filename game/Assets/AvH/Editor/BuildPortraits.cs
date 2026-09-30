using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace AvH.Editor {
 public static class BuildPortraits {
  public static void Generate() {
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");if(catalog==null)throw new System.InvalidOperationException("Configure owned assets first.");
   const string folder="Assets/ThirdParty/Generated/Portraits/";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   foreach(var entry in catalog.Humans.Concat(catalog.Animals)) {
    var preview=new PreviewRenderUtility();GameObject model=null;
    try {
     model=Object.Instantiate(entry.Prefab);preview.AddSingleGO(model);
     foreach(var animator in model.GetComponentsInChildren<Animator>())animator.enabled=false;
     var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
     bool human=entry.Id.StartsWith("human-");
     var head=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.ToLowerInvariant().Contains("head"));
     Vector3 focus=head!=null?head.position:bounds.center+Vector3.up*bounds.size.y*(human?.32f:.15f)+Vector3.forward*(human?0:bounds.size.z*.16f);
     Vector3 facing=human?Vector3.forward:(head!=null?head.position-bounds.center:(bounds.size.x>bounds.size.z?Vector3.right:Vector3.forward));facing.y=0;if(facing.sqrMagnitude<.001f)facing=Vector3.forward;facing.Normalize();
     if(!human&&head==null)focus=bounds.center+Vector3.up*bounds.size.y*.15f+facing*Mathf.Max(bounds.extents.x,bounds.extents.z)*.65f;
     float frame=human?bounds.size.y*.22f:Mathf.Max(bounds.size.y*.55f,Mathf.Min(bounds.size.x,bounds.size.z)*.75f);
     Debug.Log($"Portrait {entry.Id}: head={head?.name}, facing={facing}, bounds={bounds.size}");
     preview.camera.orthographic=true;preview.camera.orthographicSize=frame*.65f;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=40;
     preview.camera.transform.position=focus+(facing+Vector3.up*.08f+Vector3.Cross(Vector3.up,facing)*.12f)*Mathf.Max(3,bounds.size.magnitude*2);preview.camera.transform.LookAt(focus);
     preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(0,0,0,0);
     preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.LookRotation(-facing+Vector3.down*.4f);preview.lights[1].intensity=1;preview.lights[1].transform.rotation=Quaternion.LookRotation(-facing+Vector3.right*.5f);preview.ambientColor=new Color(.8f,.8f,.8f);
     preview.BeginPreview(new Rect(0,0,160,160),GUIStyle.none);preview.Render();var texture=(RenderTexture)preview.EndPreview();var old=RenderTexture.active;RenderTexture.active=texture;
     var pixels=new Texture2D(160,160,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,160,160),0,0);pixels.Apply();RenderTexture.active=old;
     var path=folder+entry.Id+".png";File.WriteAllBytes(path,pixels.EncodeToPNG());Object.DestroyImmediate(pixels);AssetDatabase.ImportAsset(path);
     var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)throw new System.InvalidOperationException("Portrait import failed: "+path);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();entry.Portrait=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }finally{preview.Cleanup();if(model!=null)Object.DestroyImmediate(model);}
   }
   EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();Debug.Log("Twelve owned character portraits generated.");
  }
 }
}
