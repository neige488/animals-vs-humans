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
 }
}
