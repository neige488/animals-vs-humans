using System.Linq;
using NUnit.Framework;
namespace AvH.Tests {
 public class CombatTests {
  [Test] public void DeadlineTieBelongsToHumansAndPriorRoundCommandsCannotChangeNextRound() {
   foreach(double delta in new[]{-1e-10,0,1e-10}) {
    var s=new PlaytestSession(123);s.StartSolo("tester");s.Advance(22);
    var state=s.Observe();int attacker=state.Players.First(p=>p.Faction==Faction.Animal).Slot;
    var victims=state.Players.Where(p=>p.Faction==Faction.Human).Select(p=>p.Slot).ToArray();
    foreach(var victim in victims){s.RecordWorldPosition(victim,state.Players[attacker].Position);}
    foreach(var victim in victims.Take(victims.Length-1)){Assert.IsTrue(s.TryMeleeHit(attacker,victim,state.Round));s.Advance(.51);}
    s.Advance(s.Observe().SecondsRemaining+delta);
    Assert.AreEqual(delta<0,s.TryMeleeHit(attacker,victims.Last(),state.Round));
    Assert.AreEqual(delta<0?Faction.Animal:Faction.Human,s.Observe().Winner);
    s.Advance(5.01);Assert.AreEqual(state.Round+1,s.Observe().Round);
    Assert.IsFalse(s.TryMeleeHit(attacker,victims.Last(),state.Round));Assert.IsFalse(s.TryFire(0,state.Round));
    Assert.AreEqual(12,s.Observe().Players.Count(p=>p.Faction==Faction.Human));
   }
  }

  [Test] public void MeleeRequiresAttackAndGraceThenTransformsAtCurrentPosition() {
   var s=new PlaytestSession(123);s.StartSolo("tester");s.Advance(20);
   var state=s.Observe();int attacker=state.Players.First(p=>p.Faction==Faction.Animal).Slot;
   int victim=state.Players.First(p=>p.Faction==Faction.Human).Slot;
   var point=state.Players[attacker].Position;s.RecordWorldPosition(victim,point);
   Assert.AreEqual(Faction.Human,s.Observe().Players[victim].Faction,"Contact alone cannot transform");
   Assert.IsFalse(s.TryMeleeHit(attacker,victim,state.Round));s.Advance(2);
   Assert.IsTrue(s.TryMeleeHit(attacker,victim,state.Round));
   var transformed=s.Observe().Players[victim];Assert.AreEqual(Faction.Animal,transformed.Faction);Assert.AreEqual(point,transformed.Position);
   Assert.AreEqual(1,transformed.AttackGraceRemaining);Assert.IsFalse(s.TryMeleeHit(victim,attacker,state.Round));
   Assert.AreEqual(1,s.Observe().Births.Single().Count);
  }

  [Test] public void HumanMagazineHonorsCadenceAndReloadsFromUnlimitedReserve() {
   var s=new PlaytestSession(123);s.StartSolo("tester");var round=s.Observe().Round;
   for(int i=0;i<12;i++) {Assert.IsTrue(s.TryFire(0,round));Assert.IsFalse(s.TryFire(0,round));s.Advance(.31);}
   Assert.AreEqual(0,s.Observe().Players[0].Ammo);Assert.IsFalse(s.TryFire(0,round));
   Assert.IsTrue(s.TryReload(0,round));Assert.IsFalse(s.TryFire(0,round));
   s.Advance(1.5);Assert.AreEqual(12,s.Observe().Players[0].Ammo);Assert.IsTrue(s.TryFire(0,round));
  }
 }
}
