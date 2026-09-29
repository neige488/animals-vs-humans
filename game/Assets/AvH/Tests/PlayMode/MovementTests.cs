using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class MovementTests {
  [UnityTest] public IEnumerator AllRecoveryPointsHaveClearStandingCapsules() {
   var root=new GameObject("recovery clearance");var session=root.AddComponent<UnityPlaytestSession>();
   session.AutomaticStep=false;session.StartSolo("clearance");yield return null;Physics.SyncTransforms();
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
   session.AutomaticStep=false;session.StartSolo("animal tester",seed);yield return null;
   for(int i=0;i<1001;i++)session.Step(.02f);
   Assert.AreEqual(RoundPhase.Chase,session.Observe().Phase);
   var animal=session.Observe().Players.First(p=>p.Slot==0);
   Assert.AreEqual(Faction.Animal,animal.Faction);
   bool recovered=false;
   for(int i=0;i<350;i++) {
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
   session.AutomaticStep=false;session.StartSolo("route tester");yield return null;
   MoveTo(session,-12,-3,130);MoveTo(session,-12,10,250);
   Assert.Greater(session.Observe().Players[0].Position.Y,3.7f,"Rooftop must be reachable by basic movement");
   // Walk off the rooftop toward the map edge; ordinary falling must continue before recovery.
   MoveTo(session,-20,10,150);
   Assert.Less(session.Observe().Players[0].Position.Y,1f,"Ordinary rooftop fall returns to ground without teleport");
   var faction=session.Observe().Players[0].Faction;bool recovered=false;
   for(int i=0;i<250;i++) {
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
   session.AutomaticStep = false;
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
