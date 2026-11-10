using NUnit.Framework;
using UnityEngine;
namespace AvH.Tests {
 /// <summary>Camera-only scenery: the camera boom stops at it, game queries and bodies pass it.</summary>
 public class CameraOnlyLayerTests {
  [Test] public void CameraSeesItGameIgnoresIt() {
   Layers.Ensure();
   var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
   try {
    cube.layer=Layers.CameraOnly;cube.transform.position=new Vector3(500,0,0);Physics.SyncTransforms();
    var from=new Vector3(495,0,0);
    Assert.IsTrue(Physics.SphereCast(from,.18f,Vector3.right,out _,10,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),"the camera's cast stops at it");
    Assert.IsFalse(Physics.Raycast(from,Vector3.right,10,Layers.World,QueryTriggerInteraction.Ignore),"shots, sight and the bot grid look through it");
    Assert.IsTrue(Physics.GetIgnoreLayerCollision(Layers.CameraOnly,0),"bodies walk through it");
   } finally {Object.DestroyImmediate(cube);}
  }
 }
}
