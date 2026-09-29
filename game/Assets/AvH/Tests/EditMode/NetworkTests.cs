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
    world.RecordWorldPosition(1,new WorldPosition(9,1,8));world.TryFire(1,1);world.TryReload(1,1);client.Ready();Until(host,client,()=>client.Status==ConnectionStatus.Playing);
    Assert.That(client.Snapshot.Players[1].Ammo,Is.EqualTo(11));Assert.That(client.Snapshot.Players[1].ReloadRemaining,Is.EqualTo(1.5));
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
    host.Dispose();Until(host,client,()=>client.Status==ConnectionStatus.Interrupted);Assert.That(client.Status,Is.EqualTo(ConnectionStatus.Interrupted));Assert.That(client.Snapshot,Is.Null);
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
    world.Advance(200);var overflow=clients.Single(c=>c.Status==ConnectionStatus.Failed);overflow.Connect(host.RoomCode,"retry");Until(host,overflow,()=>overflow.Status==ConnectionStatus.Loading);overflow.Ready();Until(host,overflow,()=>overflow.Status==ConnectionStatus.Failed);
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
  [Test] public void ClientReceivesHostCurrentAndReservedSettingsWithoutEditAuthority() {
   var world=new PlaytestSession(2);world.StartSolo("host");world.BeginSettingsEdit(0);var rules=world.ObserveSettings().Edit;rules.HumanSpeed=8;world.UpdateSettingsEdit(0,rules);world.ApplySettings(0);
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var client=new PrivateRoomClient()) {
    client.Connect(host.RoomCode,"guest");Until(host,client,()=>client.Status==ConnectionStatus.Loading);client.Ready();Until(host,client,()=>client.Status==ConnectionStatus.Playing);
    Assert.That(client.Visuals.CurrentRules.HumanSpeed,Is.EqualTo(5));Assert.That(client.Visuals.HasPending,Is.True);Assert.That(client.Visuals.PendingRules.HumanSpeed,Is.EqualTo(8));
    world.Advance(205);Until(host,client,()=>client.Visuals.SettingsVersion==2);Assert.That(client.Visuals.CurrentRules.HumanSpeed,Is.EqualTo(8));Assert.That(client.Visuals.HasPending,Is.False);
   }
  }
  [Test] public void AnimalBotFallbackKeepsFactionPositionAndAttackGrace() {
   int seed=Enumerable.Range(0,100).First(n=>{var w=new PlaytestSession(n);w.StartSolo("host");w.Advance(20);return w.Observe().Players[0].Faction==Faction.Human;});
   var world=new PlaytestSession(seed);world.BeginSettingsEdit(0);var rules=world.ObserveSettings().Edit;rules.TransformAttackGrace=30;world.UpdateSettingsEdit(0,rules);world.ApplySettings(0);world.StartSolo("host");
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var client=new PrivateRoomClient()) {
    client.Connect(host.RoomCode,"guest");Until(host,client,()=>client.Status==ConnectionStatus.Loading);
    world.Advance(22);int attacker=world.Observe().Players.First(p=>p.Faction==Faction.Animal).Slot;
    foreach(var victim in world.Observe().Players.Where(p=>p.IsBot&&p.Faction==Faction.Human)){world.RecordWorldPosition(victim.Slot,world.Observe().Players[attacker].Position);Assert.That(world.TryMeleeHit(attacker,victim.Slot,1),Is.True);world.Advance(.6);}
    var before=world.Observe();client.Ready();Until(host,client,()=>client.Status==ConnectionStatus.Playing);
    Assert.That(client.Snapshot.Players[client.Slot].Faction,Is.EqualTo(Faction.Animal));
    Assert.That(client.Snapshot.Players[client.Slot].AttackGraceRemaining,Is.EqualTo(before.Players[client.Slot].AttackGraceRemaining));
    Assert.That(client.Snapshot.Players[client.Slot].Position.X,Is.EqualTo(before.Players[client.Slot].Position.X));
    Assert.That(client.Snapshot.Players.Count(p=>p.Faction==Faction.Human),Is.EqualTo(1));
   }
  }
  [Test] public void SilentConnectedPeerTimesOutAndReturnsItsSlotToBot() {
   var world=new PlaytestSession(2);world.StartSolo("host");
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var client=new PrivateRoomClient()) {
    client.Connect(host.RoomCode,"guest");Until(host,client,()=>client.Status==ConnectionStatus.Loading);client.Ready();Until(host,client,()=>client.Status==ConnectionStatus.Playing);int slot=client.Slot;
    var deadline=DateTime.UtcNow.AddSeconds(12);while(!world.Observe().Players[slot].IsBot&&DateTime.UtcNow<deadline){host.Pump();Thread.Sleep(100);}
    Assert.That(world.Observe().Players[slot].IsBot,Is.True);
   }
  }
  [Test] public void LargeVisualSnapshotUsesBoundedFramesAndCommitsCompleteBubbleSet() {
   var world=new PlaytestSession(2);world.StartSolo("host");
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var client=new PrivateRoomClient()) {
    host.CaptureVisuals=()=>new NetworkVisualState{Bubbles=Enumerable.Range(0,1500).Select(i=>new NetworkBubble{Id=i,Round=1,Position=new WorldPosition(i,1,0)}).ToArray()};
    client.Connect(host.RoomCode,"guest");Until(host,client,()=>client.Status==ConnectionStatus.Loading);client.Ready();Until(host,client,()=>client.Visuals!=null&&client.Visuals.Bubbles.Length==1500);
    Assert.That(client.Status,Is.EqualTo(ConnectionStatus.Playing));Assert.That(client.Visuals.Bubbles[1499].Id,Is.EqualTo(1499));
   }
  }
  [Test] public void WireSchemaIsExplicitAndPreservedForStandalone() {
   Assert.That(RoomProtocol.Version,Is.EqualTo("avh-private-3"));
   Assert.That(RoomProtocol.SchemaFingerprint(),Is.EqualTo("O1rOwjdYSzQP+ATOll1kh9LPBWWqGOnn46MpfAzGTV4="),"Schema changes require explicit protocol version and guard update");
   foreach(var t in new[]{typeof(SessionState),typeof(PlayerState),typeof(WorldPosition),typeof(PlaytestValues),typeof(NetworkInput),typeof(NetworkVisualState),typeof(NetworkBubble),typeof(NetworkBurst),typeof(BirthNotice)}) {
    Assert.That(t.GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance),Is.Empty,t.Name+" wire contract must use fields");
    Assert.That(t.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance).Length,Is.GreaterThan(0),t.Name);
   }
   var path=System.IO.File.Exists("Assets/AvH/link.xml")?"Assets/AvH/link.xml":"game/Assets/AvH/link.xml";
   Assert.That(System.IO.File.Exists(path),Is.True,"Standalone linker must preserve AvH.Core DTOs");
   StringAssert.Contains("fullname=\"AvH.Core\" preserve=\"all\"",System.IO.File.ReadAllText(path));
  }
  [Test] public void ConnectedClientsShareOneVisualCapturePerHostPump() {
   var world=new PlaytestSession(2);world.StartSolo("host");int captures=0;
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var a=new PrivateRoomClient())using(var b=new PrivateRoomClient()) {
    host.CaptureVisuals=()=>{captures++;return new NetworkVisualState();};
    a.Connect(host.RoomCode,"a");Until(host,a,()=>a.Status==ConnectionStatus.Loading);a.Ready();Until(host,a,()=>a.Status==ConnectionStatus.Playing);
    b.Connect(host.RoomCode,"b");Until(host,b,()=>b.Status==ConnectionStatus.Loading);b.Ready();Until(host,b,()=>b.Status==ConnectionStatus.Playing);
    captures=0;host.Pump();Assert.That(captures,Is.EqualTo(1));
   }
  }
  [Test] public void SessionClaimsAreAtomicAndOnlyMatchingIdentityCanRelease() {
   var world=new PlaytestSession(3);world.StartSolo("host");string a=Guid.NewGuid().ToString("N"),b=Guid.NewGuid().ToString("N");int slot,duplicate;
   Assert.That(world.TryClaimSlot(a,"a",out slot),Is.True);Assert.That(world.TryClaimSlot(a,"duplicate",out duplicate),Is.False);
   Assert.That(world.ReleaseSlotToBot(slot,b),Is.False);Assert.That(world.Observe().Players[slot].IsBot,Is.False);
   world.RecordWorldPosition(slot,new WorldPosition(8,1,9));Assert.That(world.ReleaseSlotToBot(slot,a),Is.True);int restored;
   Assert.That(world.TryClaimSlot(a,"renamed",out restored),Is.True);Assert.That(restored,Is.EqualTo(slot));Assert.That(world.Observe().Players[restored].Position.X,Is.EqualTo(8));
   Assert.That(typeof(PlaytestSession).GetMethod("SetSlotOwner"),Is.Null,"No public raw ownership setter");
  }
  [Test] public void MixedBotPoolPrefersHumanAndKeepsLatestWeaponAndGrace() {
   int seed=-1;for(int n=0;n<100;n++){var trial=new PlaytestSession(n);trial.StartSolo("host");trial.Advance(20);if(trial.Observe().Players[1].Faction==Faction.Animal){seed=n;break;}}
   Assert.That(seed,Is.GreaterThanOrEqualTo(0),"Fixture needs lowest bot slot to be an animal");
   var world=new PlaytestSession(seed);world.StartSolo("host");world.Advance(20);
   var animalSlots=world.Observe().Players.Where(p=>p.IsBot&&p.Faction==Faction.Animal).Select(p=>p.Slot).ToArray();
   int expected=world.Observe().Players.First(p=>p.IsBot&&p.Faction==Faction.Human).Slot;
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var client=new PrivateRoomClient()) {
    client.Connect(host.RoomCode,"guest");Until(host,client,()=>client.Status==ConnectionStatus.Loading);
    world.RecordWorldPosition(expected,new WorldPosition(9,2,7));world.TryFire(expected,1);world.TryReload(expected,1);var latest=world.Observe().Players[expected];
    client.Ready();Until(host,client,()=>client.Status==ConnectionStatus.Playing);var assigned=client.Snapshot.Players[client.Slot];
    Assert.That(client.Slot,Is.EqualTo(expected));Assert.That(assigned.Faction,Is.EqualTo(Faction.Human));
    Assert.That(assigned.Position.X,Is.EqualTo(latest.Position.X));Assert.That(assigned.Position.Y,Is.EqualTo(latest.Position.Y));Assert.That(assigned.Position.Z,Is.EqualTo(latest.Position.Z));
    Assert.That(assigned.Ammo,Is.EqualTo(latest.Ammo));Assert.That(assigned.ReloadRemaining,Is.EqualTo(latest.ReloadRemaining));Assert.That(assigned.AttackGraceRemaining,Is.EqualTo(latest.AttackGraceRemaining));
    foreach(int slot in animalSlots)Assert.That(client.Snapshot.Players[slot].IsBot,Is.True);
   }
  }
  [Test] public void CompletingChunkedVisualsPublishesNewSnapshotWithoutMutatingOldOne() {
   var world=new PlaytestSession(2);world.StartSolo("host");
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var client=new PrivateRoomClient()) {
    host.CaptureVisuals=()=>new NetworkVisualState{Bubbles=Enumerable.Range(0,1500).Select(n=>new NetworkBubble{Id=n}).ToArray()};
    client.Connect(host.RoomCode,"guest");Until(host,client,()=>client.Status==ConnectionStatus.Loading);
    NetworkVisualState first=null;for(int n=0;n<50&&first==null;n++){host.Pump();client.Pump();first=client.Visuals;Thread.Sleep(2);}
    Assert.That(first,Is.Not.Null);Assert.That(first.Bubbles,Is.Empty);
    Until(host,client,()=>client.Visuals.Bubbles.Length==1500);
    Assert.That(ReferenceEquals(first,client.Visuals),Is.False);Assert.That(first.Bubbles,Is.Empty);
   }
  }
  [Test] public void SlowReaderDoesNotLoseSlotOrBlockAnotherJoin() {
   var world=new PlaytestSession(2);world.StartSolo("host");
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var slow=new PrivateRoomClient())using(var other=new PrivateRoomClient()) {
    slow.Connect(host.RoomCode,"slow");Until(host,slow,()=>slow.Status==ConnectionStatus.Loading);slow.Ready();Until(host,slow,()=>slow.Status==ConnectionStatus.Playing);int slot=slow.Slot;
    var bubbles=Enumerable.Range(0,1500).Select(n=>new NetworkBubble{Id=n}).ToArray();host.CaptureVisuals=()=>new NetworkVisualState{Bubbles=bubbles};
    for(int n=0;n<400;n++)host.Pump();
    Assert.That(world.Observe().Players[slot].IsBot,Is.False,"Slow receiver is not a disconnected participant");
    other.Connect(host.RoomCode,"other");Until(host,other,()=>other.Status==ConnectionStatus.Loading);other.Ready();Until(host,other,()=>other.Status==ConnectionStatus.Playing);
   }
  }
  [Test] public void ProgressingLargeSnapshotCanFinishAfterTwoSeconds() {
   var world=new PlaytestSession(2);world.StartSolo("host");var now=DateTime.UtcNow;
   using(var host=new PrivateRoomHost(world,"127.0.0.1"))using(var client=new PrivateRoomClient(clock:()=>now)) {
    client.Connect(host.RoomCode,"guest");Until(host,client,()=>client.Status==ConnectionStatus.Loading);client.Ready();Until(host,client,()=>client.Status==ConnectionStatus.Playing);
    var items=Enumerable.Range(0,12000).Select(n=>new NetworkBubble{Id=n}).ToArray();host.CaptureVisuals=()=>new NetworkVisualState{Bubbles=items};
    var started=now;for(int n=0;n<30&&client.Visuals.Bubbles.Length!=12000;n++){now=now.AddSeconds(.3);host.Pump();client.Pump();Thread.Sleep(2);}
    Assert.That((now-started).TotalSeconds,Is.GreaterThan(2));Assert.That(client.Visuals.Bubbles.Length,Is.EqualTo(12000));Assert.That(client.VisualsDelayed,Is.False);
   }
  }
 }
}
