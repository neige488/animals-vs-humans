using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 // Light market props: bodies and bubbles shove them, they never block a path, and each round starts with them home.
 public class PropTests {
  GameObject root;UnityPlaytestSession world;string profile;
  [TearDown] public void Cleanup(){if(root!=null)Object.Destroy(root);if(profile!=null&&System.IO.File.Exists(profile))System.IO.File.Delete(profile);}
  IEnumerator Village() {
   if(Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog")==null)Assert.Ignore("The market props come from the owned village");
   root=new GameObject("prop evidence");world=root.AddComponent<UnityPlaytestSession>();world.AutomaticStep=false;world.BotAutomationEnabled=false;
   profile=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   world.StartSolo("props",123,profile);yield return null;Physics.SyncTransforms();
   // Everyone else waits far from the market so only the tested body touches a prop.
   PropScene.Park(world);
  }
  void Warp(int slot,Vector3 position)=>PropScene.Warp(world,slot,position);
  void Drive(int slot,PlayerInput input,int steps){for(int i=0;i<steps;i++){world.SubmitInput(slot,input);world.Step(.02f);}}
  static float YawOf(Vector3 direction)=>PropScene.YawOf(direction);
  (PropView prop,Vector3 away) Open(float before,float after,float minHeight=0)=>PropScene.Open(world,before,after,minHeight);
  PropView Now(int index)=>world.ObserveProps().Single(p=>p.Index==index);

  [UnityTest] public IEnumerator LightPropsStandAwayFromSheltersAndStartHome() {
   yield return Village();
   var props=world.ObserveProps();
   Assert.GreaterOrEqual(props.Length,8,"Crates, barrels and pots of the market are pushable");
   CollectionAssert.IsSubsetOf(props.Select(p=>p.Kind).Distinct().ToArray(),new[]{"crate","barrel","pot"},"Only light props move");
   foreach(var kind in new[]{"crate","barrel","pot"})Assert.IsTrue(props.Any(p=>p.Kind==kind),"Pushable "+kind);
   foreach(var p in props) {
    Assert.IsFalse(TownLayout.NearShelter(p.Home),$"Prop {p.Index} stands inside or at the entrance of a shelter: {p.Home}");
    foreach(var shelter in world.ShelterPoints)Assert.GreaterOrEqual(Vector2.Distance(new Vector2(p.Home.x,p.Home.z),new Vector2(shelter.x,shelter.z)),TownLayout.PropClearance);
    Assert.AreEqual(p.Home,p.Position);Assert.AreEqual(p.HomeYaw,p.Yaw);Assert.IsFalse(p.Moving);
    Assert.That(p.Radius,Is.InRange(.15f,.7f));Assert.That(p.Height,Is.InRange(.05f,1.6f));
   }
  }

  [UnityTest] public IEnumerator BodyShovesAPropAsideAndKeepsWalking() {
   yield return Village();
   var (prop,away)=Open(3,4);Warp(0,prop.Home+away*3);Physics.SyncTransforms();
   var from=world.PlayerTransform(0).position;
   Drive(0,new PlayerInput{Forward=1,Yaw=YawOf(-away)},80);
   var shoved=Now(prop.Index);var walked=Vector3.Dot(world.PlayerTransform(0).position-from,-away);
   Assert.Greater(Vector3.Distance(shoved.Position,prop.Home),.5f,"The body shoves the prop");
   Assert.Greater(Vector3.Dot(shoved.Position-prop.Home,-away),0,"The prop goes the way it was pushed");
   Assert.Greater(walked,5,"The prop never stops the body: it walked past the prop's spot");
   Drive(0,new PlayerInput(),150);
   Assert.IsFalse(Now(prop.Index).Moving,"A shoved prop slides to a rest");
  }

  [UnityTest] public IEnumerator APropWedgedAgainstACurbDoesNotBlockTheBody() {
   yield return Village();
   var (prop,away)=Open(3,4);
   // A curb a body steps over but a prop cannot slide across.
   var curb=GameObject.CreatePrimitive(PrimitiveType.Cube);curb.transform.SetParent(root.transform);
   curb.transform.position=prop.Home-away*(prop.Radius+.45f)+Vector3.up*.12f;curb.transform.rotation=Quaternion.LookRotation(away);curb.transform.localScale=new Vector3(3,.24f,.3f);
   Warp(0,prop.Home+away*3);Physics.SyncTransforms();var from=world.PlayerTransform(0).position;
   Drive(0,new PlayerInput{Forward=1,Yaw=YawOf(-away)},90);
   var wedged=Now(prop.Index);
   Assert.Greater(Vector3.Dot(wedged.Position-curb.transform.position,away),0,"The prop stops at the curb instead of passing through it");
   Assert.Greater(Vector3.Dot(world.PlayerTransform(0).position-from,-away),4.5f,"The body walks through the wedged prop and over the curb");
  }

  [UnityTest] public IEnumerator ABubbleHitShovesAProp() {
   yield return Village();
   var (prop,away)=Open(4,3,.5f);Warp(0,prop.Home+away*4);Physics.SyncTransforms();
   float pitch=Mathf.Atan2(1.3f-prop.Height*.5f,4)*Mathf.Rad2Deg;
   Drive(0,new PlayerInput{Yaw=YawOf(-away),Pitch=pitch},5);
   Drive(0,new PlayerInput{Attack=true,Yaw=YawOf(-away),Pitch=pitch},1);
   Drive(0,new PlayerInput{Yaw=YawOf(-away),Pitch=pitch},50);
   var hit=Now(prop.Index);
   Assert.Greater(Vector3.Dot(hit.Position-prop.Home,-away),.3f,"The bubble pushes the prop along its flight");
   Assert.AreEqual(0,world.ObserveBubbles().Length,"The bubble pops on the prop");
  }

  [UnityTest] public IEnumerator EveryRoundStartsWithEachPropBackHome() {
   yield return Village();
   var (prop,away)=Open(3,4);Warp(0,prop.Home+away*3);Physics.SyncTransforms();
   Drive(0,new PlayerInput{Forward=1,Yaw=YawOf(-away)},60);
   Assert.Greater(Vector3.Distance(Now(prop.Index).Position,prop.Home),.5f);
   int round=world.Observe().Round;
   for(int guard=0;guard<10&&world.Observe().Round==round;guard++){world.Session.Advance(System.Math.Max(0,world.Observe().SecondsRemaining-.01));world.Step(.02f);}
   Assert.AreEqual(round+1,world.Observe().Round);
   foreach(var p in world.ObserveProps()){Assert.Less(Vector3.Distance(p.Position,p.Home),.001f,$"Prop {p.Index} is back home");Assert.AreEqual(p.HomeYaw,p.Yaw,.01f);Assert.IsFalse(p.Moving);}
  }
 }
}
