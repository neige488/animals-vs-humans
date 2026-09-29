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
 [Test] public void InvalidEditsAndCancelledDefaultsPreserveExistingReservation() {
  var s=new PlaytestSession(1);s.StartSolo("host");s.BeginSettingsEdit(0);
  var v=s.ObserveSettings().Edit;v.RoundSeconds=60;v.InitialAttackGrace=0;v.TransformAttackGrace=0;
  s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettings(0));
  s.BeginSettingsEdit(0);v.RoundSeconds=-1;s.UpdateSettingsEdit(0,v);Assert.IsFalse(s.ApplySettings(0));
  Assert.IsTrue(s.ObserveSettings().Errors.ContainsKey("RoundSeconds"));
  Assert.AreEqual(60,s.ObserveSettings().Pending.RoundSeconds);
  s.RestoreSettingsDefaults(0);s.CancelSettingsEdit(0);
  Assert.AreEqual(60,s.ObserveSettings().Pending.RoundSeconds);Assert.AreEqual(180,s.ObserveSettings().Current.RoundSeconds);
 }
}}
