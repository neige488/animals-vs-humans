using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 // Impact rules (attack tell, hit-stop, stun, transformation presentation) through real CharacterControllers.
 public class ImpactPlayTests {
  string profile;GameObject root;UnityPlaytestSession world;
  [TearDown] public void Cleanup(){if(root!=null)Object.Destroy(root);if(profile!=null&&File.Exists(profile))File.Delete(profile);}
  // Slot 0 and slot 4 get the requested factions after a one second preparation without attack grace.
  IEnumerator Create(Faction slot0,Faction slot4,System.Action<PlaytestValues> tune=null) {
   profile=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   var setup=new PlaytestSession(1,profile);setup.BeginSettingsEdit(0);var v=setup.ObserveSettings().Edit;v.PreparationSeconds=1;v.InitialAttackGrace=0;v.TransformAttackGrace=0;tune?.Invoke(v);setup.UpdateSettingsEdit(0,v);Assert.IsTrue(setup.ApplySettingsNow(0,true));
   int seed=0;for(;seed<2000;seed++){var probe=new PlaytestSession(seed,profile);probe.StartSolo("probe");probe.Advance(1.01);var s=probe.Observe();if(s.Players[0].Faction==slot0&&s.Players[4].Faction==slot4&&s.Players[8].Faction==Faction.Human)break;}
   Assert.Less(seed,2000);
   root=new GameObject("impact physics");world=root.AddComponent<UnityPlaytestSession>();world.AutomaticStep=false;world.BotAutomationEnabled=false;world.StartSolo("host",seed,profile);
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(100,-.5f,0);floor.transform.localScale=new Vector3(120,1,60);
   yield return new WaitForFixedUpdate();world.Step(1.01f);
   foreach(var p in world.Observe().Players)Warp(p.Slot,new Vector3(60+p.Slot*7,0,-20));
   Warp(0,new Vector3(100,0,0));Warp(4,new Vector3(100,0,1.2f));Warp(8,new Vector3(110,0,0));
   Physics.SyncTransforms();for(int i=0;i<10;i++)world.Step(.02f);
  }
  void Warp(int slot,Vector3 position){var body=world.PlayerTransform(slot).GetComponent<CharacterController>();body.enabled=false;body.transform.position=position;body.enabled=true;world.Session.RecordWorldPosition(slot,new WorldPosition(position.x,position.y,position.z));}
  void Tune(System.Action<PlaytestValues> tune){var s=world.Session;s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;tune(v);s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettingsNow(0));}
  Vector3 At(int slot)=>world.PlayerTransform(slot).position;
  void Step(int steps,params (int slot,PlayerInput input)[] inputs){for(int i=0;i<steps;i++){foreach(var (slot,input) in inputs)world.SubmitInput(slot,input);world.Step(.02f);}}

  [UnityTest] public IEnumerator WindupSwingIsJudgedByPhysicsOneWindupAfterTheClick() {
   yield return Create(Faction.Animal,Faction.Human,v=>{v.AttackWindupSeconds=.15f;v.HitStopSeconds=0;});
   Step(1,(0,new PlayerInput{Attack=true,Yaw=0}));
   Assert.AreEqual(Faction.Human,world.Observe().Players[4].Faction,"The click starts a tell, not a hit");
   Assert.IsTrue(world.Observe().Events.Any(e=>e.Kind==FeelEventKind.AttackWindup&&e.Actor==0));
   Assert.AreEqual(ActionPhase.Windup,world.Observe().Players[0].Action);
   Step(5,(0,new PlayerInput{Yaw=0}));Assert.AreEqual(Faction.Human,world.Observe().Players[4].Faction,"Still inside the tell at 0.12s");
   Step(3,(0,new PlayerInput{Yaw=0}));Assert.AreEqual(Faction.Animal,world.Observe().Players[4].Faction,"The swing lands once the tell ends");
   Assert.IsTrue(world.Observe().Events.Any(e=>e.Kind==FeelEventKind.AttackHit&&e.Actor==0&&e.Target==4));
  }

  [UnityTest] public IEnumerator HitStopHoldsOnlyTheAttackerAndVictimInPlace() {
   yield return Create(Faction.Animal,Faction.Human,v=>{v.AttackWindupSeconds=0;v.HitStopSeconds=.2f;});
   Step(1,(0,new PlayerInput{Attack=true,Yaw=0}));Assert.AreEqual(Faction.Animal,world.Observe().Players[4].Faction);
   var a=At(0);var v4=At(4);var b=At(8);var forward=new PlayerInput{Forward=1,Yaw=90};
   Step(8,(0,forward),(4,forward),(8,forward));
   Assert.Less(Vector3.Distance(a,At(0)),.02f,"Attacker holds during hit-stop");Assert.Less(Vector3.Distance(v4,At(4)),.02f,"Victim holds during hit-stop");
   Assert.Greater(Vector3.Distance(b,At(8)),.4f,"A bystander keeps playing");
   Assert.AreEqual(ActionPhase.HitStop,world.Observe().Players[0].Action);Assert.IsTrue(world.ObserveAnimation(0).Frozen,"The pose holds too");
   a=At(0);Step(15,(0,forward));Assert.Greater(Vector3.Distance(a,At(0)),.3f,"Movement resumes after the hit-stop");
   Assert.IsFalse(world.ObserveAnimation(0).Frozen);
  }

  [UnityTest] public IEnumerator BubbleHitStaggersTheAnimalAndWeakensItsSteeringBriefly() {
   yield return Create(Faction.Human,Faction.Animal,v=>{v.HitStunSeconds=.5f;v.InertiaSeconds=0;});
   Warp(4,new Vector3(100,0,4));Physics.SyncTransforms();Step(2);
   Step(1,(0,new PlayerInput{Attack=true,Yaw=0}));
   for(int i=0;i<20&&world.Observe().Players[4].StunRemaining<=0;i++)Step(1,(0,new PlayerInput{Yaw=0}));
   var state=world.Observe();Assert.AreEqual(ActionPhase.Stunned,state.Players[4].Action,"A real bubble hit stuns the animal");
   Assert.IsTrue(state.Events.Any(e=>e.Kind==FeelEventKind.Stagger&&e.Target==4&&e.Actor==0));
   var side=new PlayerInput{Right=1,Yaw=0};float x=At(4).x;Step(10,(4,side));float stunned=At(4).x-x;
   var view=world.ObserveAnimation(4);Assert.AreEqual(ActionPhase.Stunned,view.Action);Assert.Greater(view.Stagger,.1f,"The body visibly staggers");
   Step(30,(4,new PlayerInput{Yaw=0}));Assert.AreEqual(ActionPhase.None,world.Observe().Players[4].Action);
   x=At(4).x;Step(10,(4,side));float normal=At(4).x-x;
   Assert.Greater(stunned,0,"Stunned animals still steer");Assert.Less(stunned,normal*.6f,"...but weakly");
   Assert.AreEqual(Faction.Animal,world.Observe().Players[4].Faction,"A bubble never changes faction");
  }

  [UnityTest] public IEnumerator TransformationIsShownBrieflyWhileTheNewAnimalAlreadyPlaysByTheRules() {
   yield return Create(Faction.Animal,Faction.Human,v=>{v.AttackWindupSeconds=0;v.HitStopSeconds=0;v.TransformAttackGrace=.6f;});
   Step(1,(0,new PlayerInput{Attack=true,Yaw=0}));
   Assert.AreEqual(Faction.Animal,world.Observe().Players[4].Faction,"Judged an animal at the hit");
   var shown=world.ObserveAnimation(4);Assert.Less(shown.TransformProgress,.3f,"The transformation starts presenting");Assert.AreEqual(1,shown.TransformPlays);
   var start=At(4);Step(10,(4,new PlayerInput{Forward=1,Yaw=90}));
   Assert.Greater(Vector3.Distance(start,At(4)),.3f,"The new animal moves during the presentation (inertia ramp, any species)");
   var mid=world.ObserveAnimation(4).TransformProgress;Assert.That(mid,Is.InRange(.2f,.99f),"Presentation lasts about 0.3~0.5s");
   Assert.IsFalse(world.Session.TryStartAttack(4,world.Observe().Round),"Attacks follow the transform attack grace");
   Step(15,(4,new PlayerInput()));
   Assert.AreEqual(1,world.ObserveAnimation(4).TransformProgress,1e-4);Assert.AreEqual(1,world.ObserveAnimation(4).TransformPlays,"Never replayed");
   Step(6,(4,new PlayerInput()));Assert.IsTrue(world.Session.TryStartAttack(4,world.Observe().Round),"Grace (0.6s) over");
  }

  [UnityTest] public IEnumerator RemoteViewsPresentEachTransformationOnceAndNotOnJoin() {
   var source=new PlaytestSession(7);source.StartSolo("source");source.BeginSettingsEdit(0);var v=source.ObserveSettings().Edit;v.InitialAttackGrace=0;v.AttackWindupSeconds=0;source.UpdateSettingsEdit(0,v);Assert.IsTrue(source.ApplySettingsNow(0));
   source.Advance(20.01);
   root=new GameObject("remote transform");world=root.AddComponent<UnityPlaytestSession>();world.StartRemote(source.Observe());
   var state=source.Observe();int animal=state.Players.First(p=>p.Faction==Faction.Animal).Slot;int human=state.Players.First(p=>p.Faction==Faction.Human).Slot;
   Assert.AreEqual(0,world.ObserveAnimation(animal).TransformPlays,"Joining mid-round does not replay earlier transformations");
   source.RecordWorldPosition(human,state.Players[animal].Position);Assert.IsTrue(source.TryStartAttack(animal,state.Round)&&source.TryMeleeHit(animal,human,state.Round));
   world.ApplyRemoteSnapshot(source.Observe());yield return null;world.ApplyRemoteSnapshot(source.Observe());yield return null;
   Assert.AreEqual(1,world.ObserveAnimation(human).TransformPlays,"Duplicate snapshots present it once");
   yield return new WaitForSeconds(.6f);world.ApplyRemoteSnapshot(source.Observe());
   Assert.AreEqual(1,world.ObserveAnimation(human).TransformPlays);Assert.AreEqual(1,world.ObserveAnimation(human).TransformProgress,1e-4);
  }

  [UnityTest] public IEnumerator AFrozenNewAnimalCannotChainAHitThroughRealPhysics() {
   // Review F-1 through the real adapter: attack input during the victim's hit-stop is ignored.
   yield return Create(Faction.Animal,Faction.Human,v=>{v.AttackWindupSeconds=0;v.HitStopSeconds=.3f;});
   Warp(8,new Vector3(100,0,2.4f));Physics.SyncTransforms();Step(1);
   Step(1,(0,new PlayerInput{Attack=true,Yaw=0}));Assert.AreEqual(Faction.Animal,world.Observe().Players[4].Faction);
   Step(5,(0,new PlayerInput{Yaw=0}),(4,new PlayerInput{Attack=true,Yaw=0}));
   Assert.AreEqual(Faction.Human,world.Observe().Players[8].Faction,"A frozen animal cannot hit");
   Step(12,(0,new PlayerInput{Yaw=0}),(4,new PlayerInput{Yaw=0}));Assert.IsFalse(world.ObserveAnimation(4).Frozen);
   Step(1,(4,new PlayerInput{Attack=true,Yaw=0}));Assert.AreEqual(Faction.Animal,world.Observe().Players[8].Faction,"After the freeze it can");
  }
 }
}
