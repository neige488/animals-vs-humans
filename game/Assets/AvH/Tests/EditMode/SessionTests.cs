using System.Linq;
using NUnit.Framework;
namespace AvH.Tests {
public class SessionTests {
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
