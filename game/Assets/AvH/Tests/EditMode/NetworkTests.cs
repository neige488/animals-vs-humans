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
  [Test] public void HostRejectsNonFiniteInputAndDeliversOnlyCurrentRoundCommands() {
   var world=new PlaytestSession(2);world.StartSolo("host");int accepted=0;
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var client=new PrivateRoomClient()) {
    host.InputReceived=(slot,input)=>{if(input.RoundId!=0)accepted++;};
    client.Connect(host.RoomCode,"guest");Until(host,client,()=>client.Status==ConnectionStatus.Loading);client.Ready();Until(host,client,()=>client.Status==ConnectionStatus.Playing);
    client.SubmitInput(new NetworkInput {Right=float.NaN,RoundId=1});for(int n=0;n<20;n++){host.Pump();client.Pump();Thread.Sleep(2);}
    Assert.That(accepted,Is.EqualTo(0));
    client.SubmitInput(new NetworkInput {Forward=1,RoundId=1});Until(host,client,()=>accepted==1);
    client.SubmitInput(new NetworkInput {Attack=true,RoundId=99});for(int n=0;n<20;n++){host.Pump();client.Pump();Thread.Sleep(2);}
    Assert.That(accepted,Is.EqualTo(1));
   }
  }
  [Test] public void TwelveClientRaceIsAtomicAndDuplicateIdentityCannotTakeTwoSlots() {
   var world=new PlaytestSession(2);world.StartSolo("host");var clients=Enumerable.Range(0,12).Select(_=>new PrivateRoomClient()).ToArray();
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))try {
    foreach(var c in clients)c.Connect(host.RoomCode,"guest");
    for(int n=0;n<100;n++){host.Pump();foreach(var c in clients){c.Pump();if(c.Status==ConnectionStatus.Loading)c.Ready();}Thread.Sleep(2);}
    Assert.That(clients.Count(c=>c.Status==ConnectionStatus.Playing),Is.EqualTo(11));Assert.That(clients.Count(c=>c.Status==ConnectionStatus.Failed),Is.EqualTo(1));
    Assert.That(world.Observe().Players.Count(p=>!p.IsBot),Is.EqualTo(12));Assert.That(clients.Where(c=>c.Slot>=0).Select(c=>c.Slot).Distinct().Count(),Is.EqualTo(11));
   }finally{foreach(var c in clients)c.Dispose();}
   world=new PlaytestSession(2);world.StartSolo("host");string identity=Guid.NewGuid().ToString("N");
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var a=new PrivateRoomClient(identity))using(var b=new PrivateRoomClient(identity)) {
    a.Connect(host.RoomCode,"a");Until(host,a,()=>a.Status==ConnectionStatus.Loading);a.Ready();Until(host,a,()=>a.Status==ConnectionStatus.Playing);
    b.Connect(host.RoomCode,"b");Until(host,b,()=>b.Status==ConnectionStatus.Failed);Assert.That(world.Observe().Players.Count(p=>!p.IsBot),Is.EqualTo(2));
   }
  }
  [Test] public void ResultsWaitForPreparationAndCancellationNeverEnters() {
   var world=new PlaytestSession(2);world.StartSolo("host");world.Advance(200);
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var client=new PrivateRoomClient()) {
    client.Connect(host.RoomCode,"guest");Until(host,client,()=>client.Status==ConnectionStatus.Loading);client.Ready();
    for(int i=0;i<20;i++){host.Pump();client.Pump();Thread.Sleep(2);}
    Assert.That(client.Status,Is.EqualTo(ConnectionStatus.Waiting));Assert.That(world.Observe().Players.Count(p=>!p.IsBot),Is.EqualTo(1));
    world.Advance(5);Until(host,client,()=>client.Status==ConnectionStatus.Playing);Assert.That(client.Snapshot.Round,Is.EqualTo(2));
    client.Cancel();for(int i=0;i<20;i++){host.Pump();Thread.Sleep(2);}
    client.Connect(host.RoomCode,"guest");client.Cancel();for(int i=0;i<20;i++){host.Pump();client.Pump();Thread.Sleep(2);}
    Assert.That(client.Status,Is.EqualTo(ConnectionStatus.Idle));Assert.That(world.Observe().Players.Count(p=>!p.IsBot),Is.EqualTo(1));
   }
  }
  [Test] public void OversizedSocketFrameIsDroppedWithoutBreakingRoom() {
   var world=new PlaytestSession(2);world.StartSolo("host");
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var client=new PrivateRoomClient()) {
    string address,key;int port;PrivateRoomCode.TryParse(host.RoomCode,out address,out port,out key);
    using(var bad=new System.Net.Sockets.TcpClient(address,port)){var b=BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(999999));bad.GetStream().Write(b,0,4);host.Pump();}
    client.Connect(host.RoomCode,"guest");Until(host,client,()=>client.Status==ConnectionStatus.Loading);client.Ready();Until(host,client,()=>client.Status==ConnectionStatus.Playing);
    Assert.That(world.Observe().Players.Count(p=>!p.IsBot),Is.EqualTo(2));
   }
  }
 }
}
