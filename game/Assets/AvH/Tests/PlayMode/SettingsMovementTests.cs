using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class SettingsMovementTests {
  [UnityTest] public IEnumerator ReservedMovementReachesEveryBodyOnlyAtNextRound() {
   var directory=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid().ToString("N"));
   var root=new GameObject("settings session");
   try {
    var adapter=root.AddComponent<UnityPlaytestSession>();adapter.AutomaticStep=false; adapter.BotAutomationEnabled=false;
    adapter.StartSolo("host",123,Path.Combine(directory,"settings.xml"));yield return null;
    var session=adapter.Session;session.BeginSettingsEdit(0);
    var values=session.ObserveSettings().Edit;values.HumanSpeed=2;values.AnimalSpeed=2;
    session.UpdateSettingsEdit(0,values);Assert.IsTrue(session.ApplySettings(0));
    for(int i=0;i<20;i++)adapter.Step(.02f);
    float before=adapter.Observe().Players[0].Position.X;
    for(int i=0;i<20;i++){adapter.SubmitInput(0,new PlayerInput{Right=-1});adapter.Step(.02f);}
    Assert.That(before-adapter.Observe().Players[0].Position.X,Is.EqualTo(2).Within(.15),"Current round must keep original speed");
    // Advance the public clock up to the next-round boundary; the adapter consumes that boundary itself.
    session.Advance(session.Observe().SecondsRemaining+180+4.99);
    adapter.Step(.02f);Assert.AreEqual(2,adapter.Observe().Round);
    Assert.AreEqual(2,session.ObserveSettings().Version);
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
