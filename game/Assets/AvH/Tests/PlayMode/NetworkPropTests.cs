using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 // Real sockets: every remote view shows the host's pushed props where the host has them.
 public class NetworkPropTests {
  readonly List<GameObject> roots=new List<GameObject>();
  [TearDown] public void Cleanup(){foreach(var r in roots)if(r!=null)Object.Destroy(r);roots.Clear();}
  UnityPlaytestSession hostWorld;
  NetworkPresentation View(string name,out UnityPlaytestSession world){var go=new GameObject(name);roots.Add(go);world=go.AddComponent<UnityPlaytestSession>();var view=go.AddComponent<NetworkPresentation>();view.Initialize(world);return view;}
  IEnumerator Host(int steps,PlayerInput? input=null){for(int n=0;n<steps;n++){if(input.HasValue)hostWorld.SubmitInput(0,input.Value);hostWorld.Step(.02f);yield return new WaitForSecondsRealtime(.02f);}}
  static PropView Of(UnityPlaytestSession world,int index)=>world.ObserveProps().Single(p=>p.Index==index);
  static void Same(PropView host,PropView remote,string who) {
   Assert.Less(Vector3.Distance(host.Position,remote.Position),.03f,who+" sees the prop where the host has it");
   Assert.AreEqual(0,Mathf.DeltaAngle(host.Yaw,remote.Yaw),1,who+" sees the prop facing the host's way");
  }

  [UnityTest] public IEnumerator RemoteViewsShowTheHostsPushedPropsAndLateJoinersGetTheirCurrentSpot() {
   if(Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog")==null)Assert.Ignore("The market props come from the owned village");
   var host=View("prop host",out hostWorld);hostWorld.BotAutomationEnabled=false;host.StartHost("host","127.0.0.1");hostWorld.AutomaticStep=false;
   var client=View("prop client",out var clientWorld);client.JoinRoom(host.RoomCode,"guest");
   for(int n=0;n<180&&!client.CanPlay;n++)yield return Host(1);
   Assert.IsTrue(client.CanPlay);
   PropScene.Park(hostWorld);var (prop,away)=PropScene.Open(hostWorld,3,4);PropScene.Warp(hostWorld,0,prop.Home+away*3);Physics.SyncTransforms();
   int moving=0,carried=0;
   for(int n=0;n<50;n++){
    yield return Host(1,new PlayerInput{Forward=1,Yaw=PropScene.YawOf(-away)});
    var frame=hostWorld.CaptureMotion();
    if(Of(hostWorld,prop.Index).Moving){moving++;if(frame.Props.Any(p=>p.Index==prop.Index))carried++;}
   }
   Assert.Greater(moving,5,"The host body shoves the prop");Assert.AreEqual(moving,carried,"A moving prop rides every host frame");
   yield return Host(120,new PlayerInput());
   var rested=Of(hostWorld,prop.Index);Assert.IsFalse(rested.Moving);Assert.Greater(Vector3.Distance(rested.Position,prop.Home),.5f);
   Same(rested,Of(clientWorld,prop.Index),"The client");
   // At rest only a periodic refresh carries the displaced prop; untouched props never ride the frame.
   int refreshes=0;
   for(int n=0;n<20;n++){hostWorld.Step(.05f);var frame=hostWorld.CaptureMotion();
    refreshes+=frame.Props.Count(p=>p.Index==prop.Index);
    foreach(var p in frame.Props)Assert.Greater(Vector3.Distance(Of(hostWorld,p.Index).Position,Of(hostWorld,p.Index).Home),.001f,"Only displaced props are refreshed");}
   Assert.That(refreshes,Is.InRange(1,3),"A resting prop is refreshed about twice a second, not every frame");

   var late=View("prop late joiner",out var lateWorld);late.JoinRoom(host.RoomCode,"late");
   for(int n=0;n<180&&!late.CanPlay;n++)yield return Host(1);
   Assert.IsTrue(late.CanPlay);yield return Host(30);
   Same(Of(hostWorld,prop.Index),Of(lateWorld,prop.Index),"A late joiner");

   int round=hostWorld.Observe().Round;
   for(int guard=0;guard<10&&hostWorld.Observe().Round==round;guard++){hostWorld.Session.Advance(System.Math.Max(0,hostWorld.Observe().SecondsRemaining-.01));yield return Host(1);}
   Assert.AreEqual(round+1,hostWorld.Observe().Round);yield return Host(20);
   foreach(var world in new[]{hostWorld,clientWorld,lateWorld}){var back=Of(world,prop.Index);Assert.Less(Vector3.Distance(back.Position,back.Home),.01f,"Every view starts the new round with the prop home");}
  }
 }
}
