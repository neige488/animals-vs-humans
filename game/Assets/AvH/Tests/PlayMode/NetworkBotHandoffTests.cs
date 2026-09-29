using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class NetworkBotHandoffTests {
  [UnityTest] public IEnumerator MovingHumanBotYieldsToReadyPeerAndResumesAfterDisconnect() {
   var hostObject=new GameObject("Bot handoff host");
   var clientObject=new GameObject("Bot handoff client");
   try {
    var world=hostObject.AddComponent<UnityPlaytestSession>();world.AutomaticStep=false;
    var host=hostObject.AddComponent<NetworkPresentation>();host.Initialize(world);host.StartHost("host","127.0.0.1");
    Assert.That(world.BotAutomationEnabled,Is.True,"This integration fixture must exercise the real bot director");
    var initial=world.Observe();
    for(int n=0;n<40;n++){world.Step(.02f);if(n%10==0)yield return null;}
    var moving=world.Observe();
    int expectedSlot=moving.Players.Where(p=>p.IsBot&&p.Faction==Faction.Human).OrderBy(p=>p.Slot).First().Slot;
    Assert.That(FlatDistance(initial.Players[expectedSlot].Position,moving.Players[expectedSlot].Position),Is.GreaterThan(.4f),"The claimed human bot was moving before Ready");
    var clientWorld=clientObject.AddComponent<UnityPlaytestSession>();
    var client=clientObject.AddComponent<NetworkPresentation>();client.Initialize(clientWorld);client.JoinRoom(host.RoomCode,"human guest");
    for(int n=0;n<180&&!client.CanPlay;n++)yield return new WaitForSecondsRealtime(.02f);
    Assert.That(client.CanPlay,Is.True);Assert.That(client.LocalSlot,Is.EqualTo(expectedSlot));
    int slot=client.LocalSlot;var owned=world.Observe().Players[slot];
    Assert.That(owned.IsBot,Is.False);Assert.That(owned.Faction,Is.EqualTo(Faction.Human));
    Assert.That(FlatDistance(owned.Position,moving.Players[slot].Position),Is.LessThan(.001f),"Ready transfers the existing body, without respawning");
    // A neutral input must halt the old bot path while every other bot continues.
    client.SubmitInput(new PlayerInput());yield return new WaitForSecondsRealtime(.15f);
    var stopped=world.Observe();
    for(int n=0;n<20;n++){world.Step(.02f);yield return new WaitForSecondsRealtime(.02f);}
    Assert.That(FlatDistance(stopped.Players[slot].Position,world.Observe().Players[slot].Position),Is.LessThan(.08f),"The bot must not overwrite a person's neutral input");
    // Walk back toward the open spawn area, opposite the shelter approach.
    var beforeInput=world.Observe().Players[slot].Position;
    for(int n=0;n<35;n++){client.SubmitInput(new PlayerInput{Forward=-1,Yaw=0});yield return new WaitForSecondsRealtime(.02f);world.Step(.02f);}
    var afterInput=world.Observe().Players[slot];
    Assert.That(afterInput.Position.Z,Is.LessThan(beforeInput.Z-.8f),"Only the peer's requested movement should control the claimed slot");
    Assert.That(world.Observe().Players.Where(p=>p.IsBot).Any(p=>FlatDistance(stopped.Players[p.Slot].Position,p.Position)>.5f),Is.True,"Other bots continue during peer control");
    client.Leave();
    for(int n=0;n<180&&!world.Observe().Players[slot].IsBot;n++)yield return new WaitForSecondsRealtime(.02f);
    var returned=world.Observe().Players[slot];
    Assert.That(returned.IsBot,Is.True);Assert.That(returned.Slot,Is.EqualTo(slot));Assert.That(returned.Faction,Is.EqualTo(afterInput.Faction));
    Assert.That(FlatDistance(returned.Position,afterInput.Position),Is.LessThan(.001f),"Disconnect retains the current body");
    for(int n=0;n<60;n++){world.Step(.02f);if(n%10==0)yield return null;}
    var resumed=world.Observe().Players[slot];
    Assert.That(resumed.IsBot,Is.True);
    Assert.That(resumed.Position.Z,Is.GreaterThan(returned.Position.Z+.4f),"The same bot resumes toward its shelter, rather than retaining the peer's backward input");
   } finally {Object.Destroy(hostObject);Object.Destroy(clientObject);}
   yield return null;
  }
  static float FlatDistance(WorldPosition a,WorldPosition b)=>Vector2.Distance(new Vector2(a.X,a.Z),new Vector2(b.X,b.Z));
 }
}
