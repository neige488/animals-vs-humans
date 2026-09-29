using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class NetworkPlayTests {
  [UnityTest] public IEnumerator RealSocketInputMovesAuthoritativeBodyAndRemoteMirror() {
   var h=new GameObject("Network host test");var c=new GameObject("Network client test");
   try {
    var hostWorld=h.AddComponent<UnityPlaytestSession>();var host=h.AddComponent<NetworkPresentation>();host.Initialize(hostWorld);host.StartHost("host","127.0.0.1");
    var clientWorld=c.AddComponent<UnityPlaytestSession>();var client=c.AddComponent<NetworkPresentation>();client.Initialize(clientWorld);client.JoinRoom(host.RoomCode,"guest");
    for(int n=0;n<180&&!client.CanPlay;n++)yield return null;
    Assert.That(client.CanPlay,Is.True);Assert.That(client.LocalSlot,Is.GreaterThan(0));
    int slot=client.LocalSlot;var before=hostWorld.Observe().Players[slot].Position;
    for(int n=0;n<60;n++){client.SubmitInput(new PlayerInput {Forward=-1,Yaw=0});yield return null;}
    var after=hostWorld.Observe().Players[slot].Position;
    Assert.That(after.Z,Is.LessThan(before.Z-.5f));Assert.That(clientWorld.IsRemote,Is.True);
    Assert.That(clientWorld.Observe().Players[slot].Position.Z,Is.EqualTo(after.Z).Within(.5f));
    client.Leave();for(int n=0;n<20;n++)yield return null;Assert.That(hostWorld.Observe().Players[slot].IsBot,Is.True);
   }finally{Object.Destroy(h);Object.Destroy(c);}
  }
 }
}
