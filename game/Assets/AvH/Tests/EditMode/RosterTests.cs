using System.Linq;
using NUnit.Framework;
namespace AvH.Tests {
 public class RosterTests {
  public static CharacterDefinition[] Definitions(string prefix)=>Enumerable.Range(0,6).Select(i=>new CharacterDefinition(prefix+i,prefix+" 이름 "+i)).ToArray();
  [Test] public void TwelveSlotsUseSixHumansAndBirthsIdentifyEveryAnimalThenReset() {
   var s=new PlaytestSession(123);s.BeginSettingsEdit(0);var rules=s.ObserveSettings().Edit;rules.InitialAttackGrace=0;rules.TransformAttackGrace=0;s.UpdateSettingsEdit(0,rules);s.ApplySettings(0);
   s.StartSolo("tester",humans:Definitions("human"),animals:Definitions("animal"));
   Assert.That(s.Observe().Players.GroupBy(p=>p.CharacterId).Select(g=>g.Count()),Is.All.EqualTo(2));
   s.Advance(20);var state=s.Observe();Assert.AreEqual(2,state.Births.Length);
   int attacker=state.Players.First(p=>p.Faction==Faction.Animal).Slot;
   var point=state.Players[attacker].Position;
   foreach(var victim in state.Players.Where(p=>p.Faction==Faction.Human).Take(4)) {
    s.RecordWorldPosition(victim.Slot,point);
    Assert.IsTrue(s.TryMeleeHit(attacker,victim.Slot,state.Round));attacker=victim.Slot;
   }
   state=s.Observe();CollectionAssert.AreEquivalent(Definitions("animal").Select(c=>c.Id),state.Players.Where(p=>p.Faction==Faction.Animal).Select(p=>p.CharacterId));
   CollectionAssert.AreEquivalent(state.Players.Where(p=>p.Faction==Faction.Animal).Select(p=>p.CharacterName),state.Births.Select(b=>b.Kind));
   Assert.That(state.Births.Select(b=>b.Count),Is.All.EqualTo(1));
   state.Players[0].CharacterId="tampered";Assert.AreNotEqual("tampered",s.Observe().Players[0].CharacterId);
   s.Advance(185);Assert.That(s.Observe().Players.Select(p=>p.CharacterId),Is.All.StartsWith("human"));
  }
 }
}
