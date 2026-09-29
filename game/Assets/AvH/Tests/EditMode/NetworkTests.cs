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
 }
}
