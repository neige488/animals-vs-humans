using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 /// <summary>Jump forgiveness: coyote time off a ledge, a buffered press before landing, and nothing past the window.</summary>
 public class JumpFeelTests {
  static string TempSettings()=>System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");
  static UnityPlaytestSession World(GameObject root){var g=root.AddComponent<UnityPlaytestSession>();g.AutomaticStep=false;g.BotAutomationEnabled=false;g.StartSolo("점프",3,TempSettings(),GameMaps.Warehouse);return g;}
  static void Put(UnityPlaytestSession g,Vector3 at){var b=g.PlayerTransform(0).GetComponent<CharacterController>();b.enabled=false;b.transform.position=at;b.enabled=true;for(int i=0;i<30;i++)g.Step(.02f);}
  static CharacterController Body(UnityPlaytestSession g)=>g.PlayerTransform(0).GetComponent<CharacterController>();

  // Off the open east side of the mezzanine: a press a few frames after the feet leave still jumps; a late one does not.
  [UnityTest] public IEnumerator CoyoteTimeForgivesALatePressOffALedge() {
   foreach(var (late,jumps) in new[]{(.06f,true),(.3f,false)}) {
    var root=new GameObject("coyote");
    try {
     var g=World(root);yield return null;Put(g,new Vector3(19.2f,WarehouseLayout.Upper+.1f,10));
     float off=-1;for(int i=0;i<200;i++){g.SubmitInput(0,new PlayerInput{Forward=1,Yaw=90});g.Step(.02f);if(!Body(g).isGrounded){off=0;break;}}
     Assert.AreEqual(0,off,"walked off the edge");
     for(float t=.02f;t<late;t+=.02f){g.SubmitInput(0,new PlayerInput{Forward=1,Yaw=90});g.Step(.02f);}
     float before=g.PlayerTransform(0).position.y;
     g.SubmitInput(0,new PlayerInput{Forward=1,Yaw=90,Jump=true});g.Step(.02f);
     float highest=before;for(int i=0;i<10;i++){g.SubmitInput(0,new PlayerInput{Forward=1,Yaw=90});g.Step(.02f);highest=Mathf.Max(highest,g.PlayerTransform(0).position.y);}
     if(jumps)Assert.Greater(highest,before+.3f,"a press .06 s after leaving still jumps");else Assert.LessOrEqual(highest,before+.01f,"a press .3 s after leaving does not");
    } finally {Object.Destroy(root);}
    yield return null;
   }
  }

  [UnityTest] public IEnumerator AJumpPressedJustBeforeLandingFiresOnTouchdown() {
   var root=new GameObject("buffer");
   try {
    var g=World(root);yield return null;Put(g,new Vector3(0,.1f,-6));
    g.SubmitInput(0,new PlayerInput{Jump=true});g.Step(.02f);
    bool pressed=false;float rose=0;
    for(int i=0;i<120;i++) {
     var b=Body(g);float y=b.transform.position.y;
     bool press=!pressed&&i>10&&!b.isGrounded&&y<.45f;if(press)pressed=true;
     g.SubmitInput(0,new PlayerInput{Jump=press});g.Step(.02f);
     if(pressed&&g.PlayerTransform(0).position.y>y+.02f&&i>15){rose=g.PlayerTransform(0).position.y;break;}
    }
    Assert.IsTrue(pressed,"pressed while still in the air");Assert.Greater(rose,0,"the buffered press jumps again on landing");
   } finally {Object.Destroy(root);}
   yield return null;
  }
 }
}
