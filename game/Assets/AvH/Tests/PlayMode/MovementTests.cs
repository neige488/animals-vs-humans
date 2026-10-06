using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class MovementTests {
  [UnityTest] public IEnumerator InvalidInputAndStepLeaveWorldUnchanged() {
   var root=new GameObject("invalid inputs");var session=root.AddComponent<UnityPlaytestSession>();session.AutomaticStep=false; session.BotAutomationEnabled=false;
   session.StartSolo("tester");
   for(int i=0;i<50;i++)session.Step(.02f);
   var before=session.Observe();var position=session.PlayerTransform(0).position;
   session.Step(0);session.Step(float.NaN);session.Step(float.PositiveInfinity);
   Assert.AreEqual(before.SecondsRemaining,session.Observe().SecondsRemaining);
   Assert.AreEqual(position,session.PlayerTransform(0).position);
   session.SubmitInput(0,new PlayerInput {Right=float.NaN,Forward=1});
   for(int i=0;i<10;i++)session.Step(.02f);
   var after=session.PlayerTransform(0).position;
   Assert.AreEqual(position.x,after.x,.001f);Assert.AreEqual(position.z,after.z,.001f);
   Object.Destroy(root);yield return null;
  }
  [UnityTest] public IEnumerator OwnedCharactersStandIdleWithFeetAtControllerGround() {
   if(Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog")==null)Assert.Ignore("Owned assets are required for visual integration");
   var root=new GameObject("owned pose");var session=root.AddComponent<UnityPlaytestSession>();session.AutomaticStep=false; session.BotAutomationEnabled=false;
   session.StartSolo("pose");session.Step(20.1f);yield return new WaitForSeconds(2);
   var state=session.Observe();
   foreach(var faction in new[]{Faction.Human,Faction.Animal}) {
    int slot=state.Players.First(p=>p.Faction==faction).Slot;var body=session.PlayerTransform(slot);
    foreach(var animator in body.GetComponentsInChildren<Animator>()) {
     var clips=animator.GetCurrentAnimatorClipInfo(0);
     foreach(var clip in clips)Assert.IsFalse(clip.clip.name.ToLowerInvariant().Contains("crouch"),"Standing idle must not use a crouch demo override");
    }
    var rendered=body.GetComponentsInChildren<SkinnedMeshRenderer>();
    Assert.IsNotEmpty(rendered,"Both factions must have a visible mesh");
    var visibleBounds=rendered[0].bounds;foreach(var renderer in rendered)visibleBounds.Encapsulate(renderer.bounds);
    if(faction==Faction.Human)Assert.That(visibleBounds.size.y,Is.EqualTo(1.88f).Within(.2f));
    else Assert.That(visibleBounds.size.y,Is.InRange(.3f,4f),"Each animal has its own authored bounds; exact mesh grounding is checked below and for all six in RosterWorldTests");
    float bottom=float.MaxValue;
    foreach(var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>()) {
     var mesh=new Mesh();renderer.BakeMesh(mesh,true);
     foreach(var vertex in mesh.vertices)bottom=Mathf.Min(bottom,renderer.transform.TransformPoint(vertex).y);
     Object.Destroy(mesh);
    }
    Assert.Less(Mathf.Abs(bottom-body.position.y),.15f,faction+" feet must align with controller ground");
   }
   Object.Destroy(root);yield return null;
  }

  [UnityTest] public IEnumerator TurningPassesThroughIntermediateAnglesAndAcceptsAnyHeading() {
   var root=new GameObject("smooth rotation");var session=root.AddComponent<UnityPlaytestSession>();
   session.AutomaticStep=false;session.BotAutomationEnabled=false;session.StartSolo("turn");yield return null;
   session.SubmitInput(0,new PlayerInput{Right=1});session.Step(.02f);
   float first=session.PlayerTransform(0).eulerAngles.y;Assert.Greater(first,0);Assert.Less(first,45,"No instant 8-way snap");
   for(int i=0;i<12;i++){session.SubmitInput(0,new PlayerInput{Forward=1,Yaw=123});session.Step(.02f);}
   Assert.Less(Mathf.Abs(Mathf.DeltaAngle(session.PlayerTransform(0).eulerAngles.y,123)),.1f);
   Object.Destroy(root);yield return null;
  }

  [UnityTest] public IEnumerator RemoteFacingSmoothlyCatchesUpAndSnapsAtWarp() {
   var source=new PlaytestSession(123);source.StartSolo("source");
   var root=new GameObject("remote facing");var remote=root.AddComponent<UnityPlaytestSession>();remote.StartRemote(source.Observe());
   var first=RemoteFrames.Facing(source.Observe(),0,0,10);remote.ApplyRemoteVisuals(first);
   Assert.Less(Mathf.Abs(Mathf.DeltaAngle(remote.PlayerTransform(0).eulerAngles.y,10)),.01f);
   var next=RemoteFrames.Facing(source.Observe(),.05,0,123);remote.ApplyRemoteVisuals(next);yield return null;
   float angle=remote.PlayerTransform(0).eulerAngles.y;Assert.Greater(angle,10);Assert.Less(angle,123);
   var reset=source.Observe();reset.Players[0].Position=new WorldPosition(12,1,0);remote.ApplyRemoteSnapshot(reset,true);
   var resetVisual=RemoteFrames.Facing(reset,.1,0,270);remote.ApplyRemoteVisuals(resetVisual);
   Assert.Less(Mathf.Abs(Mathf.DeltaAngle(remote.PlayerTransform(0).eulerAngles.y,270)),.01f);
   Object.Destroy(root);yield return null;
  }

  [UnityTest] public IEnumerator AllRecoveryPointsHaveClearStandingCapsules() {
   var root=new GameObject("recovery clearance");var session=root.AddComponent<UnityPlaytestSession>();
   session.AutomaticStep=false; session.BotAutomationEnabled=false;session.StartSolo("clearance");yield return null;Physics.SyncTransforms();
   foreach(var point in session.RecoveryPoints)
    Assert.IsFalse(Physics.CheckCapsule(point+Vector3.up*.38f,point+Vector3.up*1.42f,.38f,~0,QueryTriggerInteraction.Ignore),"Recovery capsule overlaps terrain: "+point);
   Object.Destroy(root);yield return null;
  }
  [UnityTest] public IEnumerator AnimalMapEdgeRecoveryPreservesFactionAndSlot() {
   // Randomness is controlled at the public start boundary; select a seed that makes slot zero an animal.
   int seed=0;
   for(;seed<1000;seed++){var probe=new PlaytestSession(seed);probe.StartSolo("probe");probe.Advance(20);if(probe.Observe().Players[0].Faction==Faction.Animal)break;}
   Assert.Less(seed,1000);
   var root=new GameObject("animal recovery");var session=root.AddComponent<UnityPlaytestSession>();
   session.AutomaticStep=false; session.BotAutomationEnabled=false;session.StartSolo("animal tester",seed);yield return null;
   for(int i=0;i<1001;i++)session.Step(.02f);
   Assert.AreEqual(RoundPhase.Chase,session.Observe().Phase);
   var animal=session.Observe().Players.First(p=>p.Slot==0);
   Assert.AreEqual(Faction.Animal,animal.Faction);
   bool recovered=false;
   for(int i=0;i<45;i++){session.SubmitInput(animal.Slot,new PlayerInput{Forward=1});session.Step(.02f);}
   for(int i=0;i<650;i++) {
    var before=session.Observe().Players[animal.Slot].Position;
    session.SubmitInput(animal.Slot,new PlayerInput{Right=-1});session.Step(.02f);
    var after=session.Observe().Players[animal.Slot];
    if(before.Y< -8 && after.Position.Y>0){recovered=true;Assert.AreEqual(animal.Faction,after.Faction);Assert.AreEqual(animal.Slot,after.Slot);break;}
   }
   Assert.IsTrue(recovered,"Selected animal must actually fall and recover during Chase");
   Object.Destroy(root);yield return null;
  }

  [UnityTest] public IEnumerator BasicInputsReachRooftopAndRecoverFromMapEdge() {
   var root=new GameObject("route session");var session=root.AddComponent<UnityPlaytestSession>();
   session.AutomaticStep=false; session.BotAutomationEnabled=false;session.StartSolo("route tester");yield return null;
   MoveTo(session,TownLayout.RoofApproach.x,0,220);MoveTo(session,TownLayout.RoofApproach.x,TownLayout.RoofApproach.z,180);MoveTo(session,TownLayout.Roof.x,TownLayout.Roof.z,250);
   Assert.Greater(session.Observe().Players[0].Position.Y,3.7f,"Rooftop must be reachable by basic movement");
   // Walk off the rooftop toward the map edge; ordinary falling must continue before recovery.
   MoveTo(session,TownLayout.Roof.x-8,TownLayout.Roof.z,180);
   Assert.Less(session.Observe().Players[0].Position.Y,1f,"Ordinary rooftop fall returns to ground without teleport");
   MoveTo(session,TownLayout.Roof.x-8,0,350);
   var faction=session.Observe().Players[0].Faction;bool recovered=false;
   for(int i=0;i<500;i++) {
    var before=session.Observe().Players[0].Position;
    session.SubmitInput(0,new PlayerInput{Right=-1});session.Step(.02f);
    var after=session.Observe().Players[0].Position;
    if(before.Y< -8 && after.Y>0){recovered=true;Assert.AreEqual(faction,session.Observe().Players[0].Faction);break;}
   }
   Assert.IsTrue(recovered,"Map-edge fall must return to a ground point");
   Object.Destroy(root);yield return null;
  }
  static void MoveTo(UnityPlaytestSession session,float x,float z,int maxSteps) {
   for(int i=0;i<maxSteps;i++) {
    var p=session.Observe().Players[0].Position;var delta=new Vector2(x-p.X,z-p.Z);
    if(delta.magnitude<.15f)break;delta=delta.normalized;
    session.SubmitInput(0,new PlayerInput{Right=delta.x,Forward=delta.y});session.Step(.02f);
   }
  }

  [UnityTest] public IEnumerator PublicMovementInputMovesAndJumpsInActualWorld() {
   var root = new GameObject("test session");
   var session = root.AddComponent<UnityPlaytestSession>();
   session.AutomaticStep = false; session.BotAutomationEnabled=false;
   session.StartSolo("tester");
   yield return null;
   var before = session.Observe().Players[0].Position;
   for(int i=0;i<50;i++) { session.SubmitInput(0, new PlayerInput { Right=-1 }); session.Step(0.02f); }
   var after = session.Observe().Players[0].Position;
   // Move into clear plaza space; forward runs into another starting slot.
   Assert.Less(after.X, before.X - 3);
   session.SubmitInput(0, new PlayerInput { Jump=true });
   session.Step(0.1f);
   Assert.Greater(session.Observe().Players[0].Position.Y, after.Y + 0.1f);
   Object.Destroy(root);
   yield return null;
  }
 }
}
