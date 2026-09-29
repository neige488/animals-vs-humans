using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
namespace AvH.Tests {
 public class NetworkTests {
  static void Until(PrivateRoomHost host, PrivateRoomClient client, Func<bool> done) {
   for(int i=0;i<300&&!done();i++){host.Pump();client.Pump();Thread.Sleep(2);} Assert.That(done(),Is.True,client.Error);
  }
  [Test] public void RealSocketReadyTakesLatestBotAndDisconnectPreservesWorld() {
   var world=new PlaytestSession(1);world.StartSolo("host");
   using(var host=new PrivateRoomHost(world,"127.0.0.1")) using(var client=new PrivateRoomClient()) {
    client.Connect(host.RoomCode,"guest"); Until(host,client,()=>client.Status==ConnectionStatus.Loading);
    Assert.That(world.Observe().Players.Count(p=>!p.IsBot),Is.EqualTo(1));
    world.RecordWorldPosition(1,new WorldPosition(9,1,8));client.Ready();Until(host,client,()=>client.Status==ConnectionStatus.Playing);
    Assert.That(client.Slot,Is.EqualTo(1));Assert.That(client.Snapshot.Players[1].Position.X,Is.EqualTo(9));
    client.Cancel();for(int i=0;i<20;i++){host.Pump();Thread.Sleep(2);}
    Assert.That(world.Observe().Players[1].IsBot,Is.True);Assert.That(world.Observe().Players[1].Position.X,Is.EqualTo(9));
   }
  }
  [Test] public void LoadingReconnectCannotStealOccupiedSlotByNicknameAndUsesLatestAnimal() {
   var world=new PlaytestSession(2);world.StartSolo("host");
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var client=new PrivateRoomClient())using(var impostor=new PrivateRoomClient()) {
    client.Connect(host.RoomCode,"same");Until(host,client,()=>client.Status==ConnectionStatus.Loading);client.Ready();Until(host,client,()=>client.Status==ConnectionStatus.Playing);
    int original=client.Slot;client.Cancel();for(int i=0;i<20;i++){host.Pump();Thread.Sleep(2);}
    world.Advance(20);world.RecordWorldPosition(original,new WorldPosition(7,1,7));
    client.Connect(host.RoomCode,"renamed");Until(host,client,()=>client.Status==ConnectionStatus.Loading);client.Ready();Until(host,client,()=>client.Status==ConnectionStatus.Playing);
    Assert.That(client.Slot,Is.EqualTo(original));Assert.That(client.Snapshot.Players[original].Position.X,Is.EqualTo(7));
    impostor.Connect(host.RoomCode,"renamed");Until(host,impostor,()=>impostor.Status==ConnectionStatus.Loading);impostor.Ready();Until(host,impostor,()=>impostor.Status==ConnectionStatus.Playing);
    Assert.That(impostor.Slot,Is.Not.EqualTo(original));
    host.Dispose();client.Pump();Assert.That(client.Status,Is.EqualTo(ConnectionStatus.Interrupted));Assert.That(client.Snapshot,Is.Null);
   }
  }
  [Test] public void WireRejectsMismatchedBuildBeforeReadiness() {
   var world=new PlaytestSession(2);world.StartSolo("host");
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var client=new PrivateRoomClient(protocolVersion:"other-build")) {
    client.Connect(host.RoomCode,"guest");Until(host,client,()=>client.Status==ConnectionStatus.Failed);
    Assert.That(world.Observe().Players.Count(p=>!p.IsBot),Is.EqualTo(1));
   }
  }
 }
}
