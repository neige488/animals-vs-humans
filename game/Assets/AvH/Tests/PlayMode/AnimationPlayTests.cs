using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 // Locomotion animation through the public session boundary with the owned Animator rigs.
 public class AnimationPlayTests {
  string profile;GameObject root;UnityPlaytestSession world;
  [TearDown] public void Cleanup(){if(root!=null)Object.Destroy(root);if(profile!=null&&File.Exists(profile))File.Delete(profile);}
  IEnumerator Create(bool animals) {
   if(Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog")==null)Assert.Ignore("Owned assets are required for animation integration");
   profile=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   if(animals){var setup=new PlaytestSession(1,profile);setup.BeginSettingsEdit(0);var v=setup.ObserveSettings().Edit;v.PreparationSeconds=1;v.InitialAnimals=6;v.InitialAttackGrace=30;setup.UpdateSettingsEdit(0,v);setup.ApplySettingsNow(0,true);}
   root=new GameObject("animation physics");world=root.AddComponent<UnityPlaytestSession>();world.AutomaticStep=false;world.BotAutomationEnabled=false;world.StartSolo("host",42,profile);
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(100,-.5f,0);floor.transform.localScale=new Vector3(120,1,60);
   yield return new WaitForFixedUpdate();if(animals)world.Step(1.01f);
   foreach(var p in world.Observe().Players)Warp(p.Slot,new Vector3(60+p.Slot*7,0,-20));
   Physics.SyncTransforms();for(int i=0;i<20;i++)world.Step(.02f);
  }
  void Warp(int slot,Vector3 position){var body=world.PlayerTransform(slot).GetComponent<CharacterController>();body.enabled=false;body.transform.position=position;body.enabled=true;world.Session.RecordWorldPosition(slot,new WorldPosition(position.x,position.y,position.z));}
  void Tune(System.Action<PlaytestValues> tune){var s=world.Session;s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;tune(v);s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettingsNow(0));}
  CharacterAnimationView Drive(int slot,PlayerInput input,int steps){for(int i=0;i<steps;i++){world.SubmitInput(slot,input);world.Step(.02f);}return world.ObserveAnimation(slot);}
  int Human=>world.Observe().Players.First(p=>p.Faction==Faction.Human).Slot;

  [UnityTest] public IEnumerator HumanBlendsWalkToRunAndSyncsCadenceWithSpeedOnTheRealAnimator() {
   yield return Create(false);int h=Human;
   var idle=world.ObserveAnimation(h);Assert.AreEqual(LocomotionGait.Idle,idle.Gait);Assert.IsTrue(idle.DrivesAnimator,"Locomotion graph drives the Animator");
   StringAssert.Contains("Idle",idle.IdleClip);StringAssert.Contains("Walk",idle.WalkClip);StringAssert.Contains("Run",idle.RunClip);
   var animator=world.PlayerTransform(h).GetComponentsInChildren<Animator>().First(a=>a.isHuman);var body=world.PlayerTransform(h);
   var foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);
   float Spread(List<Vector3> samples)=>samples.Count==0?0:samples.Max(a=>samples.Max(b=>Vector3.Distance(a,b)));
   var still=new List<Vector3>();for(int f=0;f<12;f++){world.Step(.02f);yield return null;still.Add(body.InverseTransformPoint(foot.position));}
   var blends=new List<float>();bool sawWalk=false;
   for(int i=0;i<12;i++){var v=Drive(h,new PlayerInput{Forward=1},1);blends.Add(v.RunBlend);sawWalk|=v.Gait==LocomotionGait.Walk;}
   Assert.IsTrue(sawWalk,"Accelerating passes through the walk blend");
   for(int i=1;i<blends.Count;i++)Assert.GreaterOrEqual(blends[i],blends[i-1]-1e-4f,"Run blend rises with speed");
   var run=world.ObserveAnimation(h);Assert.AreEqual(LocomotionGait.Run,run.Gait);Assert.AreEqual(1,run.RunBlend,.01f);Assert.AreEqual(1,run.StrideDirection);
   var moving=new List<Vector3>();for(int f=0;f<12;f++){world.SubmitInput(h,new PlayerInput{Forward=1});world.Step(.02f);yield return null;moving.Add(body.InverseTransformPoint(foot.position));}
   Assert.Greater(Spread(moving),Spread(still)+.15f,"The real skeleton swings its feet while running");
   Tune(v=>v.HumanSpeed=6);float fast=Drive(h,new PlayerInput{Forward=1},20).CyclesPerSecond;
   Tune(v=>v.HumanSpeed=5);float normal=Drive(h,new PlayerInput{Forward=1},20).CyclesPerSecond;
   Assert.AreEqual(1.2f,fast/normal,.06f,"Run cadence follows ground speed (no foot sliding)");
   Tune(v=>v.HumanSpeed=1.2f);var walk=Drive(h,new PlayerInput{Forward=1},25);Assert.AreEqual(LocomotionGait.Walk,walk.Gait);Assert.Less(walk.RunBlend,.05f);
   Tune(v=>v.HumanSpeed=.9f);var slow=Drive(h,new PlayerInput{Forward=1},25);
   Assert.AreEqual(1.2f/.9f,walk.CyclesPerSecond/slow.CyclesPerSecond,.1f,"Walk cadence follows ground speed");
  }

  [UnityTest] public IEnumerator TappingAndStoppingDoNotFlickerBetweenIdleAndRun() {
   yield return Create(false);int h=Human;
   foreach(float inertia in new[]{0f,.12f}) {
    Tune(v=>v.InertiaSeconds=inertia);Drive(h,new PlayerInput(),20);
    int changes=0;bool wasIdle=true;float lastWeight=world.ObserveAnimation(h).MoveWeight;
    for(int i=0;i<40;i++) {
     var v=Drive(h,i%2==0?new PlayerInput{Forward=1}:new PlayerInput(),1);bool idle=v.Gait==LocomotionGait.Idle;
     if(idle!=wasIdle)changes++;wasIdle=idle;
     Assert.Less(Mathf.Abs(v.MoveWeight-lastWeight),.5f,"Move weight eases, it never snaps");lastWeight=v.MoveWeight;
    }
    Assert.LessOrEqual(changes,1,"Rapid taps keep one moving gait (inertia "+inertia+")");
    Assert.AreEqual(LocomotionGait.Idle,Drive(h,new PlayerInput(),25).Gait,"Releasing settles to idle");
   }
  }

  [UnityTest] public IEnumerator JumpShowsAirborneThenLandingAndReturnsToGround() {
   yield return Create(false);int h=Human;
   world.SubmitInput(h,new PlayerInput{Jump=true});world.Step(.02f);
   var seen=new List<CharacterAnimationView>();
   for(int i=0;i<80;i++){seen.Add(Drive(h,new PlayerInput(),1));}
   int air=seen.FindIndex(v=>v.Gait==LocomotionGait.Airborne);int land=seen.FindIndex(v=>v.Gait==LocomotionGait.Landing);
   Assert.GreaterOrEqual(air,0,"Jump shows the airborne pose");Assert.Greater(land,air,"Touchdown shows a landing");
   Assert.Greater(seen.Skip(air).Take(land-air).Max(v=>v.AirWeight),.8f);
   Assert.Greater(seen.Skip(land).Max(v=>v.Squash),.02f,"Landing compresses the body");
   Assert.AreEqual(LocomotionGait.Idle,seen.Last().Gait);Assert.AreEqual(0,seen.Last().Squash,.01f);Assert.Less(seen.Last().AirWeight,.05f);
  }

  [UnityTest] public IEnumerator AimingHumanStrafesAndBackpedalsInsteadOfMoonwalking() {
   yield return Create(false);int h=Human;
   var animator=world.PlayerTransform(h).GetComponentsInChildren<Animator>().First(a=>a.isHuman);
   CharacterAnimationView view=default;
   view=Drive(h,new PlayerInput{Right=1,Yaw=0},30);yield return new WaitForSeconds(.5f);
   Assert.AreEqual(1,view.StrideDirection);Assert.That(view.LegYaw,Is.InRange(45f,85f),"Legs turn toward the side step");
   Vector3 Axis(HumanBodyBones r,HumanBodyBones l){var d=animator.GetBoneTransform(r).position-animator.GetBoneTransform(l).position;d.y=0;return d;}
   Assert.Less(Vector3.Angle(Axis(HumanBodyBones.RightUpperArm,HumanBodyBones.LeftUpperArm),Vector3.right),25,"Shoulders keep facing the aim");
   Assert.Greater(Vector3.Angle(Axis(HumanBodyBones.RightUpperLeg,HumanBodyBones.LeftUpperLeg),Vector3.right),35,"Hips follow the legs");
   view=Drive(h,new PlayerInput{Forward=-1,Yaw=0},30);yield return new WaitForSeconds(.5f);
   Assert.AreEqual(-1,view.StrideDirection,"Backing away plays the stride in reverse");Assert.Less(Mathf.Abs(view.LegYaw),10);Assert.Greater(view.CyclesPerSecond,0);
   Assert.Less(Vector3.Angle(Axis(HumanBodyBones.RightUpperArm,HumanBodyBones.LeftUpperArm),Vector3.right),25,"Backpedal keeps facing the aim");
   view=Drive(h,new PlayerInput{Forward=1,Yaw=0},30);
   Assert.AreEqual(1,view.StrideDirection);Assert.Less(Mathf.Abs(view.LegYaw),10);
  }

  [UnityTest] public IEnumerator AnimalSpeciesRunOnTheirOwnClipsAndMoveWithDistinctCharacter() {
   yield return Create(true);var animals=world.Observe().Players.Where(p=>p.Faction==Faction.Animal).ToArray();
   Assert.AreEqual(6,animals.Select(p=>p.CharacterId).Distinct().Count());
   var landing=new Dictionary<string,(float squash,float rebound)>();var roll=new Dictionary<string,float>();var pitch=new Dictionary<string,float>();
   foreach(var p in animals) {
    float maxPitch=0;for(int i=0;i<8;i++)maxPitch=Mathf.Max(maxPitch,Drive(p.Slot,new PlayerInput{Forward=1},1).BodyPitch);pitch[p.CharacterId]=maxPitch;
    float maxRoll=0;CharacterAnimationView run=default;for(int i=0;i<45;i++){run=Drive(p.Slot,new PlayerInput{Forward=1},1);if(i>=25)maxRoll=Mathf.Max(maxRoll,Mathf.Abs(run.BodyRoll));}roll[p.CharacterId]=maxRoll;// roll sampled after the heading settles
    Assert.AreEqual(LocomotionGait.Run,run.Gait,p.CharacterId);Assert.IsTrue(run.DrivesAnimator,p.CharacterId);Assert.Greater(run.CyclesPerSecond,0);
    StringAssert.Contains("Run",run.RunClip,p.CharacterId);StringAssert.Contains("Walk",run.WalkClip,p.CharacterId);
    Drive(p.Slot,new PlayerInput(),30);world.SubmitInput(p.Slot,new PlayerInput{Jump=true});world.Step(.02f);
    float squash=0,rebound=0;bool landed=false;
    for(int i=0;i<90;i++){var v=Drive(p.Slot,new PlayerInput(),1);landed|=v.Gait==LocomotionGait.Landing;if(landed){squash=Mathf.Max(squash,v.Squash);rebound=Mathf.Min(rebound,v.Squash);}}
    Assert.IsTrue(landed,p.CharacterId+" lands");landing[p.CharacterId]=(squash,rebound);
    Warp(p.Slot,new Vector3(60+p.Slot*7,0,-20));Physics.SyncTransforms();Drive(p.Slot,new PlayerInput(),10);
   }
   Assert.Greater(landing["animal-bear_grizzly"].squash,landing["animal-fox"].squash*1.2f,"Bear lands heavily");
   Assert.Less(landing["animal-rabbit_brown"].rebound,-.02f,"Rabbit springs back after landing");
   Assert.Greater(landing["animal-bear_grizzly"].rebound,-.01f,"Bear does not bounce");
   Assert.Greater(roll["animal-penguin"],3,"Penguin waddles");Assert.Greater(roll["animal-penguin"],roll["animal-wolf"]*2);
   Assert.Greater(pitch["animal-boar"],pitch["animal-bear_grizzly"],"Boar leans into its charge more than the bear");
   Assert.Greater(pitch["animal-wolf"],1,"Runners lean into acceleration");
   StringAssert.Contains("Jump",world.ObserveAnimation(animals.First(p=>p.CharacterId=="animal-rabbit_brown").Slot).AirClip,"Rabbit uses its own jump clip");
  }

  [UnityTest] public IEnumerator RemoteCharactersUseTheSameAnimatorFromInterpolatedMovement() {
   if(Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog")==null)Assert.Ignore("Owned assets are required for animation integration");
   var source=new PlaytestSession(123);source.StartSolo("source");
   root=new GameObject("remote animation");var remote=root.AddComponent<UnityPlaytestSession>();remote.StartRemote(source.Observe());
   var start=source.Observe().Players[3].Position;float x=start.X;
   for(float t=0;t<.8f;t+=Time.deltaTime){x+=4*Time.deltaTime;var state=source.Observe();state.Players[3].Position=new WorldPosition(x,start.Y,start.Z);remote.ApplyRemoteSnapshot(state);yield return null;}
   var moving=remote.ObserveAnimation(3);Assert.AreNotEqual(LocomotionGait.Idle,moving.Gait,"Remote runner is not stuck idle");Assert.IsTrue(moving.DrivesAnimator);Assert.Greater(moving.CyclesPerSecond,0);
   yield return new WaitForSeconds(.6f);Assert.AreEqual(LocomotionGait.Idle,remote.ObserveAnimation(3).Gait,"Remote stops when snapshots stop");
  }
 }
}
