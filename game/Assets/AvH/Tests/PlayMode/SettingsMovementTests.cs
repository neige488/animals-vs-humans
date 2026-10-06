using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class SettingsMovementTests {
  [Test] public void EverySliderEndpointAndRoundedStepIsAcceptedByThePublicSession() {
   var panel=new SettingsPanel();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
   var keys=(string[][])typeof(SettingsPanel).GetField("keys",flags).GetValue(panel);
   var rangeMethod=typeof(SettingsPanel).GetMethod("Range",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
   var session=new PlaytestSession(1);session.StartSolo("host");
   foreach(var group in keys)foreach(string key in group) {
    var field=typeof(PlaytestValues).GetField(key);Assert.IsNotNull(field,key);if(field.FieldType==typeof(bool))continue;
    var range=(Vector3)rangeMethod.Invoke(null,new object[]{key});
    foreach(float n in new[]{range.x,range.y,Mathf.Clamp(Mathf.Round((range.x+range.z)/range.z)*range.z,range.x,range.y)}) {
     session.BeginSettingsEdit(0);var value=session.ObserveSettings().Edit;
     if(field.FieldType==typeof(int))field.SetValue(value,Mathf.RoundToInt(n));else field.SetValue(value,n);
     session.UpdateSettingsEdit(0,value);Assert.IsTrue(session.ApplySettingsNow(0,false),key+" "+n);
    }
   }
  }
  [Test] public void InertiaSlidersSitOnTheMovementTabAndCanReachZero() {
   var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var panel=new SettingsPanel();
   var keys=(string[][])typeof(SettingsPanel).GetField("keys",flags).GetValue(panel);var names=(string[][])typeof(SettingsPanel).GetField("names",flags).GetValue(panel);
   var rangeMethod=typeof(SettingsPanel).GetMethod("Range",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
   int inertia=System.Array.IndexOf(keys[1],"InertiaSeconds"),air=System.Array.IndexOf(keys[1],"AirControl");
   Assert.GreaterOrEqual(inertia,0,"Movement tab exposes inertia");Assert.GreaterOrEqual(air,0,"Movement tab exposes air control");
   Assert.AreEqual(keys[1].Length,names[1].Length);StringAssert.Contains("관성",names[1][inertia]);StringAssert.Contains("공중",names[1][air]);
   var range=(Vector3)rangeMethod.Invoke(null,new object[]{"InertiaSeconds"});Assert.AreEqual(0,range.x,"Slider reaches 0 = original instant movement");Assert.AreEqual(1,range.y);
   range=(Vector3)rangeMethod.Invoke(null,new object[]{"AirControl"});Assert.AreEqual(.05f,range.x,1e-6);Assert.AreEqual(1,range.y);
  }
  [UnityTest] public IEnumerator DebugOverlayKeepsTheWorldRunningAndReleasesTheMouse() {
   var path=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   var root=new GameObject("debug overlay capture");var presentation=root.AddComponent<GamePresentation>();
   var game=root.GetComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;
   try {
    game.StartSolo("디버그",123,path);yield return null;yield return null;
    presentation.SetDebugPanelOpen(true);yield return null;yield return null;LogAssert.NoUnexpectedReceived();
    Assert.IsTrue(presentation.DebugPanelOpen);Assert.AreEqual(CursorLockMode.None,Cursor.lockState);
    double before=game.Observe().SecondsRemaining;game.Step(.2f);Assert.Less(game.Observe().SecondsRemaining,before);
    foreach(var size in new[]{new Vector2(1280,720),new Vector2(1920,1080)}) {
     var rect=SettingsPanel.PanelRect(size.x,size.y);Assert.LessOrEqual(rect.xMax,size.x-8);Assert.GreaterOrEqual(rect.yMin,180);Assert.LessOrEqual(rect.yMax,size.y-90);
    }
    if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-avhCapture")>=0) {
     var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/VisualAudit/LiveSettings"));Directory.CreateDirectory(folder);
     ScreenCapture.CaptureScreenshot(Path.Combine(folder,"debug-overlay.png"));for(int frame=0;frame<8;frame++)yield return null;
    }
    presentation.SetDebugPanelOpen(false);Assert.IsFalse(presentation.DebugPanelOpen);Assert.IsNull(game.Session.ObserveSettings().Edit);
   }finally{Object.Destroy(root);if(File.Exists(path))File.Delete(path);}yield return null;
  }
  [UnityTest] public IEnumerator LiveMovementAndCollisionApplyWithoutRestartingRound() {
   var directory=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid().ToString("N"));
   var root=new GameObject("live settings session");
   try {
    var adapter=root.AddComponent<UnityPlaytestSession>();adapter.AutomaticStep=false;adapter.BotAutomationEnabled=false;
    adapter.StartSolo("host",123,Path.Combine(directory,"settings.xml"));yield return new WaitForFixedUpdate();
    var s=adapter.Session;s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;
    v.HumanSpeed=2;v.InertiaSeconds=0;v.FriendlyCollision=false;v.EnemyCollision=false;s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettingsNow(0));
    adapter.Step(.02f);
    Assert.IsTrue(Physics.GetIgnoreCollision(adapter.PlayerTransform(0).GetComponent<CharacterController>(),adapter.PlayerTransform(1).GetComponent<CharacterController>()));
    float before=adapter.Observe().Players[0].Position.X;
    for(int i=0;i<20;i++){adapter.SubmitInput(0,new PlayerInput{Right=-1});adapter.Step(.02f);}
    Assert.That(before-adapter.Observe().Players[0].Position.X,Is.EqualTo(.8).Within(.15));
    Assert.AreEqual(1,adapter.Observe().Round);Assert.AreEqual(2,s.ObserveSettings().Version);
    s.BeginSettingsEdit(0);v=s.ObserveSettings().Edit;v.FriendlyCollision=true;s.UpdateSettingsEdit(0,v);s.ApplySettingsNow(0);adapter.Step(.02f);
    Assert.IsFalse(Physics.GetIgnoreCollision(adapter.PlayerTransform(0).GetComponent<CharacterController>(),adapter.PlayerTransform(1).GetComponent<CharacterController>()));
   }finally{Object.Destroy(root);if(Directory.Exists(directory))Directory.Delete(directory,true);}
   yield return null;
  }
  [UnityTest] public IEnumerator ReservedMovementReachesEveryBodyOnlyAtNextRound() {
   var directory=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid().ToString("N"));
   var root=new GameObject("settings session");
   try {
    var adapter=root.AddComponent<UnityPlaytestSession>();adapter.AutomaticStep=false; adapter.BotAutomationEnabled=false;
    adapter.StartSolo("host",123,Path.Combine(directory,"settings.xml"));yield return null;
    var session=adapter.Session;session.BeginSettingsEdit(0);
    var values=session.ObserveSettings().Edit;values.InertiaSeconds=0;session.UpdateSettingsEdit(0,values);Assert.IsTrue(session.ApplySettingsNow(0));
    // Exact speed x time checks run with inertia off; the speed change itself is reserved for the next round.
    session.BeginSettingsEdit(0);values=session.ObserveSettings().Edit;values.HumanSpeed=2;values.AnimalSpeed=2;
    session.UpdateSettingsEdit(0,values);Assert.IsTrue(session.ApplySettings(0));
    for(int i=0;i<20;i++)adapter.Step(.02f);
    float before=adapter.Observe().Players[0].Position.X;
    for(int i=0;i<20;i++){adapter.SubmitInput(0,new PlayerInput{Right=-1});adapter.Step(.02f);}
    Assert.That(before-adapter.Observe().Players[0].Position.X,Is.EqualTo(2).Within(.15),"Current round must keep original speed");
    // Advance the public clock up to the next-round boundary; the adapter consumes that boundary itself.
    session.Advance(session.Observe().SecondsRemaining+180+4.99);
    adapter.Step(.02f);Assert.AreEqual(2,adapter.Observe().Round);
    Assert.AreEqual(3,session.ObserveSettings().Version,"Live inertia-off apply, then the reserved speed at the next round");
    for(int i=0;i<20;i++)adapter.Step(.02f);
    foreach(int slot in new[]{0,4,8}) {
     before=adapter.Observe().Players[slot].Position.X;
     for(int i=0;i<20;i++){adapter.SubmitInput(slot,new PlayerInput{Right=-1});adapter.Step(.02f);}
     Assert.That(before-adapter.Observe().Players[slot].Position.X,Is.EqualTo(.8).Within(.15),"Host and bot bodies consume same rules");
     adapter.SubmitInput(slot,new PlayerInput());
    }
   } finally {Object.Destroy(root);if(Directory.Exists(directory))Directory.Delete(directory,true);}
   yield return null;
  }
 }
}
