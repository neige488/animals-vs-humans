using System.Linq;
using NUnit.Framework;
namespace AvH.Tests {
 // Public session boundary: a shoved prop is a public event every viewer presents (knock sound, dust).
 public class PropEventTests {
  static FeelEvent[] Knocks(PlaytestSession s)=>s.Observe().Events.Where(e=>e.Kind==FeelEventKind.PropPushed).ToArray();
  [Test] public void AShoveIsPublishedOncePerKnockWithWhoAndWhichProp() {
   var s=new PlaytestSession(1);s.StartSolo("host");
   Assert.IsTrue(s.RecordPropPush(3,0,new WorldPosition(1,0,2),.5f));
   var knock=Knocks(s).Single();Assert.AreEqual(0,knock.Actor);Assert.AreEqual(3,knock.Target);Assert.AreEqual(.5f,knock.Strength,1e-6);Assert.AreEqual(2,knock.Position.Z);
   Assert.IsFalse(s.RecordPropPush(3,4,new WorldPosition(1,0,2),1),"The same prop knocks at most once per quarter second");
   Assert.IsTrue(s.RecordPropPush(4,4,new WorldPosition(5,0,2),1),"Another prop is its own knock");
   s.Advance(.3);Assert.IsTrue(s.RecordPropPush(3,4,new WorldPosition(1,0,2),2));Assert.AreEqual(1,Knocks(s).Last().Strength,"Strength is a 0..1 hint");
   Assert.IsFalse(s.RecordPropPush(-1,0,new WorldPosition(0,0,0),1));Assert.IsFalse(s.RecordPropPush(5,0,new WorldPosition(0,0,0),float.NaN));
  }
  [Test] public void NoKnocksDuringTheResultsScreen() {
   var s=new PlaytestSession(1);s.StartSolo("host");var rules=s.ObserveSettings().Current;
   s.Advance(rules.PreparationSeconds+rules.RoundSeconds+.1);Assert.AreEqual(RoundPhase.Results,s.Observe().Phase);
   Assert.IsFalse(s.RecordPropPush(1,0,new WorldPosition(0,0,0),1));
  }
 }
}
