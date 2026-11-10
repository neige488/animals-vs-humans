using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class FishNetConnectionTests {
  [UnityTest] public IEnumerator UnansweredInitialConnectionReportsJoinFailure(){
   var go=new GameObject("Unanswered guest");
   using(var silent=new UdpClient(0))try{
    var world=go.AddComponent<UnityPlaytestSession>();var network=go.AddComponent<NetworkPresentation>();network.Initialize(world);
    var bridge=go.GetComponent<FishRoomBridge>();int port=((IPEndPoint)silent.Client.LocalEndPoint).Port;
    network.JoinRoom(PrivateRoomCode.Create("127.0.0.1",port,new string('a',32)),"guest");
    for(int i=0;i<600&&bridge.Status!=ConnectionStatus.Failed&&bridge.Status!=ConnectionStatus.Interrupted;i++)yield return new WaitForSecondsRealtime(.02f);
    Assert.AreEqual(ConnectionStatus.Failed,bridge.Status,"A room never entered is a join failure, not an interrupted match");
    Assert.IsNull(world.Session);network.Leave();
   }finally{Object.Destroy(go);}
  }
  [UnityTest] public IEnumerator WrongRoomKeyExplainsFailureAndCanRetryTheSameHost(){
   var h=new GameObject("Admission host");var c=new GameObject("Admission guest");
   try{
    var hw=h.AddComponent<UnityPlaytestSession>();hw.BotAutomationEnabled=false;var host=h.AddComponent<NetworkPresentation>();host.Initialize(hw);host.StartHost("host","127.0.0.1");
    var cw=c.AddComponent<UnityPlaytestSession>();var client=c.AddComponent<NetworkPresentation>();client.Initialize(cw);var bridge=c.GetComponent<FishRoomBridge>();
    Assert.IsTrue(PrivateRoomCode.TryParse(host.RoomCode,out string address,out int port,out string key));
    client.JoinRoom(PrivateRoomCode.Create(address,port,new string('b',32)),"guest");
    for(int i=0;i<250&&bridge.Status!=ConnectionStatus.Failed&&bridge.Status!=ConnectionStatus.Interrupted;i++)yield return new WaitForSecondsRealtime(.02f);
    Assert.AreEqual(ConnectionStatus.Failed,bridge.Status);Assert.That(bridge.Error,Does.Contain("코드"));
    client.JoinRoom(host.RoomCode,"guest");
    for(int i=0;i<250&&!client.CanPlay;i++)yield return new WaitForSecondsRealtime(.02f);
    Assert.IsTrue(client.CanPlay,"An explicit retry connects after rejection");client.Leave();host.Leave();
   }finally{Object.Destroy(h);Object.Destroy(c);}
  }
 }
}
