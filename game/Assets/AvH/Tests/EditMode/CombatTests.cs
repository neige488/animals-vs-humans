using System.Linq;
using NUnit.Framework;
namespace AvH.Tests {
 public class CombatTests {
  [Test] public void HumanMagazineHonorsCadenceAndReloadsFromUnlimitedReserve() {
   var s=new PlaytestSession(123);s.StartSolo("tester");var round=s.Observe().Round;
   for(int i=0;i<12;i++) {Assert.IsTrue(s.TryFire(0,round));Assert.IsFalse(s.TryFire(0,round));s.Advance(.31);}
   Assert.AreEqual(0,s.Observe().Players[0].Ammo);Assert.IsFalse(s.TryFire(0,round));
   Assert.IsTrue(s.TryReload(0,round));Assert.IsFalse(s.TryFire(0,round));
   s.Advance(1.5);Assert.AreEqual(12,s.Observe().Players[0].Ammo);Assert.IsTrue(s.TryFire(0,round));
  }
 }
}
