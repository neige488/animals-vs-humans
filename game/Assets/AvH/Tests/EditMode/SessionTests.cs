using System.Linq;
using NUnit.Framework;
namespace AvH.Tests {
public class SessionTests {
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
