using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 /// <summary>
 /// Evidence frames of abilities in the real scene (props, gadgets, holds). Runs only when AVH_SHOTS names an output folder.
 /// Every human uses Q at the start of the chase, then a few frames are rendered to JPG.
 /// </summary>
 public class AbilityShots {
  [UnityTest,Timeout(300000)] public IEnumerator CaptureAbilitiesInTheWarehouse() {
   string output=Environment.GetEnvironmentVariable("AVH_SHOTS");if(string.IsNullOrEmpty(output)){Assert.Ignore("AVH_SHOTS not set");yield break;}
   Directory.CreateDirectory(output);
   string profile=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xml");
   var setup=new PlaytestSession(1,profile);setup.BeginSettingsEdit(0);var v=setup.ObserveSettings().Edit;v.PreparationSeconds=1;v.InitialAttackGrace=30;v.Map=GameMaps.Warehouse;setup.UpdateSettingsEdit(0,v);Assert.IsTrue(setup.ApplySettingsNow(0,true));
   var root=new GameObject("ability shots");var target=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
   try {
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;
    game.StartSolo("tester",7,profile,GameMaps.Warehouse);yield return null;
    for(int i=0;i<200&&game.Observe().Phase!=RoundPhase.Chase;i++)game.Step(.02f);
    var state=game.Observe();int round=state.Round;var animals=state.Players.Where(p=>p.Faction==Faction.Animal).ToArray();
    var camera=new GameObject("shot camera").AddComponent<Camera>();camera.targetTexture=target;camera.fieldOfView=55;camera.farClipPlane=200;
    var center=state.Players.Aggregate(Vector3.zero,(sum,p)=>sum+new Vector3(p.Position.X,p.Position.Y,p.Position.Z))/state.Players.Length;
    void Shot(string name,Vector3 from,Vector3 at){camera.transform.position=from;camera.transform.LookAt(at);camera.Render();var old=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(output,name+".jpg"),pixels.EncodeToJPG(82));}
    // Holds aim at the nearest animal; everything else is used where the human stands.
    var sight=game.Session.Sight;game.Session.Sight=null;
    foreach(var p in state.Players.Where(p=>p.Faction==Faction.Human)) {
     var near=animals.OrderBy(a=>Dist(a,p)).First();float yaw=Mathf.Atan2(near.Position.X-p.Position.X,near.Position.Z-p.Position.Z)*Mathf.Rad2Deg;
     bool aimed=p.Ability==AbilityKind.Lasso||p.Ability==AbilityKind.IceRay||p.Ability==AbilityKind.BigBubble||p.Ability==AbilityKind.WaterCannon;
     if(p.Ability==AbilityKind.Kickboard||p.Ability==AbilityKind.Hook)continue;
     game.Session.TryUseAbility(p.Slot,round,aimed?yaw:0,0);
    }
    game.Session.Sight=sight;
    Debug.Log("SHOTS used: "+string.Join(", ",game.Observe().Players.Where(p=>p.AbilityCooldownRemaining>0).Select(p=>p.CharacterName+"="+p.Ability)));
    for(int i=0;i<25;i++){game.Step(.02f);yield return null;}
    Shot("abilities-wide",center+new Vector3(9,7,-12),center+Vector3.up);
    Shot("abilities-close",center+new Vector3(-5,3,-7),center+Vector3.up*1.2f);
    for(int i=0;i<60;i++){game.Step(.02f);yield return null;}
    Shot("abilities-later",center+new Vector3(10,5,8),center+Vector3.up*2);
    Assert.Greater(game.GadgetViewCount+game.Observe().Players.Count(p=>game.AbilityPropKey(p.Slot)!=0),2,"Something to see");
   } finally {UnityEngine.Object.Destroy(root);foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))if(c.name=="shot camera")UnityEngine.Object.Destroy(c.gameObject);target.Release();if(File.Exists(profile))File.Delete(profile);}
   yield return null;
  }
  static float Dist(PlayerState a,PlayerState b){float x=a.Position.X-b.Position.X,z=a.Position.Z-b.Position.Z;return Mathf.Sqrt(x*x+z*z);}
 }
}
