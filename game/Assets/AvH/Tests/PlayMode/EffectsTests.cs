using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class EffectsTests {
  [UnityTest] public IEnumerator RoundResetDoesNotReplayAnimalBirthEffects() {
   var root=new GameObject("birth effect boundary");var game=root.AddComponent<UnityPlaytestSession>();
   game.AutomaticStep=false;game.BotAutomationEnabled=false;game.StartSolo("birth",123);game.Effects.AutomaticUpdate=false;
   try {
    while(game.Observe().Phase!=RoundPhase.Results){game.Step(.02f);game.Effects.Advance(2);}
    int round=game.Observe().Round;
    while(game.Observe().Round==round)game.Step(.02f);
    Assert.AreEqual(0,game.Effects.ActiveCount,"Animal-to-human round reset is not an animal birth");
   } finally {Object.Destroy(root);}
   yield return null;
  }
  [UnityTest] public IEnumerator RemoteImpactsDoNotReplayOnInitialStateOrDuplicatePackets() {
   var source=new PlaytestSession(123);source.StartSolo("source");var root=new GameObject("remote effects");var remote=root.AddComponent<UnityPlaytestSession>();remote.StartRemote(source.Observe());
   remote.Effects.AutomaticUpdate=false;
   remote.ApplyRemoteVisuals(new NetworkVisualState{Bursts=new[]{new NetworkBurst{Id=1,Remaining=.1f,Position=new WorldPosition(0,1,0)}}});Assert.AreEqual(0,remote.Effects.ActiveCount);
   remote.ApplyRemoteVisuals(new NetworkVisualState{Bursts=new[]{new NetworkBurst{Id=2,Remaining=.1f,Position=new WorldPosition(0,1,0)}}});int count=remote.Effects.ActiveCount;Assert.Greater(count,0);
   remote.ApplyRemoteVisuals(new NetworkVisualState());
   remote.ApplyRemoteVisuals(new NetworkVisualState{Bursts=new[]{new NetworkBurst{Id=2,Remaining=.05f,Position=new WorldPosition(0,1,0)}}});Assert.AreEqual(count,remote.Effects.ActiveCount);
   Object.Destroy(root);yield return null;
  }
  [UnityTest] public IEnumerator EffectsStayBoundedAndNeverBlockGameplay() {
   var root=new GameObject("effect load");var effects=root.AddComponent<PrimitiveEffects>();effects.Initialize();
   for(int i=0;i<100;i++)effects.Emit(Vector3.zero,Color.cyan,12,2);
   Assert.LessOrEqual(effects.ActiveCount,256);
   foreach(var c in root.GetComponentsInChildren<Collider>())Assert.IsFalse(c.enabled,"Cosmetic effects may not collide with a player or bubble");
   effects.Advance(2);Assert.AreEqual(0,effects.ActiveCount);
   var bubble=effects.Bubble(root.transform,.35f);
   Assert.IsTrue(bubble.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"The shipped thin-film shader must compile for this target");
   Assert.AreEqual("AvH/SoapFilm",bubble.GetComponent<Renderer>().sharedMaterial.shader.name);
   Object.Destroy(root);yield return null;
  }
  [UnityTest] public IEnumerator ImpactEventsShowEffectsOnceAndNeverReplayOnJoin() {
   var source=new PlaytestSession(123);source.StartSolo("source");source.BeginSettingsEdit(0);var v=source.ObserveSettings().Edit;v.InitialAttackGrace=0;v.AttackWindupSeconds=0;source.UpdateSettingsEdit(0,v);Assert.IsTrue(source.ApplySettingsNow(0));
   source.Advance(20.01);var state=source.Observe();int animal=state.Players.First(p=>p.Faction==Faction.Animal).Slot;int other=state.Players.Last(p=>p.Faction==Faction.Animal).Slot;
   Assert.IsTrue(source.TryStartAttack(animal,state.Round)&&source.ResolveAttackMiss(animal,state.Round),"An earlier swing exists before the join");
   var root=new GameObject("impact effects");var remote=root.AddComponent<UnityPlaytestSession>();remote.StartRemote(source.Observe());remote.Effects.AutomaticUpdate=false;
   try {
    remote.Effects.Advance(5);remote.ApplyRemoteSnapshot(source.Observe());Assert.AreEqual(0,remote.Effects.ActiveCount,"Joining does not replay earlier swings");
    Assert.IsTrue(source.TryStartAttack(other,state.Round)&&source.ResolveAttackMiss(other,state.Round));
    remote.ApplyRemoteSnapshot(source.Observe());int count=remote.Effects.ActiveCount;Assert.Greater(count,0,"A new swing shows its effect");
    remote.ApplyRemoteSnapshot(source.Observe());Assert.AreEqual(count,remote.Effects.ActiveCount,"Repeated snapshots never replay it");
   } finally {Object.Destroy(root);}
   var host=new GameObject("host impact effects");var game=host.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;
   string profile=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   try {
    game.StartSolo("host",123,profile);game.Effects.AutomaticUpdate=false;game.Session.BeginSettingsEdit(0);v=game.Session.ObserveSettings().Edit;v.InitialAttackGrace=0;v.PreparationSeconds=1;game.Session.UpdateSettingsEdit(0,v);Assert.IsTrue(game.Session.ApplySettingsNow(0));
    game.Step(1.01f);for(int i=0;i<5;i++)game.Step(.02f);game.Effects.Advance(5);
    animal=game.Observe().Players.First(p=>p.Faction==Faction.Animal).Slot;Assert.IsTrue(game.Session.TryStartAttack(animal,game.Observe().Round));
    game.Step(.02f);Assert.Greater(game.Effects.ActiveCount,0,"The host shows the attack tell");
   } finally {Object.Destroy(host);if(System.IO.File.Exists(profile))System.IO.File.Delete(profile);}
   yield return null;
  }
 }
}
