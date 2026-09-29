using NUnit.Framework;
namespace AvH.Tests {
public class SettingsTests {
 [Test] public void HostEditsStaySeparateUntilNextRoundAndGuestsCannotChangeThem() {
  var session=new PlaytestSession(1); session.StartSolo("host");
  Assert.IsFalse(session.BeginSettingsEdit(1)); Assert.IsTrue(session.BeginSettingsEdit(0));
  var values=session.ObserveSettings().Edit; values.RoundSeconds=60;
  session.UpdateSettingsEdit(0,values); Assert.IsTrue(session.ApplySettings(0));
  Assert.AreEqual(180,session.ObserveSettings().Current.RoundSeconds);
  Assert.AreEqual(60,session.ObserveSettings().Pending.RoundSeconds);
  session.Advance(205);
  Assert.AreEqual(60,session.ObserveSettings().Current.RoundSeconds);
  session.Advance(20); Assert.AreEqual(60,session.Observe().SecondsRemaining);
 }
}}
