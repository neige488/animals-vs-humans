using System.Linq;
using NUnit.Framework;
namespace AvH.Tests {
 // Per-map balance on top of the host settings: chase length and first-animal count, measured in bot matches.
 public class MapBalanceTests {
  [SetUp] public void On()=>GameMaps.PerMapBalance=true;
  [TearDown] public void Off()=>GameMaps.PerMapBalance=false;
  static PlaytestSession Chase(int map,int initialAnimals=2) {
   var s=new PlaytestSession(5);s.StartSolo("tester",map:map);
   s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;v.InitialAnimals=initialAnimals;v.RoundSeconds=180;s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettingsNow(0));
   s.Advance(20.01);Assert.AreEqual(RoundPhase.Chase,s.Observe().Phase);return s;
  }
  [TestCase(GameMaps.Town,4,180)]
  [TestCase(GameMaps.Warehouse,2,180)]
  [TestCase(GameMaps.Crossing,2,216)]
  [TestCase(GameMaps.Sky,2,135)]
  [TestCase(GameMaps.Snow,2,180)]
  [TestCase(GameMaps.Ruins,2,216)]
  public void EachMapStartsItsChaseWithItsOwnCountAndClock(int map,int animals,double seconds) {
   var s=Chase(map);var state=s.Observe();
   Assert.AreEqual(animals,state.Players.Count(p=>p.Faction==Faction.Animal));
   Assert.AreEqual(seconds,state.SecondsRemaining,.02);
  }
  [Test] public void TheExtraNeverLeavesNoHumans() {
   var s=Chase(GameMaps.Town,11);Assert.AreEqual(11,s.Observe().Players.Count(p=>p.Faction==Faction.Animal));
   Assert.AreEqual(11,GameMaps.FirstAnimals(GameMaps.Town,11,12));Assert.AreEqual(1,GameMaps.FirstAnimals(GameMaps.Warehouse,0,12));
  }
  [Test] public void SettingsPanelNamesWhatTheMapChanges() {
   StringAssert.Contains("최초 동물 +2",GameMaps.BalanceNote(GameMaps.Town));StringAssert.Contains("×0.75",GameMaps.BalanceNote(GameMaps.Sky));
   Assert.AreEqual("",GameMaps.BalanceNote(GameMaps.Warehouse));
   GameMaps.PerMapBalance=false;Assert.AreEqual("",GameMaps.BalanceNote(GameMaps.Town));Assert.AreEqual(2,GameMaps.FirstAnimals(GameMaps.Town,2,12));Assert.AreEqual(180,GameMaps.ChaseSeconds(GameMaps.Sky,180));
  }
 }
}
