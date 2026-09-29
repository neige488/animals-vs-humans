using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class MovementTests {
  [UnityTest] public IEnumerator PublicMovementInputMovesAndJumpsInActualWorld() {
   var root = new GameObject("test session");
   var session = root.AddComponent<UnityPlaytestSession>();
   session.AutomaticStep = false;
   session.StartSolo("tester");
   yield return null;
   var before = session.Observe().Players[0].Position;
   for(int i=0;i<50;i++) { session.SubmitInput(0, new PlayerInput { Right=-1 }); session.Step(0.02f); }
   var after = session.Observe().Players[0].Position;
   // Move into clear plaza space; forward runs into another starting slot.
   Assert.Less(after.X, before.X - 3);
   session.SubmitInput(0, new PlayerInput { Jump=true });
   session.Step(0.1f);
   Assert.Greater(session.Observe().Players[0].Position.Y, after.Y + 0.1f);
   Object.Destroy(root);
   yield return null;
  }
 }
}
