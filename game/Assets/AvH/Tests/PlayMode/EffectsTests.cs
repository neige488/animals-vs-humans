using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class EffectsTests {
  [UnityTest] public IEnumerator RoundResetDoesNotReplayAnimalBirthEffects() {
   var root=new GameObject("birth effect boundary");var game=root.AddComponent<UnityPlaytestSession>();
   game.AutomaticStep=false;game.BotAutomationEnabled=false;game.StartSolo("birth",123);game.Effects.AutomaticUpdate=false;
   try {
    while(game.Observe().Phase!=RoundPhase.Results){game.Step(.02f);game.Effects.Advance(2);}
    int round=game.Observe().Round;
    while(game.Observe().Round==round)game.Step(.02f);
    Assert.AreEqual(0,game.Effects.ActiveCount,"Animal-to-human round reset is not an animal birth");
   } finally {Object.Destroy(root);}
   yield return null;
  }
  [UnityTest] public IEnumerator RemoteImpactsDoNotReplayOnInitialStateOrDuplicatePackets() {
   var source=new PlaytestSession(123);source.StartSolo("source");var root=new GameObject("remote effects");var remote=root.AddComponent<UnityPlaytestSession>();remote.StartRemote(source.Observe());
   remote.Effects.AutomaticUpdate=false;
   remote.ApplyRemoteVisuals(new NetworkVisualState{Bursts=new[]{new NetworkBurst{Id=1,Remaining=.1f,Position=new WorldPosition(0,1,0)}}});Assert.AreEqual(0,remote.Effects.ActiveCount);
   remote.ApplyRemoteVisuals(new NetworkVisualState{Bursts=new[]{new NetworkBurst{Id=2,Remaining=.1f,Position=new WorldPosition(0,1,0)}}});int count=remote.Effects.ActiveCount;Assert.Greater(count,0);
   remote.ApplyRemoteVisuals(new NetworkVisualState());
   remote.ApplyRemoteVisuals(new NetworkVisualState{Bursts=new[]{new NetworkBurst{Id=2,Remaining=.05f,Position=new WorldPosition(0,1,0)}}});Assert.AreEqual(count,remote.Effects.ActiveCount);
   Object.Destroy(root);yield return null;
  }
  [UnityTest] public IEnumerator EffectsStayBoundedAndNeverBlockGameplay() {
   var root=new GameObject("effect load");var effects=root.AddComponent<PrimitiveEffects>();effects.Initialize();
   for(int i=0;i<100;i++)effects.Emit(Vector3.zero,Color.cyan,12,2);
   Assert.LessOrEqual(effects.ActiveCount,256);
   foreach(var c in root.GetComponentsInChildren<Collider>())Assert.IsFalse(c.enabled,"Cosmetic effects may not collide with a player or bubble");
   effects.Advance(2);Assert.AreEqual(0,effects.ActiveCount);
   var bubble=effects.Bubble(root.transform,.35f);
   Assert.IsTrue(bubble.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"The shipped thin-film shader must compile for this target");
   Assert.AreEqual("AvH/SoapFilm",bubble.GetComponent<Renderer>().sharedMaterial.shader.name);
   Object.Destroy(root);yield return null;
  }
 }
}
