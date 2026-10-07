using NUnit.Framework;
using UnityEngine;
namespace AvH.Tests {
 // A bubble sweep meets a prop's upright cylinder wherever it first enters: side, top or bottom.
 public class PropSweepTests {
  GameObject village;
  [TearDown] public void Cleanup(){if(village!=null)Object.DestroyImmediate(village);}
  // One prop: radius 0.5, height 0.8, standing on y=0 at the origin.
  PushProps Prop() {
   village=new GameObject("sweep village");var container=new GameObject(PushProps.Container);container.transform.SetParent(village.transform,false);
   var crate=GameObject.CreatePrimitive(PrimitiveType.Cube);crate.name="SM_Prop_Crate_01(Clone)";Object.DestroyImmediate(crate.GetComponent<Collider>());
   crate.transform.SetParent(container.transform,false);crate.transform.localScale=new Vector3(1,.8f,1);crate.transform.position=new Vector3(0,.4f,0);
   var props=PushProps.Adopt(village.transform);Assert.AreEqual(1,props.Count);Assert.AreEqual(.5f,props.Observe()[0].Radius,1e-4);Assert.AreEqual(.8f,props.Observe()[0].Height,1e-4);
   return props;
  }
  static Vector3 Down(float degrees)=>new Vector3(Mathf.Cos(degrees*Mathf.Deg2Rad),-Mathf.Sin(degrees*Mathf.Deg2Rad),0);
  [Test] public void ASteepShotFromAboveEntersThroughTheTop() {
   var props=Prop();
   // Starts above the footprint (inside the circle) and 1.3 m up: the top (y 0.8 + bubble 0.1) is reached after (1.3-0.9)/sin 60°.
   Assert.IsTrue(props.Hit(new Vector3(-.2f,1.3f,0),Down(60),.1f,2,out int index,out float at),"A shot down onto the top hits");
   Assert.AreEqual(0,index);Assert.AreEqual(.4f/Mathf.Sin(60*Mathf.Deg2Rad),at,.002f);
  }
  [Test] public void ALongFrameSweepFromAboveOutsideStillFindsTheTop() {
   var props=Prop();
   // Outside the circle and high: the side entry is above the top, the first contact is the top face.
   var origin=new Vector3(-1.5f,2.5f,0);var direction=Down(50);
   Assert.IsTrue(props.Hit(origin,direction,.1f,4,out _,out float at));
   var contact=origin+direction*at;Assert.AreEqual(.9f,contact.y,.002f,"Contact on the top face");Assert.LessOrEqual(Mathf.Abs(contact.x),.6f+.002f);
  }
  [Test] public void ASideShotStillHitsTheSideAndShotsOverOrUnderMiss() {
   var props=Prop();
   Assert.IsTrue(props.Hit(new Vector3(-3,.4f,0),Vector3.right,.1f,5,out _,out float at));Assert.AreEqual(2.4f,at,.002f);
   Assert.IsFalse(props.Hit(new Vector3(-3,1.2f,0),Vector3.right,.1f,6,out _,out _),"Flies over the top");
   Assert.IsFalse(props.Hit(new Vector3(-.2f,1.3f,0),Down(60),.1f,.3f,out _,out _),"Stops short of the top this frame");
  }
 }
}
