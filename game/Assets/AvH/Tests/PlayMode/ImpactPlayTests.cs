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
 }
}
