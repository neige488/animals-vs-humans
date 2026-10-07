using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 // The town's look per local graphics quality on the Built-in pipeline: post-processing, sway and lantern light.
 public class TownAtmosphereTests {
  GameObject root;UnityPlaytestSession world;Camera camera;RenderTexture target;Texture2D pixels;string profile;
  [TearDown] public void Cleanup(){DisplayQuality.Apply(GraphicsQuality.High);if(root!=null)Object.Destroy(root);if(target!=null)Object.Destroy(target);if(pixels!=null)Object.Destroy(pixels);if(profile!=null&&System.IO.File.Exists(profile))System.IO.File.Delete(profile);}
  IEnumerator Town() {
   if(Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog")==null)Assert.Ignore("The dressing comes from the owned village");
   root=new GameObject("atmosphere evidence");world=root.AddComponent<UnityPlaytestSession>();world.AutomaticStep=false;world.BotAutomationEnabled=false;
   profile=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");world.StartSolo("atmosphere",123,profile);
   var cameraObject=new GameObject("Atmosphere camera");cameraObject.transform.SetParent(root.transform);camera=cameraObject.AddComponent<Camera>();camera.fieldOfView=60;
   var sun=new GameObject("Atmosphere sun");sun.transform.SetParent(root.transform);TownLighting.Apply(camera,sun.AddComponent<Light>());
   // Market street with a lantern pole, a stall and the bunting in view.
   camera.transform.position=new Vector3(-2,2.2f,-14);camera.transform.LookAt(new Vector3(2,1.6f,4));
   target=new RenderTexture(480,270,24){antiAliasing=1};camera.targetTexture=target;pixels=new Texture2D(480,270,TextureFormat.RGB24,false);
   yield return null;
  }
  Color[] Render(){camera.Render();var old=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,480,270),0,0);pixels.Apply();RenderTexture.active=old;return pixels.GetPixels();}
  static float Difference(Color[] a,Color[] b){double sum=0;for(int i=0;i<a.Length;i++)sum+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);return (float)(sum/(a.Length*3));}

  [UnityTest] public IEnumerator PostProcessingRendersOnBuiltInAtHighAndStepsDownWithQuality() {
   yield return Town();
   DisplayQuality.Apply(GraphicsQuality.Low,camera);yield return null;
   var low=TownAtmosphere.Observe(camera);var plain=Render();
   Assert.IsFalse(low.PostProcessing,"Low renders without post-processing");
   DisplayQuality.Apply(GraphicsQuality.High,camera);yield return null;
   var high=TownAtmosphere.Observe(camera);var graded=Render();
   Assert.IsTrue(high.PostProcessing&&high.ToneMapping&&high.ColorGrading&&high.Bloom&&high.AmbientOcclusion,"High: tone mapping, colour grading, bloom and ambient occlusion");
   Assert.Greater(Difference(plain,graded),.01f,"The post stack actually changes the rendered image on this Unity version");
   DisplayQuality.Apply(GraphicsQuality.Medium,camera);yield return null;
   var medium=TownAtmosphere.Observe(camera);
   Assert.IsTrue(medium.PostProcessing&&medium.ToneMapping&&medium.Bloom,"Medium keeps the grade and bloom");Assert.IsFalse(medium.AmbientOcclusion,"Medium drops ambient occlusion");
   Assert.Greater(high.PixelLights,medium.PixelLights);Assert.Greater(medium.PixelLights,low.PixelLights);
  }

  [UnityTest] public IEnumerator TreesAndFlagsSwayAndLanternsGlowLessAtLowerQuality() {
   yield return Town();
   var tree=root.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith("SM_Env_Tree"));
   var pennant=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Market ribbon");
   IEnumerator Moves(System.Action<bool> result){var a=(tree.rotation,pennant.rotation);float angle=0;for(int i=0;i<20;i++){yield return null;angle=Mathf.Max(angle,Quaternion.Angle(a.Item1,tree.rotation),Quaternion.Angle(a.Item2,pennant.rotation));}result(angle>.05f);}
   bool moving=false;
   DisplayQuality.Apply(GraphicsQuality.High,camera);yield return Moves(m=>moving=m);var high=world.ObserveTown();
   Assert.IsTrue(moving,"Trees and flags sway at high quality");
   Assert.GreaterOrEqual(high.SwayingObjects,20,"Trees, bushes and the market flags");Assert.GreaterOrEqual(high.LanternLights,10,"Every street lantern glows");Assert.IsFalse(high.LanternShadows,"Lantern lights never cast shadows");
   DisplayQuality.Apply(GraphicsQuality.Medium,camera);yield return Moves(m=>moving=m);var medium=world.ObserveTown();
   Assert.IsTrue(moving);Assert.Less(medium.SwayAmplitude,high.SwayAmplitude);Assert.Less(medium.LanternLights,high.LanternLights);Assert.Greater(medium.LanternLights,0);
   DisplayQuality.Apply(GraphicsQuality.Low,camera);yield return null;yield return Moves(m=>moving=m);var low=world.ObserveTown();
   Assert.IsFalse(moving,"Low holds trees and flags still");Assert.AreEqual(0,low.SwayingObjects);Assert.AreEqual(0,low.LanternLights);
   Assert.AreEqual(ShadowQuality.Disable,QualitySettings.shadows);
  }
 }
}
