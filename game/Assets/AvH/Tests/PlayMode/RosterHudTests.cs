using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class RosterHudTests {
  [UnityTest] public IEnumerator RealSessionBirthsMoveDividerAndPreserveAllTwelvePortraits() {
   string profile=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   var config=new PlaytestSession(123,profile);config.BeginSettingsEdit(0);var rules=config.ObserveSettings().Edit;rules.InitialAnimals=3;rules.PreparationSeconds=1;config.UpdateSettingsEdit(0,rules);Assert.IsTrue(config.ApplySettings(0));
   var root=new GameObject("roster HUD gameplay evidence");root.AddComponent<GamePresentation>();var game=root.GetComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;
   RenderTexture target=null;Texture2D pixels=null;RosterHud hud=null;
   try {
    game.StartSolo("HUD",123,profile);yield return null;yield return null;
    hud=Object.FindAnyObjectByType<RosterHud>();Assert.IsNotNull(hud);
    var texts=hud.GetComponentsInChildren<Text>();var left=texts.Single(t=>t.name=="Human count");var right=texts.Single(t=>t.name=="Animal count");var vs=texts.Single(t=>t.name=="VS").rectTransform;
    Assert.AreEqual("12",left.text);Assert.AreEqual("0",right.text);Assert.AreEqual(12,hud.GetComponentsInChildren<RawImage>().Count(p=>p.name=="Current portrait"));
    Assert.IsTrue(hud.GetComponentsInChildren<RawImage>().Where(p=>p.name=="Current portrait").All(p=>p.texture!=null));
    float from=vs.anchoredPosition.x;
    var camera=Camera.main??Object.FindAnyObjectByType<Camera>();string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/VisualAudit/RosterHud"));Directory.CreateDirectory(output);
    bool film=System.Environment.GetCommandLineArgs().Contains("-avhCapture");if(film)Directory.CreateDirectory(Path.Combine(output,"Sequence"));
    target=new RenderTexture(1280,720,24);pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);camera.targetTexture=target;hud.SetCaptureTarget(target);
    for(int frame=0;frame<75;frame++) {
     if(frame==10){for(int i=0;i<51;i++)game.Step(.02f);}
     yield return null;Canvas.ForceUpdateCanvases();
     if(film||frame==0||frame==30){Capture(camera,target,pixels,Path.Combine(output,film?$"Sequence/frame-{frame:0000}.png":$"hud-{frame:00}.png"));}
     if(frame==11){Assert.AreEqual("9",left.text);Assert.AreEqual("3",right.text);Assert.Greater(vs.anchoredPosition.x,from-3*46,"Divider must animate rather than jump instantly.");}
     if(film)yield return new WaitForSecondsRealtime(1f/30);
    }
    yield return new WaitForSecondsRealtime(.35f);yield return null;
    Assert.AreEqual(from-3*46,vs.anchoredPosition.x,.1f);
    var faces=hud.GetComponentsInChildren<RawImage>().Where(p=>p.name=="Current portrait").OrderBy(p=>p.transform.parent.parent.localPosition.x).ToArray();Assert.AreEqual(12,faces.Length);
    var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");Assert.IsTrue(faces.Take(9).All(p=>catalog.Humans.Any(c=>c.Portrait==p.texture)));Assert.IsTrue(faces.Skip(9).All(p=>catalog.Animals.Any(c=>c.Portrait==p.texture)));
    for(int i=1;i<faces.Length;i++)Assert.Greater(faces[i].transform.parent.parent.localPosition.x-faces[i-1].transform.parent.parent.localPosition.x,42,"Settled portraits must not overlap.");
    // A round reset must restore all identities immediately, without a twelve-face crossing animation.
    game.Step(185f);yield return null;yield return null;Assert.AreEqual("12",left.text);Assert.AreEqual("0",right.text);Assert.AreEqual(from,vs.anchoredPosition.x,.1f);
    camera.targetTexture=null;hud.SetCaptureTarget(null);Object.Destroy(target);target=new RenderTexture(1920,1080,24);camera.targetTexture=target;hud.SetCaptureTarget(target);Object.Destroy(pixels);pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);for(int i=0;i<51;i++)game.Step(.02f);yield return null;Canvas.ForceUpdateCanvases();
    var timerBounds=new Vector3[4];var birthBounds=new Vector3[4];texts.Single(t=>t.name=="Round timer").rectTransform.GetWorldCorners(timerBounds);texts.Single(t=>t.name=="Animal births").rectTransform.GetWorldCorners(birthBounds);Assert.Greater(timerBounds[0].y,birthBounds[1].y,"Birth notice must stay below the timer at 1080p.");
    Capture(camera,target,pixels,Path.Combine(output,"hud-1920.png"));
    var occluder=GameObject.CreatePrimitive(PrimitiveType.Cube);occluder.transform.SetParent(root.transform);occluder.GetComponent<Collider>().enabled=false;occluder.transform.position=camera.transform.position+camera.transform.forward*.4f;occluder.transform.rotation=camera.transform.rotation;occluder.transform.localScale=new Vector3(3,3,.04f);var material=new Material(Shader.Find("Unlit/Color"));material.color=Color.magenta;occluder.GetComponent<Renderer>().sharedMaterial=material;
    Capture(camera,target,pixels,Path.Combine(output,"hud-occlusion-1920.png"));Assert.Less(pixels.GetPixel(435,1020).r,.6f,"Opaque geometry in front of the gameplay camera must not cover the overlay HUD.");Object.Destroy(material);
    root.GetComponent<NetworkPresentation>().Leave();yield return null;yield return null;Assert.IsFalse(hud.gameObject.activeSelf,"Leaving the room must hide the match HUD.");
   } finally {if(hud!=null)hud.SetCaptureTarget(null);var cam=Camera.main??Object.FindAnyObjectByType<Camera>();if(cam!=null)cam.targetTexture=null;Object.Destroy(root);if(target!=null)Object.Destroy(target);if(pixels!=null)Object.Destroy(pixels);if(File.Exists(profile))File.Delete(profile);}
   yield return null;
  }
  [UnityTest] public IEnumerator RapidConfirmedTransformationsRetargetAndReachZeroHumans() {
   string profile=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");var source=new PlaytestSession(42,profile);source.BeginSettingsEdit(0);var rules=source.ObserveSettings().Edit;rules.PreparationSeconds=1;rules.InitialAnimals=3;rules.InitialAttackGrace=0;source.UpdateSettingsEdit(0,rules);Assert.IsTrue(source.ApplySettings(0));
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");source.StartSolo("HUD",humans:catalog.Humans.Select(c=>c.Definition()).ToArray(),animals:catalog.Animals.Select(c=>c.Definition()).ToArray());
   var root=new GameObject("rapid births HUD");var hud=root.AddComponent<RosterHud>();var camera=new GameObject("HUD fixture camera").AddComponent<Camera>();hud.Initialize(camera);
   try {
    hud.Present(source.Observe(),0);source.Advance(1.01);hud.Present(source.Observe(),0);yield return null;
    var state=source.Observe();int attacker=state.Players.First(p=>p.Faction==Faction.Animal).Slot;
    foreach(var victim in state.Players.Where(p=>p.Faction==Faction.Human)) {
     source.RecordWorldPosition(attacker,new WorldPosition(0,1,0));source.RecordWorldPosition(victim.Slot,new WorldPosition(0,1,0));Assert.IsTrue(source.TryMeleeHit(attacker,victim.Slot,state.Round));hud.Present(source.Observe(),0);yield return null;source.Advance(.6);
    }
    yield return new WaitForSecondsRealtime(.35f);hud.Present(source.Observe(),0);Canvas.ForceUpdateCanvases();
    var texts=hud.GetComponentsInChildren<Text>();Assert.AreEqual("0",texts.Single(t=>t.name=="Human count").text);Assert.AreEqual("12",texts.Single(t=>t.name=="Animal count").text);Assert.AreEqual(-276,texts.Single(t=>t.name=="VS").rectTransform.anchoredPosition.x,.1f);
    var faces=hud.GetComponentsInChildren<RawImage>().Where(p=>p.name=="Current portrait").ToArray();Assert.AreEqual(12,faces.Length);Assert.IsTrue(faces.All(p=>catalog.Animals.Any(c=>c.Portrait==p.texture)));
   }finally{Object.Destroy(root);Object.Destroy(camera.gameObject);if(File.Exists(profile))File.Delete(profile);}yield return null;
  }
  static void Capture(Camera camera,RenderTexture target,Texture2D pixels,string path) {
   var hud=Object.FindAnyObjectByType<RosterHud>();hud.SetCaptureTarget(target);Canvas.ForceUpdateCanvases();camera.Render();hud.RenderOverlay();var old=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();RenderTexture.active=old;File.WriteAllBytes(path,pixels.EncodeToPNG());
  }
 }
}
