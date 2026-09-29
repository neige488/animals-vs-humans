using NUnit.Framework;
namespace AvH.Tests {
public class SettingsTests {
 [Test] public void ValidXmlWithInvalidValuesFallsBackWithoutChangingOriginal() {
  var dir=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid().ToString());
  System.IO.Directory.CreateDirectory(dir);var path=System.IO.Path.Combine(dir,"settings.xml");
  try {
   var original=new PlaytestSession(1,path);original.StartSolo("host");original.BeginSettingsEdit(0);original.ApplySettings(0);
   var corrupt=System.IO.File.ReadAllText(path).Replace("<RoundSeconds>180</RoundSeconds>","<RoundSeconds>-1</RoundSeconds>");
   System.IO.File.WriteAllText(path,corrupt);
   var restarted=new PlaytestSession(2,path);restarted.StartSolo("host");
   Assert.AreEqual(180,restarted.ObserveSettings().Current.RoundSeconds);
   Assert.IsNotEmpty(restarted.ObserveSettings().Error);
   Assert.AreEqual(SettingsFailure.Load,restarted.ObserveSettings().Failure);
   Assert.IsFalse(restarted.RetrySettingsSave(0));
   Assert.AreEqual(corrupt,System.IO.File.ReadAllText(path));
  } finally {System.IO.Directory.Delete(dir,true);}
 }
 [Test] public void CollisionSwitchesAreIndependentAcrossRoundAndRestart() {
  var dir=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid().ToString());
  System.IO.Directory.CreateDirectory(dir);var path=System.IO.Path.Combine(dir,"settings.xml");
  try {
   foreach(var key in new[]{"FriendlyCollision","EnemyCollision","FriendlyPush"}) {
    var session=new PlaytestSession(1,path);session.StartSolo("host");session.BeginSettingsEdit(0);session.RestoreSettingsDefaults(0);
    var values=session.ObserveSettings().Edit;
    Assert.IsTrue(values.FriendlyCollision&&values.EnemyCollision&&values.FriendlyPush);
    typeof(PlaytestValues).GetField(key).SetValue(values,false);
    session.UpdateSettingsEdit(0,values);session.ApplySettings(0);session.Advance(205);
    var current=session.ObserveSettings().Current;var saved=new PlaytestSession(2,path).ObserveSettings().Current;
    foreach(var other in new[]{"FriendlyCollision","EnemyCollision","FriendlyPush"}) {
     Assert.AreEqual(other!=key,typeof(PlaytestValues).GetField(other).GetValue(current));
     Assert.AreEqual(other!=key,typeof(PlaytestValues).GetField(other).GetValue(saved));
    }
   }
  } finally {System.IO.Directory.Delete(dir,true);}
 }
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
 [Test] public void RestartReadsSavedValuesAndFailedSaveKeepsReservationAndOldFile() {
  var dir=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid().ToString());
  System.IO.Directory.CreateDirectory(dir);var path=System.IO.Path.Combine(dir,"settings.xml");
  try {
   var s=new PlaytestSession(1,path);s.StartSolo("host");s.BeginSettingsEdit(0);
   var v=s.ObserveSettings().Edit;v.RoundSeconds=60;s.UpdateSettingsEdit(0,v);s.ApplySettings(0);
   var old=System.IO.File.ReadAllText(path);
   System.IO.Directory.CreateDirectory(path+".tmp");
   s.BeginSettingsEdit(0);v.RoundSeconds=90;s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettings(0));
   Assert.AreEqual(90,s.ObserveSettings().Pending.RoundSeconds);Assert.AreEqual(60,s.ObserveSettings().Saved.RoundSeconds);
   Assert.IsNotEmpty(s.ObserveSettings().Error);Assert.AreEqual(SettingsFailure.Save,s.ObserveSettings().Failure);Assert.AreEqual(old,System.IO.File.ReadAllText(path));
   System.IO.Directory.Delete(path+".tmp");Assert.IsTrue(s.RetrySettingsSave(0));
   var restarted=new PlaytestSession(2,path);restarted.StartSolo("host");restarted.Advance(20);
   Assert.AreEqual(90,restarted.Observe().SecondsRemaining);
   System.IO.File.WriteAllText(path,"broken");var broken=new PlaytestSession(3,path);
   Assert.IsNotEmpty(broken.ObserveSettings().Error);Assert.AreEqual(180,broken.ObserveSettings().Current.RoundSeconds);
   Assert.AreEqual("broken",System.IO.File.ReadAllText(path));
  } finally {System.IO.Directory.Delete(dir,true);}
 }
 [Test] public void ResultDurationIsReservedThenUsedByTheNextRound() {
  var s=new PlaytestSession(1);s.StartSolo("host");s.BeginSettingsEdit(0);
  var v=s.ObserveSettings().Edit;v.ResultSeconds=9;s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettings(0));
  s.Advance(200);Assert.AreEqual(5,s.Observe().SecondsRemaining);
  s.Advance(5+200);Assert.AreEqual(RoundPhase.Results,s.Observe().Phase);Assert.AreEqual(9,s.Observe().SecondsRemaining);
  s.Advance(8);Assert.AreEqual(2,s.Observe().Round);s.Advance(1);Assert.AreEqual(3,s.Observe().Round);
 }
 [Test] public void SavingAfterCorruptLoadPreservesTheOriginalInASeparateCopy() {
  var dir=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid().ToString("N"));
  System.IO.Directory.CreateDirectory(dir);var path=System.IO.Path.Combine(dir,"settings.xml");
  const string damaged="<PlaytestValues>손상된 XML";
  try {
   System.IO.File.WriteAllText(path,damaged);var s=new PlaytestSession(1,path);s.StartSolo("host");
   Assert.AreEqual(SettingsFailure.Load,s.ObserveSettings().Failure);
   s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;v.RoundSeconds=90;s.UpdateSettingsEdit(0,v);
   Assert.IsTrue(s.ApplySettings(0));Assert.AreEqual(SettingsFailure.None,s.ObserveSettings().Failure);
   var preserved=System.IO.Directory.GetFiles(dir,"settings.xml.corrupt-*");Assert.AreEqual(1,preserved.Length);
   Assert.AreEqual(damaged,System.IO.File.ReadAllText(preserved[0]));
   Assert.AreEqual(90,new PlaytestSession(2,path).ObserveSettings().Current.RoundSeconds);
  } finally {System.IO.Directory.Delete(dir,true);}
 }
}}
