using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 // The gameplay camera: spring follow, and shake only for viewers involved in a public event.
 public class CameraFeelTests {
  GameObject root;string profile,display;
  [TearDown] public void Cleanup(){DisplayQuality.Apply(GraphicsQuality.High);if(root!=null)Object.Destroy(root);foreach(var f in new[]{profile,display})if(f!=null&&File.Exists(f))File.Delete(f);}
  static void Instant(PlaytestSession s,System.Action<PlaytestValues> extra=null){s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;v.AttackWindupSeconds=0;v.HitStopSeconds=0;v.InitialAttackGrace=0;v.TransformAttackGrace=0;v.PreparationSeconds=1;extra?.Invoke(v);s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettingsNow(0));}
  static bool Swing(PlaytestSession s,int attacker,int victim){var state=s.Observe();s.RecordWorldPosition(victim,state.Players[attacker].Position);return s.TryStartAttack(attacker,state.Round)&&s.TryMeleeHit(attacker,victim,state.Round);}
  void Presentation(out GamePresentation presentation,out UnityPlaytestSession game,out LocalDisplaySettings settings) {
   root=new GameObject("camera feel");presentation=root.AddComponent<GamePresentation>();game=root.GetComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;
   profile=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");display=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   settings=new LocalDisplaySettings(display);presentation.UseDisplaySettings(settings);
   int seed=0;for(;seed<500;seed++){var probe=new PlaytestSession(seed);probe.StartSolo("p");probe.Advance(20.01);if(probe.Observe().Players[0].Faction==Faction.Human)break;}
   game.StartSolo("camera",seed,profile);Instant(game.Session);game.Session.Advance(1.01);
  }

  [UnityTest] public IEnumerator OnlyTheInvolvedViewerShakesCappedAndSwitchedOff() {
   Presentation(out var presentation,out var game,out var settings);yield return null;yield return null;
   var s=game.Session;var state=s.Observe();
   var animals=state.Players.Where(p=>p.Faction==Faction.Animal).Select(p=>p.Slot).ToArray();var bystanderHuman=state.Players.First(p=>p.Faction==Faction.Human&&p.Slot!=0).Slot;
   Assert.IsTrue(Swing(s,animals[0],bystanderHuman));
   float max=0;for(int i=0;i<10;i++){yield return null;max=Mathf.Max(max,presentation.CameraShakeAngle);}
   Assert.AreEqual(0,max,1e-4,"Someone else's hit never shakes this screen");
   Assert.IsTrue(Swing(s,animals[1],0));
   for(int i=0;i<10;i++){yield return null;max=Mathf.Max(max,presentation.CameraShakeAngle);}
   Assert.Greater(max,.3f,"Being hit shakes the victim's own camera");
   yield return new WaitForSecondsRealtime(1.5f);yield return null;Assert.Less(presentation.CameraShakeAngle,.05f,"Shake settles quickly");
   max=0;for(int i=0;i<40;i++){s.RecordLanding(0,40);yield return null;max=Mathf.Max(max,presentation.CameraShakeAngle);}
   Assert.LessOrEqual(max,FeelDirector.MaxShakeDegrees+1e-3f,"Overlapping events never exceed the maximum");Assert.Greater(max,FeelDirector.MaxShakeDegrees*.5f);
   yield return new WaitForSecondsRealtime(1.5f);
   var me=s.Observe().Players[0].Position;var far=s.Observe().Players.First(p=>p.Slot!=0&&Vector3.Distance(new Vector3(p.Position.X,p.Position.Y,p.Position.Z),new Vector3(me.X,me.Y,me.Z))>6).Slot;
   max=0;s.RecordLanding(far,40);for(int i=0;i<10;i++){yield return null;max=Mathf.Max(max,presentation.CameraShakeAngle);}
   Assert.AreEqual(0,max,1e-4,"A distant landing is not this viewer's event");
   int near=s.Observe().Players.First(p=>p.Slot!=0).Slot;s.RecordWorldPosition(near,new WorldPosition(me.X+1.5f,me.Y,me.Z));
   max=0;s.RecordLanding(near,40);for(int i=0;i<10;i++){yield return null;max=Mathf.Max(max,presentation.CameraShakeAngle);}
   Assert.Greater(max,.05f,"A heavy landing right beside the viewer is felt lightly");
   yield return new WaitForSecondsRealtime(1.5f);
   Assert.IsTrue(settings.SetScreenShake(false));
   max=0;for(int i=0;i<20;i++){s.RecordLanding(0,40);yield return null;max=Mathf.Max(max,presentation.CameraShakeAngle);}
   Assert.AreEqual(0,max,1e-4,"Screen shake off means no shake at all");
   Assert.IsFalse(new LocalDisplaySettings(display).ScreenShake,"The choice is saved for the next run");
  }

  [UnityTest] public IEnumerator CameraFollowsWithASpringAndSnapsAfterATeleport() {
   Presentation(out var presentation,out var game,out var settings);yield return null;yield return null;
   var body=game.PlayerTransform(0).GetComponent<CharacterController>();
   void Move(Vector3 to){body.enabled=false;body.transform.position=to;body.enabled=true;}
   var start=body.transform.position;Assert.Less(Vector3.Distance(presentation.FollowPoint,start+Vector3.up*1.35f),.01f,"Starts on the character");
   Move(start+new Vector3(1.2f,0,0));yield return null;
   float lag=Vector3.Distance(presentation.FollowPoint,body.transform.position+Vector3.up*1.35f);Assert.Greater(lag,.15f,"A small move is followed smoothly, not rigidly");
   yield return new WaitForSecondsRealtime(.6f);yield return null;
   Assert.Less(Vector3.Distance(presentation.FollowPoint,body.transform.position+Vector3.up*1.35f),.05f,"...and caught up quickly");
   Move(start+new Vector3(15,0,0));yield return null;
   Assert.Less(Vector3.Distance(presentation.FollowPoint,body.transform.position+Vector3.up*1.35f),.01f,"A recovery teleport snaps instead of swooping");
  }

  [UnityTest] public IEnumerator ClientCameraShakesForItsOwnHitButNotForTheHosts() {
   var h=new GameObject("feel host");var c=new GameObject("feel client");
   try {
    var hostWorld=h.AddComponent<UnityPlaytestSession>();hostWorld.BotAutomationEnabled=false;var host=h.AddComponent<NetworkPresentation>();host.Initialize(hostWorld);host.StartHost("host","127.0.0.1");hostWorld.AutomaticStep=false;
    Instant(hostWorld.Session);
    var clientWorld=c.AddComponent<UnityPlaytestSession>();var client=c.AddComponent<NetworkPresentation>();client.Initialize(clientWorld);client.JoinRoom(host.RoomCode,"guest");
    for(int n=0;n<180&&!client.CanPlay;n++)yield return new WaitForSecondsRealtime(.02f);
    Assert.IsTrue(client.CanPlay);int guest=client.LocalSlot;
    hostWorld.Session.Advance(1.01);
    var hostFeel=new FeelDirector();var clientFeel=new FeelDirector();
    for(int n=0;n<10;n++){yield return new WaitForSecondsRealtime(.02f);hostFeel.Observe(hostWorld.Observe(),0,.02f);clientFeel.Observe(clientWorld.Observe(),guest,.02f);}
    var s=hostWorld.Session;var state=s.Observe();
    int attacker,victim;
    if(state.Players[guest].Faction==Faction.Human){attacker=state.Players.First(p=>p.Faction==Faction.Animal&&p.Slot!=0).Slot;victim=guest;}
    else{attacker=guest;victim=state.Players.First(p=>p.Faction==Faction.Human&&p.Slot!=0).Slot;}
    Assert.IsTrue(Swing(s,attacker,victim));
    float hostMax=0,clientMax=0;
    for(int n=0;n<40;n++){yield return new WaitForSecondsRealtime(.02f);hostFeel.Observe(hostWorld.Observe(),0,.02f);clientFeel.Observe(clientWorld.Observe(),guest,.02f);hostMax=Mathf.Max(hostMax,hostFeel.ShakeDegrees);clientMax=Mathf.Max(clientMax,clientFeel.ShakeDegrees);}
    Assert.Greater(clientMax,.3f,"The client's own hit reaches its camera through the public snapshot");
    Assert.AreEqual(0,hostMax,1e-4,"The uninvolved host does not shake");
   } finally {Object.Destroy(h);Object.Destroy(c);}
  }

  [UnityTest] public IEnumerator GraphicsQualityScalesShadowsEffectsAndShakeImmediately() {
   Presentation(out var presentation,out var game,out var settings);yield return null;
   Assert.AreEqual(GraphicsQuality.High,settings.Quality,"First run is high quality");
   int Burst(){game.Effects.AutomaticUpdate=false;game.Effects.Advance(5);game.Effects.Emit(Vector3.zero,Color.white,20,1);return game.Effects.ActiveCount;}
   var high=(shadows:QualitySettings.shadows,distance:QualitySettings.shadowDistance,effects:Burst(),shake:presentation.Feel.Scale);
   Assert.AreEqual(ShadowQuality.All,high.shadows);
   Assert.IsTrue(settings.SetQuality(GraphicsQuality.Medium));yield return null;
   var medium=(shadows:QualitySettings.shadows,distance:QualitySettings.shadowDistance,effects:Burst(),shake:presentation.Feel.Scale);
   Assert.IsTrue(settings.SetQuality(GraphicsQuality.Low));yield return null;
   var low=(shadows:QualitySettings.shadows,distance:QualitySettings.shadowDistance,effects:Burst(),shake:presentation.Feel.Scale);
   Assert.AreEqual(ShadowQuality.Disable,low.shadows,"Low turns shadows off");Assert.AreNotEqual(ShadowQuality.Disable,medium.shadows);
   Assert.Less(medium.distance,high.distance);
   Assert.Less(medium.effects,high.effects);Assert.Less(low.effects,medium.effects);Assert.Greater(low.effects,0,"Effects still show on low");
   Assert.Less(medium.shake,high.shake);Assert.Less(low.shake,medium.shake);Assert.Greater(low.shake,0);
   Assert.AreEqual(GraphicsQuality.Low,new LocalDisplaySettings(display).Quality,"Saved for the next run");
   Assert.IsTrue(settings.SetQuality(GraphicsQuality.High));yield return null;Assert.AreEqual(high.effects,Burst());Assert.AreEqual(ShadowQuality.All,QualitySettings.shadows);
  }
 }
}
