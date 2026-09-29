using System.Linq;
using NUnit.Framework;
namespace AvH.Tests {
public class SessionTests {
 [Test] public void InvalidPublicCommandsPreserveSessionState() {
  var session = new PlaytestSession(123);
  Assert.Throws<System.InvalidOperationException>(() => session.Advance(1));
  foreach(var nickname in new[]{"", "   ", new string('a',21)})
   Assert.Throws<System.ArgumentException>(() => session.StartSolo(nickname));
  session.StartSolo("테스터");
  var before=session.Observe();
  foreach(var time in new[]{-1d, double.NaN, double.PositiveInfinity})
   Assert.Throws<System.ArgumentOutOfRangeException>(() => session.Advance(time));
  foreach(var slot in new[]{-1,12})
   Assert.Throws<System.ArgumentOutOfRangeException>(() => session.RecordWorldPosition(slot,new WorldPosition(1,2,3)));
  var after=session.Observe();
  Assert.AreEqual(before.Round,after.Round);
  Assert.AreEqual(before.SecondsRemaining,after.SecondsRemaining);
  CollectionAssert.AreEqual(before.Players.Select(p=>p.Position),after.Players.Select(p=>p.Position));
 }
 [Test] public void BirthNoticeUsesTheSelectedAnimalIdentity() {
  var session = new PlaytestSession(456);
  session.StartSolo("테스터", "여우", "에픽");
  session.Advance(20);
  Assert.AreEqual("여우", session.Observe().Births.Single().Kind);
  Assert.AreEqual("에픽", session.Observe().Births.Single().Rarity);
 }

 [Test] public void PreparationSelectsTwoAtCurrentPositionAndSurvivalRepeatsRound() {
  var session = new PlaytestSession(123);
  session.StartSolo("테스터");
  var initial = session.Observe();
  session.Advance(19.9);
  Assert.AreEqual(0, session.Observe().Players.Count(p => p.Faction == Faction.Animal));
  session.Advance(0.1);
  var chase = session.Observe();
  Assert.AreEqual(RoundPhase.Chase, chase.Phase);
  Assert.AreEqual(2, chase.Players.Count(p => p.Faction == Faction.Animal));
  CollectionAssert.AreEqual(initial.Players.Select(p => p.Position), chase.Players.Select(p => p.Position));
  Assert.AreEqual(2, chase.Births.Single().Count);
  Assert.AreEqual("일반", chase.Births.Single().Rarity);
  session.Advance(180);
  Assert.AreEqual(RoundPhase.Results, session.Observe().Phase);
  Assert.AreEqual(Faction.Human, session.Observe().Winner);
  session.Advance(5);
  Assert.AreEqual(2, session.Observe().Round);
  Assert.AreEqual(12, session.Observe().Players.Count(p => p.Faction == Faction.Human));
 }

 [Test] public void SoloStartCreatesTwelveSeparatedHumanSlots() {
  var session = new PlaytestSession(123);
  session.StartSolo("테스터");
  var state = session.Observe();
  Assert.AreEqual(12, state.Players.Length);
  Assert.AreEqual(11, state.Players.Count(p => p.IsBot));
  Assert.AreEqual(12, state.Players.Count(p => p.Faction == Faction.Human));
  Assert.AreEqual(12, state.Players.Select(p => p.Position).Distinct().Count());
  Assert.AreEqual("테스터", state.Players[0].Nickname);
  Assert.AreEqual(RoundPhase.Preparation, state.Phase);
  Assert.AreEqual(20, state.SecondsRemaining);
 }
}}
