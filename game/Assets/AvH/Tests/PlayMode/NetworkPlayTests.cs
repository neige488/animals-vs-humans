using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class NetworkPlayTests {
  [UnityTest] public IEnumerator RealSocketInputMovesAuthoritativeBodyAndRemoteMirror() {
   var h=new GameObject("Network host test");var c=new GameObject("Network client test");
   try {
    var hostWorld=h.AddComponent<UnityPlaytestSession>();hostWorld.BotAutomationEnabled=false;var host=h.AddComponent<NetworkPresentation>();host.Initialize(hostWorld);host.StartHost("host","127.0.0.1");hostWorld.AutomaticStep=false;
    var clientWorld=c.AddComponent<UnityPlaytestSession>();var client=c.AddComponent<NetworkPresentation>();client.Initialize(clientWorld);client.JoinRoom(host.RoomCode,"guest");
    for(int n=0;n<180&&!client.CanPlay;n++)yield return new WaitForSecondsRealtime(.02f);
    Assert.That(client.CanPlay,Is.True);Assert.That(client.LocalSlot,Is.GreaterThan(0));
    int slot=client.LocalSlot;var before=hostWorld.Observe().Players[slot].Position;
    for(int n=0;n<60;n++){client.SubmitInput(new PlayerInput {Forward=-1,Yaw=0});hostWorld.Step(.02f);yield return new WaitForSecondsRealtime(.02f);}
    var after=hostWorld.Observe().Players[slot].Position;
    Assert.That(after.Z,Is.LessThan(before.Z-.5f));Assert.That(clientWorld.IsRemote,Is.True);
    Assert.That(clientWorld.Observe().Players[slot].Position.Z,Is.EqualTo(after.Z).Within(.5f));
    bool sawHostBubble=false,sawRemoteBubble=false;
    for(int n=0;n<30;n++){client.SubmitInput(new PlayerInput{Attack=true,Yaw=0});hostWorld.Step(.02f);yield return new WaitForSecondsRealtime(.02f);sawHostBubble|=hostWorld.ObserveBubbles().Length>0;sawRemoteBubble|=clientWorld.ObserveBubbles().Length>0;}
    Assert.That(sawHostBubble,Is.True);Assert.That(sawRemoteBubble,Is.True);Assert.That(clientWorld.Observe().Players[slot].Ammo,Is.LessThan(12));
    Assert.That(clientWorld.ObserveActiveSettings().Version,Is.EqualTo(hostWorld.ObserveActiveSettings().Version));
    client.Leave();for(int n=0;n<20;n++)yield return new WaitForSecondsRealtime(.02f);Assert.That(hostWorld.Observe().Players[slot].IsBot,Is.True);
   }finally{Object.Destroy(h);Object.Destroy(c);}
  }
  [UnityTest] public IEnumerator RemoteEffectSnapshotReconcilesLargeSetAndRemovesDepartedIds() {
   var go=new GameObject("Remote effects test");
   try {
    var state=new PlaytestSession(2);state.StartSolo("host");var world=go.AddComponent<UnityPlaytestSession>();world.StartRemote(state.Observe());
    var full=new NetworkVisualState{Bubbles=Enumerable.Range(0,1000).Select(i=>new NetworkBubble{Id=i,Position=new WorldPosition(i%20,5,i/20)}).ToArray()};
    world.ApplyRemoteVisuals(full);world.ApplyRemoteVisuals(full);yield return null;
    Assert.That(go.GetComponentsInChildren<Renderer>().Count(r=>r.name=="Remote Bubble"),Is.EqualTo(1000));
    world.ApplyRemoteVisuals(new NetworkVisualState{Bubbles=full.Bubbles.Where(b=>b.Id%2==0).ToArray()});yield return null;
    Assert.That(go.GetComponentsInChildren<Renderer>().Count(r=>r.name=="Remote Bubble"),Is.EqualTo(500));
   } finally{Object.Destroy(go);}
  }
 }
}
