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
 [Test] public void FloatMinimumInputsAreAcceptedWithoutRelaxingAdjacentBounds() {
  var ranges=new[]{
   ("PreparationSeconds",1f,120f),("RoundSeconds",1f,3600f),("ResultSeconds",1f,30f),
   ("InitialAttackGrace",0f,30f),("TransformAttackGrace",0f,30f),
   ("HumanSpeed",.01f,20f),("AnimalSpeed",.01f,20f),("HumanJump",.01f,5f),("AnimalJump",.01f,5f),
   ("ReloadSeconds",.01f,30f),("BubbleRadius",.01f,2f),("BubbleSpeed",.01f,100f),
   ("BubbleRange",.01f,100f),("BubbleLifetime",.01f,10f),("FireInterval",.01f,30f),("PushForce",.01f,30f)
  };
  foreach(var range in ranges) {
   var session=new PlaytestSession(1);session.StartSolo("host");
   var field=typeof(PlaytestValues).GetField(range.Item1);
   // These are the exact float values produced by the settings UI parser.
   foreach(float accepted in new[]{range.Item2,range.Item3}) {
    session.BeginSettingsEdit(0);var values=session.ObserveSettings().Edit;field.SetValue(values,accepted);
    session.UpdateSettingsEdit(0,values);
    Assert.IsTrue(session.ApplySettings(0),range.Item1+" boundary "+accepted.ToString("R"));
    Assert.AreEqual(accepted,field.GetValue(session.ObserveSettings().Pending));
   }
   float below=range.Item2==0?-float.Epsilon:AdjacentPositiveFloat(range.Item2,-1);
   float above=AdjacentPositiveFloat(range.Item3,1);
   foreach(float rejected in new[]{below,above,float.NaN,float.PositiveInfinity,float.NegativeInfinity}) {
    session.BeginSettingsEdit(0);var values=session.ObserveSettings().Edit;field.SetValue(values,rejected);
    session.UpdateSettingsEdit(0,values);
    Assert.IsFalse(session.ApplySettings(0),range.Item1+" outside boundary "+rejected.ToString("R"));
    Assert.IsTrue(session.ObserveSettings().Errors.ContainsKey(range.Item1));
    Assert.AreEqual(range.Item3,field.GetValue(session.ObserveSettings().Pending),"Rejected edits retain the valid reservation");
   }
  }
 }
 [Test] public void LiveSettingsRetainElapsedTimeAndClearOldReservation() {
  var s=new PlaytestSession(1);s.StartSolo("host");s.Advance(8);
  s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;v.HumanSpeed=2;s.UpdateSettingsEdit(0,v);s.ApplySettings(0);
  s.BeginSettingsEdit(0);v=s.ObserveSettings().Edit;v.PreparationSeconds=12;v.InitialAnimals=3;
  s.UpdateSettingsEdit(0,v);Assert.IsFalse(s.ApplySettingsNow(1));Assert.IsTrue(s.ApplySettingsNow(0));
  Assert.IsNull(s.ObserveSettings().Pending);Assert.AreEqual(2,s.ObserveSettings().Version);
  Assert.AreEqual(4,s.Observe().SecondsRemaining);Assert.AreEqual(1,s.Observe().Round);
  s.Advance(4);Assert.AreEqual(RoundPhase.Chase,s.Observe().Phase);
  Assert.AreEqual(3,System.Linq.Enumerable.Count(s.Observe().Players,p=>p.Faction==Faction.Animal));
  s.Advance(10);s.BeginSettingsEdit(0);v=s.ObserveSettings().Edit;v.RoundSeconds=5;
  s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettingsNow(0));Assert.AreEqual(0,s.Observe().SecondsRemaining);
  s.Advance(0);Assert.AreEqual(RoundPhase.Results,s.Observe().Phase);Assert.AreEqual(1,s.Observe().Round);
  s.Advance(2);s.BeginSettingsEdit(0);v=s.ObserveSettings().Edit;v.ResultSeconds=9;
  s.UpdateSettingsEdit(0,v);s.ApplySettingsNow(0);Assert.AreEqual(7,s.Observe().SecondsRemaining);
 }
 [Test] public void LiveSettingsDoNotRefillAmmoAndCanFinishAnActiveReload() {
  var s=new PlaytestSession(1);s.StartSolo("host");Assert.IsTrue(s.TryFire(0,1));
  s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;v.Magazine=4;v.FireInterval=.1f;
  s.UpdateSettingsEdit(0,v);s.ApplySettingsNow(0);Assert.AreEqual(4,s.Observe().Players[0].Ammo);
  s.Advance(.11);Assert.IsTrue(s.TryFire(0,1));Assert.IsTrue(s.TryReload(0,1));s.Advance(.5);
  s.BeginSettingsEdit(0);v=s.ObserveSettings().Edit;v.Magazine=20;v.ReloadSeconds=.25f;
  s.UpdateSettingsEdit(0,v);s.ApplySettingsNow(0);Assert.AreEqual(0,s.Observe().Players[0].ReloadRemaining);Assert.AreEqual(20,s.Observe().Players[0].Ammo);
  s.BeginSettingsEdit(0);v=s.ObserveSettings().Edit;v.HumanSpeed=float.NaN;
  s.UpdateSettingsEdit(0,v);Assert.IsFalse(s.ApplySettingsNow(0));Assert.AreEqual(20,s.ObserveSettings().Current.Magazine);
 }
 [Test] public void LiveDragDoesNotWriteUntilExplicitSaveAndRestartUsesSavedSettings() {
  var dir=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid().ToString());
  var path=System.IO.Path.Combine(dir,"settings.xml");
  try {
   var s=new PlaytestSession(1,path);s.StartSolo("host");
   s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;v.HumanSpeed=3;s.UpdateSettingsEdit(0,v);s.ApplySettingsNow(0,false);
   Assert.AreEqual(3,s.ObserveSettings().Current.HumanSpeed);Assert.IsFalse(System.IO.File.Exists(path));
   Assert.IsFalse(s.SaveCurrentSettings(1));Assert.IsTrue(s.SaveCurrentSettings(0));
   s.BeginSettingsEdit(0);v=s.ObserveSettings().Edit;v.HumanSpeed=7;s.UpdateSettingsEdit(0,v);s.ApplySettingsNow(0,false);
   Assert.AreEqual(3,new PlaytestSession(2,path).ObserveSettings().Current.HumanSpeed);
   Assert.IsTrue(s.SaveCurrentSettings(0));Assert.AreEqual(7,new PlaytestSession(3,path).ObserveSettings().Current.HumanSpeed);
  }finally{if(System.IO.Directory.Exists(dir))System.IO.Directory.Delete(dir,true);}
 }
 [Test] public void LiveTimerKeepsElapsedTimeAcrossZeroThenExtensionBeforeNextTick() {
  var s=new PlaytestSession(1);s.StartSolo("host");s.Advance(8);
  s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;v.PreparationSeconds=5;s.UpdateSettingsEdit(0,v);s.ApplySettingsNow(0,false);
  Assert.AreEqual(0,s.Observe().SecondsRemaining);
  s.BeginSettingsEdit(0);v=s.ObserveSettings().Edit;v.PreparationSeconds=20;s.UpdateSettingsEdit(0,v);s.ApplySettingsNow(0,false);
  Assert.AreEqual(12,s.Observe().SecondsRemaining);
 }
 [Test] public void SpeciesTuningCopiesValidatesAndLoadsOlderXmlWithNewDefaults() {
  var file=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");
  try {
   var s=new PlaytestSession(1,file);s.StartSolo("host");s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;
   Assert.AreEqual(.45f,v.BearKnockbackMultiplier);v.FoxSpeedMultiplier=2;s.UpdateSettingsEdit(0,v);Assert.AreEqual(1.1f,s.ObserveSettings().Current.FoxSpeedMultiplier);
   Assert.IsFalse(s.ApplySettingsNow(1));Assert.IsTrue(s.ApplySettingsNow(0));Assert.IsTrue(s.SaveCurrentSettings(0));
   s.BeginSettingsEdit(0);v=s.ObserveSettings().Edit;v.BearJumpMultiplier=2;s.UpdateSettingsEdit(0,v);
   Assert.IsFalse(s.RestoreAnimalDefaults(1,0));Assert.IsFalse(s.RestoreAnimalDefaults(0,-1));Assert.IsTrue(s.RestoreAnimalDefaults(0,0));
   Assert.AreEqual(1.1f,s.ObserveSettings().Edit.FoxSpeedMultiplier);Assert.AreEqual(2,s.ObserveSettings().Edit.BearJumpMultiplier);s.CancelSettingsEdit(0);
   Assert.AreEqual(2,new PlaytestSession(2,file).ObserveSettings().Current.FoxSpeedMultiplier);
   foreach(int i in new[]{0,1,2,3,4,5})foreach(string key in AnimalBalance.Keys(i)) {
    var field=typeof(PlaytestValues).GetField(key);
    foreach(float bad in new[]{float.NaN,float.PositiveInfinity,-.01f,3.01f}) {
     s.BeginSettingsEdit(0);v=s.ObserveSettings().Edit;field.SetValue(v,bad);s.UpdateSettingsEdit(0,v);Assert.IsFalse(s.ApplySettingsNow(0),key);
    }
   }
   var xml=new System.Xml.XmlDocument();xml.Load(file);
   foreach(var field in typeof(PlaytestValues).GetFields())if(field.Name.EndsWith("Multiplier")){var node=xml.DocumentElement.SelectSingleNode(field.Name);xml.DocumentElement.RemoveChild(node);}
   xml.Save(file);var old=new PlaytestSession(3,file).ObserveSettings();Assert.IsNull(old.Error);Assert.AreEqual(1.1f,old.Current.FoxSpeedMultiplier);Assert.AreEqual(1.55f,old.Current.RabbitJumpMultiplier);
   var unknown=AnimalBalance.For(old.Current,new PlayerState{Faction=Faction.Animal,CharacterId="animal-default"});Assert.AreEqual(1,unknown.Speed);
   var human=AnimalBalance.For(old.Current,new PlayerState{Faction=Faction.Human,CharacterId=AnimalBalance.Id(0)});Assert.AreEqual(1,human.Knockback);
  }finally{if(System.IO.File.Exists(file))System.IO.File.Delete(file);}
 }
 static float AdjacentPositiveFloat(float value,int step) {
  int bits=System.BitConverter.ToInt32(System.BitConverter.GetBytes(value),0);
  return System.BitConverter.ToSingle(System.BitConverter.GetBytes(bits+step),0);
 }
}}
