using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class TownPresentationTests {
  [UnityTest] public IEnumerator TwelvePlayersRunWithBoundedPresentationAndCaptureOptionalFilm() {
   var root=new GameObject("town presentation evidence");var game=root.AddComponent<UnityPlaytestSession>();
   bool capture=System.Environment.GetCommandLineArgs().Contains("-avhCapture");
   string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/VisualAudit/Sequence"));
   RenderTexture target=null;Texture2D pixels=null;
   string profilePath=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   try {
    var profile=new PlaytestSession(123,profilePath);profile.BeginSettingsEdit(0);var rules=profile.ObserveSettings().Edit;rules.PreparationSeconds=3;profile.UpdateSettingsEdit(0,rules);Assert.IsTrue(profile.ApplySettings(0));
    game.AutomaticStep=false;game.StartSolo("capture",123,profilePath);game.Effects.AutomaticUpdate=false;
    yield return null;
    var cameraObject=new GameObject("Evidence camera");cameraObject.transform.SetParent(root.transform);var camera=cameraObject.AddComponent<Camera>();camera.fieldOfView=60;
    var lightObject=new GameObject("Evidence sun");lightObject.transform.SetParent(root.transform);var sun=lightObject.AddComponent<Light>();TownLighting.Apply(camera,sun);
    if(capture){Directory.CreateDirectory(output);target=new RenderTexture(960,540,24);camera.targetTexture=target;pixels=new Texture2D(960,540,TextureFormat.RGB24,false);}
    var costs=new System.Collections.Generic.List<double>();
    for(int frame=0;frame<180;frame++) {
     // Ordinary public inputs drive the actual game. No injected projectile or hit claims.
     game.SubmitInput(0,new PlayerInput{Attack=frame>20,Forward=frame<50?.65f:0,Right=frame>=50&&frame<90?.5f:0,Yaw=frame<90?0:-30,Jump=frame==25});
     var clock=System.Diagnostics.Stopwatch.StartNew();game.Step(1f/30);clock.Stop();costs.Add(clock.Elapsed.TotalMilliseconds);game.Effects.Advance(1f/30);
     Assert.LessOrEqual(game.Effects.ActiveCount,256);Assert.AreEqual(12,game.Observe().Players.Length);
     var p=game.PlayerTransform(0).position;camera.transform.position=p+new Vector3(3,2.8f,-7);camera.transform.LookAt(p+new Vector3(0,1.3f,5));
     if(capture){camera.Render();var old=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(output,$"frame-{frame:0000}.png"),pixels.EncodeToPNG());}
     yield return null;
    }
    costs.Sort();Debug.Log($"TOWN_SIMULATION_CPU_MS p50={costs[costs.Count/2]:F2} p95={costs[(int)(costs.Count*.95f)]:F2} max={costs.Last():F2}; 12 players; effects={game.Effects.ActiveCount}; renderedEvidence={capture}");
   } finally {Object.Destroy(root);if(target!=null)Object.Destroy(target);if(pixels!=null)Object.Destroy(pixels);if(File.Exists(profilePath))File.Delete(profilePath);}
   yield return null;
  }
  // Representative cost of a 12-player frame with pushable props, lantern lights, sway and post-processing per quality.
  [UnityTest] public IEnumerator TwelveBotsWithPropsLightsAndPostProcessingFrameCostPerQuality() {
   if(Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog")==null)Assert.Ignore("Measured on the owned village");
   var root=new GameObject("town frame cost");var game=root.AddComponent<UnityPlaytestSession>();
   string profilePath=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");RenderTexture target=null;Texture2D probe=null;
   try {
    var profile=new PlaytestSession(123,profilePath);profile.BeginSettingsEdit(0);var rules=profile.ObserveSettings().Edit;rules.PreparationSeconds=3;profile.UpdateSettingsEdit(0,rules);Assert.IsTrue(profile.ApplySettings(0));
    game.AutomaticStep=false;game.StartSolo("cost",123,profilePath);yield return null;
    var camera=new GameObject("Cost camera").AddComponent<Camera>();camera.transform.SetParent(root.transform);camera.fieldOfView=65;
    var sun=new GameObject("Cost sun").AddComponent<Light>();sun.transform.SetParent(root.transform);TownLighting.Apply(camera,sun);
    target=new RenderTexture(1280,720,24);camera.targetTexture=target;probe=new Texture2D(1,1,TextureFormat.RGB24,false);
    var report=new System.Text.StringBuilder();int moved=0;
    foreach(var quality in new[]{GraphicsQuality.High,GraphicsQuality.Medium,GraphicsQuality.Low}) {
     DisplayQuality.Apply(quality,camera);yield return null;
     var step=new System.Collections.Generic.List<double>();var render=new System.Collections.Generic.List<double>();
     for(int frame=0;frame<120;frame++) {
      var clock=System.Diagnostics.Stopwatch.StartNew();game.Step(1f/60);clock.Stop();step.Add(clock.Elapsed.TotalMilliseconds);
      var p=game.PlayerTransform(0).position;camera.transform.position=p+new Vector3(.6f,2,-5.5f);camera.transform.LookAt(p+Vector3.up*1.3f+Vector3.forward*8);
      // Reading one pixel back waits for the GPU, so the render figure includes GPU work, not only command submission.
      clock=System.Diagnostics.Stopwatch.StartNew();camera.Render();var old=RenderTexture.active;RenderTexture.active=target;probe.ReadPixels(new Rect(0,0,1,1),0,0);RenderTexture.active=old;clock.Stop();render.Add(clock.Elapsed.TotalMilliseconds);
      yield return null;
     }
     step.Sort();render.Sort();var town=game.ObserveTown();var post=TownAtmosphere.Observe(camera);
     report.Append($" {quality}: step p50={step[60]:F2} p95={step[114]:F2} render p50={render[60]:F2} p95={render[114]:F2} lights={town.LanternLights} sway={town.SwayingObjects} post={post.PostProcessing} ao={post.AmbientOcclusion};");
    }
    moved=game.ObserveProps().Count(p=>Vector3.Distance(p.Position,p.Home)>.05f);
    Debug.Log($"TOWN_FRAME_COST_MS 12 bots, {game.ObserveProps().Length} props ({moved} moved), 1280x720 RT;{report}");
    Assert.AreEqual(12,game.Observe().Players.Length);
   } finally {DisplayQuality.Apply(GraphicsQuality.High);Object.Destroy(root);if(target!=null)Object.Destroy(target);if(probe!=null)Object.Destroy(probe);if(File.Exists(profilePath))File.Delete(profilePath);}
   yield return null;
  }
 }
}
