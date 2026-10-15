using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 // Real CharacterController movement through the public session boundary.
 public class MotionPlayTests {
  string profile;GameObject root;UnityPlaytestSession world;
  [TearDown] public void Cleanup(){if(root!=null)Object.Destroy(root);if(profile!=null&&File.Exists(profile))File.Delete(profile);}
  IEnumerator Create(float inertia) {
   profile=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   root=new GameObject("motion physics");world=root.AddComponent<UnityPlaytestSession>();world.AutomaticStep=false;world.BotAutomationEnabled=false;world.StartSolo("host",7,profile);
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(100,-.5f,0);floor.transform.localScale=new Vector3(100,1,40);
   var s=world.Session;s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;v.InertiaSeconds=inertia;s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettingsNow(0));
   foreach(var p in world.Observe().Players)Warp(p.Slot,new Vector3(70+p.Slot*4,0,-12));Warp(0,new Vector3(90,0,0));
   yield return new WaitForFixedUpdate();Physics.SyncTransforms();for(int i=0;i<20;i++)world.Step(.02f);
  }
  void Warp(int slot,Vector3 position){var body=world.PlayerTransform(slot).GetComponent<CharacterController>();body.enabled=false;body.transform.position=position;body.enabled=true;world.Session.RecordWorldPosition(slot,new WorldPosition(position.x,position.y,position.z));}
  float X=>world.PlayerTransform(0).position.x;
  float Move(int steps,float right){float from=X;for(int i=0;i<steps;i++){world.SubmitInput(0,new PlayerInput{Right=right});world.Step(.02f);}return X-from;}
  [UnityTest] public IEnumerator ZeroInertiaMovesAtFullSpeedFromTheFirstStepAndStopsDead() {
   yield return Create(0);float speed=world.Session.ObserveSettings().Current.HumanSpeed;
   Assert.AreEqual(speed*.02f,Move(1,1),.01f,"First step already at full speed");
   Assert.AreEqual(speed*.2f,Move(10,1),.02f);
   Assert.AreEqual(0,Move(5,0),.005f,"Releasing input stops on the next step");
  }
  [UnityTest] public IEnumerator DefaultInertiaEasesInAndOutButKeepsTopSpeed() {
   yield return Create(.12f);float speed=world.Session.ObserveSettings().Current.HumanSpeed;
   float first=Move(3,1);Assert.Less(first,speed*.06f*.75f,"Start is eased, not instant");Assert.Greater(first,0);
   Move(10,1);
   Assert.AreEqual(speed*.2f,Move(10,1),.02f,"Top speed is unchanged once reached");
   float coast=Move(15,0);Assert.Greater(coast,.05f,"Body carries a little momentum after release");Assert.Less(coast,speed*.12f,"...but stops within the short inertia time");
   Assert.AreEqual(0,Move(5,0),.005f,"Fully stopped afterwards");
  }
 }
}
