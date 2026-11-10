using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class FishNetPredictionTests {
  sealed class BurstRelay : System.IDisposable {
   readonly UdpClient front=new UdpClient(0),back=new UdpClient(0);readonly List<byte[]> toHost=new List<byte[]>(),toGuest=new List<byte[]>();IPEndPoint guest;double next;
   public bool Burst;public int DropRun;int received;public int Port=>((IPEndPoint)front.Client.LocalEndPoint).Port;
   public BurstRelay(int hostPort){back.Connect(IPAddress.Loopback,hostPort);}
   public void Pump(){
    while(front.Available>0){IPEndPoint sender=null;var data=front.Receive(ref sender);guest=sender;if(Burst&&received++%40<DropRun)continue;toHost.Add(data);}
    while(back.Available>0){IPEndPoint sender=null;toGuest.Add(back.Receive(ref sender));}
    double now=Time.realtimeSinceStartupAsDouble;if(Burst&&now<next)return;next=now+.5;
    foreach(var data in toHost)back.Send(data,data.Length);toHost.Clear();
    if(guest!=null){foreach(var data in toGuest)front.Send(data,data.Length,guest);toGuest.Clear();}
   }
   public void Dispose(){front.Dispose();back.Dispose();}
  }
  [UnityTest] public IEnumerator HalfSecondPacketBurstsDoNotDiscardMovementAndPullOwnerBack()=>BurstMovement(0);
  [UnityTest] public IEnumerator ShortPacketLossRunsDoNotPullOwnerBackAfterRelease()=>BurstMovement(6);
  IEnumerator BurstMovement(int dropRun){
   var h=new GameObject("Burst host");var c=new GameObject("Burst guest");var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(100,-.5f,100);floor.transform.localScale=new Vector3(40,1,40);
   try{
    var hw=h.AddComponent<UnityPlaytestSession>();hw.BotAutomationEnabled=false;var host=h.AddComponent<NetworkPresentation>();host.Initialize(hw);host.StartHost("host","127.0.0.1");
    Assert.IsTrue(PrivateRoomCode.TryParse(host.RoomCode,out string address,out int port,out string key));
    using(var relay=new BurstRelay(port)){
     var cw=c.AddComponent<UnityPlaytestSession>();var client=c.AddComponent<NetworkPresentation>();client.Initialize(cw);client.JoinRoom(PrivateRoomCode.Create("127.0.0.1",relay.Port,key),"guest");
     for(int i=0;i<250&&!client.CanPlay;i++){relay.Pump();yield return new WaitForSecondsRealtime(.02f);}
     Assert.IsTrue(client.CanPlay);int slot=client.LocalSlot;
     for(int i=0;i<100&&!cw.FishControls(slot);i++){relay.Pump();yield return new WaitForSecondsRealtime(.02f);}
     foreach(var world in new[]{hw,cw}){var body=world.PlayerBodyTransform(slot);var cc=body.GetComponent<CharacterController>();cc.enabled=false;body.position=new Vector3(100,.08f,100);cc.enabled=true;}
     for(int i=0;i<70;i++){client.SubmitInput(default);relay.Pump();yield return new WaitForSecondsRealtime(.02f);}
     relay.Burst=true;relay.DropRun=dropRun;var start=cw.PlayerBodyTransform(slot).position;
     for(int i=0;i<100;i++){client.SubmitInput(new PlayerInput{Forward=1});relay.Pump();yield return new WaitForSecondsRealtime(.02f);}
     var release=cw.PlayerBodyTransform(slot).position;float furthest=release.z,maxPullback=0;
     for(int i=0;i<160;i++){client.SubmitInput(default);relay.Pump();yield return new WaitForSecondsRealtime(.02f);var pos=cw.PlayerBodyTransform(slot).position;furthest=Mathf.Max(furthest,pos.z);maxPullback=Mathf.Max(maxPullback,furthest-pos.z);}
     Debug.Log("Burst movement: predicted="+(release.z-start.z)+" authoritative="+(hw.PlayerBodyTransform(slot).position.z-start.z)+" pullback="+maxPullback);
     Assert.Greater(release.z-start.z,3,"Actual forward movement under packet bursts");
     Assert.Less(maxPullback,.25f,"Releasing forward must not trigger repeated backward corrections from discarded inputs");
     Assert.Less(Vector3.Distance(cw.PlayerBodyTransform(slot).position,hw.PlayerBodyTransform(slot).position),.2f);
     client.Leave();host.Leave();
    }
   }finally{Object.Destroy(h);Object.Destroy(c);Object.Destroy(floor);}
  }
  [UnityTest] public IEnumerator OwnerPresentationDoesNotTrailOldMovementForTheRoundTripTime(){
   var h=new GameObject("High RTT host");var c=new GameObject("High RTT guest");var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(100,-.5f,100);floor.transform.localScale=new Vector3(40,1,40);
   try{
    var hw=h.AddComponent<UnityPlaytestSession>();hw.BotAutomationEnabled=false;var host=h.AddComponent<NetworkPresentation>();host.Initialize(hw);host.StartHost("host","127.0.0.1");
    var cw=c.AddComponent<UnityPlaytestSession>();var client=c.AddComponent<NetworkPresentation>();client.Initialize(cw);client.JoinRoom(host.RoomCode,"guest");
    for(int i=0;i<250&&!client.CanPlay;i++)yield return new WaitForSecondsRealtime(.02f);
    Assert.IsTrue(client.CanPlay);int slot=client.LocalSlot;
    for(int i=0;i<100&&!cw.FishControls(slot);i++)yield return new WaitForSecondsRealtime(.02f);
    var bridge=c.GetComponent<FishRoomBridge>();var sim=bridge.Manager.TransportManager.LatencySimulator;
    sim.SetLatency(500);sim.SetEnabled(true);
    for(int i=0;i<250;i++){client.SubmitInput(default);yield return new WaitForSecondsRealtime(.02f);}
    Assert.Greater(bridge.Manager.TimeManager.RoundTripTime,350,"The test actually measures a high-latency connection");
    // Keep movement in a clear lane, away from other controllers and moving scenery.
    foreach(var world in new[]{hw,cw}){var body=world.PlayerBodyTransform(slot);var cc=body.GetComponent<CharacterController>();cc.enabled=false;body.position=new Vector3(100,.08f,100);cc.enabled=true;}
    for(int i=0;i<100;i++){client.SubmitInput(default);yield return new WaitForSecondsRealtime(.02f);}
    var started=cw.PlayerBodyTransform(slot).position;
    for(int i=0;i<80;i++){client.SubmitInput(new PlayerInput{Forward=1,Yaw=0});yield return new WaitForSecondsRealtime(.02f);}
    float trail=Vector3.Distance(cw.CameraFollowTransform(slot).position,cw.PlayerBodyTransform(slot).position);
    Assert.Greater(Vector3.Distance(started,cw.PlayerBodyTransform(slot).position),3,"Exercise actual unblocked movement");
    Debug.Log("High RTT owner trail="+trail+" RTT="+bridge.Manager.TimeManager.RoundTripTime);
    Assert.Less(trail,.45f,"The owner's visible body follows present prediction, rather than an RTT of old movement");
    for(int i=0;i<20;i++){client.SubmitInput(default);yield return new WaitForSecondsRealtime(.02f);}
    Assert.Less(Vector3.Distance(cw.CameraFollowTransform(slot).position,cw.PlayerBodyTransform(slot).position),.15f,"Visible movement stops promptly after key release");
    client.Leave();host.Leave();
   }finally{Object.Destroy(h);Object.Destroy(c);Object.Destroy(floor);}
  }
  [UnityTest] public IEnumerator GuestGraphicsDoNotKeepAnUndergroundInitializationOffset(){
   var h=new GameObject("Grounding host");var c=new GameObject("Grounding guest");
   try{
    var hw=h.AddComponent<UnityPlaytestSession>();hw.BotAutomationEnabled=false;var host=h.AddComponent<NetworkPresentation>();host.Initialize(hw);host.StartHost("host","127.0.0.1");
    var cw=c.AddComponent<UnityPlaytestSession>();var client=c.AddComponent<NetworkPresentation>();client.Initialize(cw);client.JoinRoom(host.RoomCode,"guest");
    for(int i=0;i<250&&!client.CanPlay;i++)yield return new WaitForSecondsRealtime(.02f);
    Assert.IsTrue(client.CanPlay);int slot=client.LocalSlot;
    for(int i=0;i<100&&!cw.FishControls(slot);i++)yield return new WaitForSecondsRealtime(.02f);
    var motor=c.GetComponent<FishRoomBridge>().LocalMotor;Assert.IsNotNull(motor);
    for(int i=0;i<40;i++){client.SubmitInput(default);yield return new WaitForSecondsRealtime(.02f);}
    var body=cw.PlayerBodyTransform(slot);var graphics=body.Find("Predicted presentation");
    // Reconciliation can leave a transient graphical offset before the first render initializes smoothing.
    // Recreate that ordering with a two-metre downward offset. It must never become the permanent follow target.
    var field=typeof(FishPredictedMotor).GetField("smoother",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
    ((FishNet.Component.Transforming.Beta.UniversalTickSmoother)field.GetValue(motor))?.ResetState();field.SetValue(motor,null);
    graphics.localPosition=new Vector3(.3f,-2,.2f);
    for(int i=0;i<100;i++){client.SubmitInput(default);yield return new WaitForSecondsRealtime(.02f);}
    Assert.Greater(body.position.y,-.1f,"Guest physical controller remains above the ground");
    Assert.Less(Vector3.Distance(graphics.position,body.position),.05f,"Temporary pre-initialization offset must settle to the body origin");
    var cc=body.GetComponent<CharacterController>();cc.enabled=false;body.position+=Vector3.up*2;cc.enabled=true;
    for(int i=0;i<100;i++){client.SubmitInput(default);yield return new WaitForSecondsRealtime(.02f);}
    Assert.Greater(graphics.position.y,-.1f,"Graphics remain above the floor after vertical correction");
    Assert.Less(Vector3.Distance(graphics.position,body.position),.05f,"Vertical correction settles the rendered body too");
    client.Leave();host.Leave();
   }finally{Object.Destroy(h);Object.Destroy(c);}
  }
  [UnityTest] public IEnumerator DelayedLostPacketsReplayInputsPreserveJumpAndCorrectFullState(){
   var h=new GameObject("FishNet host");var c=new GameObject("FishNet guest");
   try{
    var hw=h.AddComponent<UnityPlaytestSession>();hw.BotAutomationEnabled=false;var host=h.AddComponent<NetworkPresentation>();host.Initialize(hw);host.StartHost("host","127.0.0.1");
    var cw=c.AddComponent<UnityPlaytestSession>();var client=c.AddComponent<NetworkPresentation>();client.Initialize(cw);client.JoinRoom(host.RoomCode,"guest");
    for(int i=0;i<250&&!client.CanPlay;i++)yield return new WaitForSecondsRealtime(.02f);
    Assert.IsTrue(client.CanPlay,"FishNet room joined");int slot=client.LocalSlot;
    for(int i=0;i<100&&!cw.FishControls(slot);i++)yield return new WaitForSecondsRealtime(.02f);
    Assert.IsTrue(cw.FishControls(slot),"Spawned predicted owner motor");
    var bridge=c.GetComponent<FishRoomBridge>();var sim=bridge.Manager.TransportManager.LatencySimulator;sim.SetLatency(120);sim.SetPacketLoss(.1);sim.SetOutOfOrder(.05);sim.SetEnabled(true);
    for(int i=0;i<40;i++){client.SubmitInput(default);yield return new WaitForSecondsRealtime(.02f);}
    float ownerPeak=0,serverPeak=0;var start=cw.PlayerBodyTransform(slot).position;
    // Single render-frame Jump must survive until its next tick and survive replay.
    client.SubmitInput(new PlayerInput{Jump=true,Yaw=0});
    for(int i=0;i<100;i++){client.SubmitInput(new PlayerInput{Yaw=0});yield return new WaitForSecondsRealtime(.02f);ownerPeak=Mathf.Max(ownerPeak,cw.PlayerBodyTransform(slot).position.y-start.y);serverPeak=Mathf.Max(serverPeak,hw.PlayerBodyTransform(slot).position.y-start.y);}
    Assert.Greater(ownerPeak,.8f,"Immediate predicted jump");Assert.Greater(serverPeak,.8f,"Server received one-time jump through packet loss");
    var before=cw.PlayerBodyTransform(slot).position;
    for(int i=0;i<60;i++){client.SubmitInput(new PlayerInput{Forward=-1});yield return new WaitForSecondsRealtime(.02f);}
    Assert.Greater(Vector3.Distance(before,cw.PlayerBodyTransform(slot).position),1);
    for(int i=0;i<100;i++){client.SubmitInput(default);yield return new WaitForSecondsRealtime(.02f);}
    var motor=Object.FindObjectsByType<FishPredictedMotor>(FindObjectsSortMode.None).First(m=>m.IsOwner&&m.Slot==slot);
    Assert.IsNotNull(cw.PlayerBodyTransform(slot).Find("Predicted presentation/Visual"),"Animator model has a separate smoothed parent");
    Assert.Less(bridge.PacketBytes,bridge.DecodedBytes,"Room snapshot compresses before reliable UDP transport");
    Assert.Less(Vector3.Distance(cw.CameraFollowTransform(slot).position,cw.PlayerBodyTransform(slot).position),.3f,"Rendered body settles with the authoritative motor after stopping");
    Assert.Greater(motor.ReconcileCount,0);Assert.Greater(motor.ReplayCount,0,"Pending inputs actually replay after authoritative restoration");
    Assert.Less(Vector3.Distance(cw.PlayerBodyTransform(slot).position,hw.PlayerBodyTransform(slot).position),.2f,"Owner converges after stopping");
    var cc=cw.PlayerBodyTransform(slot).GetComponent<CharacterController>();cc.enabled=false;cc.transform.position+=Vector3.right;cc.enabled=true;
    for(int i=0;i<100;i++){client.SubmitInput(default);yield return new WaitForSecondsRealtime(.02f);}
    Assert.Less(Vector3.Distance(cw.PlayerBodyTransform(slot).position,hw.PlayerBodyTransform(slot).position),.2f,"Correct deliberate position divergence");
    client.Leave();host.Leave();
   }finally{Object.Destroy(h);Object.Destroy(c);}
  }
 }
}
