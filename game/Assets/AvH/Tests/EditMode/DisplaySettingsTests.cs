using System.IO;
using NUnit.Framework;
namespace AvH.Tests {
 // Per-PC display preferences: separate from the host's PlaytestSettings, saved on every change.
 public class DisplaySettingsTests {
  string dir;
  [SetUp] public void Folder(){dir=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);}
  [TearDown] public void Clean(){if(Directory.Exists(dir))Directory.Delete(dir,true);}
  string File(string name="display-settings.xml")=>Path.Combine(dir,name);

  [Test] public void FirstRunIsHighQualityWithScreenShake() {
   var display=new LocalDisplaySettings(File());
   Assert.AreEqual(GraphicsQuality.High,display.Quality);Assert.IsTrue(display.ScreenShake);Assert.IsNull(display.Notice);
   Assert.IsFalse(System.IO.File.Exists(File()),"Reading defaults writes nothing");
  }

  [Test] public void ChoicesApplyImmediatelyAndSurviveARestart() {
   var display=new LocalDisplaySettings(File());int version=display.Version;
   Assert.IsTrue(display.SetQuality(GraphicsQuality.Low));Assert.AreEqual(GraphicsQuality.Low,display.Quality);Assert.Greater(display.Version,version);
   Assert.IsTrue(display.SetScreenShake(false));Assert.IsFalse(display.ScreenShake);
   var restarted=new LocalDisplaySettings(File());
   Assert.AreEqual(GraphicsQuality.Low,restarted.Quality);Assert.IsFalse(restarted.ScreenShake);
   Assert.IsTrue(restarted.SetQuality(GraphicsQuality.Medium));Assert.AreEqual(GraphicsQuality.Medium,new LocalDisplaySettings(File()).Quality);
  }

  [Test] public void FailedSaveKeepsTheChoiceForThisRunAndSaysSo() {
   Directory.CreateDirectory(File()+".tmp");
   var display=new LocalDisplaySettings(File());
   Assert.IsFalse(display.SetScreenShake(false));
   Assert.IsFalse(display.ScreenShake,"Still applied for this run");Assert.AreEqual("이번 실행에만 적용됩니다",display.Notice);
   Assert.IsTrue(new LocalDisplaySettings(File()).ScreenShake,"Not persisted");
   Directory.Delete(File()+".tmp");Assert.IsTrue(display.SetQuality(GraphicsQuality.Low));Assert.IsNull(display.Notice,"A later successful save clears the notice");
   var restarted=new LocalDisplaySettings(File());Assert.AreEqual(GraphicsQuality.Low,restarted.Quality);Assert.IsFalse(restarted.ScreenShake,"The whole current choice is saved");
  }

  [Test] public void DamagedOrUnknownValuesFallBackToFirstRunDefaults() {
   System.IO.File.WriteAllText(File(),"<DisplayValues>깨짐");
   var broken=new LocalDisplaySettings(File());Assert.AreEqual(GraphicsQuality.High,broken.Quality);Assert.IsTrue(broken.ScreenShake);
   System.IO.File.WriteAllText(File(),"<?xml version=\"1.0\"?><DisplayValues><Quality>Ultra</Quality><ScreenShake>false</ScreenShake></DisplayValues>");
   Assert.AreEqual(GraphicsQuality.High,new LocalDisplaySettings(File()).Quality);
   var memory=new LocalDisplaySettings(null);Assert.IsTrue(memory.SetQuality(GraphicsQuality.Low),"A null path is an in-memory profile");Assert.AreEqual(GraphicsQuality.Low,memory.Quality);
  }

  [Test] public void DisplayPreferencesNeverTouchTheHostSettingsFile() {
   var host=File("playtest-settings.xml");var s=new PlaytestSession(1,host);s.StartSolo("host");
   var display=new LocalDisplaySettings(File());display.SetQuality(GraphicsQuality.Low);
   Assert.IsFalse(System.IO.File.Exists(host),"Host tuning is only written by the host's own save");
   Assert.AreEqual(.15f,new PlaytestSession(2,host).ObserveSettings().Current.AttackWindupSeconds);
  }
 }
}
