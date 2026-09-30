using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class GameplayCameraTests {
  [UnityTest] public IEnumerator ActualGameplayCameraOrbitsAndAvoidsNarrowObstacles() {
   var root=new GameObject("actual gameplay camera test");var presentation=root.AddComponent<GamePresentation>();
   var game=root.GetComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;
   string settings=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   try {
    game.StartSolo("camera",123,settings);yield return null;yield return null;
    var camera=Camera.main ?? Object.FindAnyObjectByType<Camera>();var pivot=game.PlayerTransform(0).position+Vector3.up*1.35f;
    var offset=Quaternion.Euler(20,0,0)*new Vector3(.6f,.3f,-5.5f);

    Debug.Log($"GAMEPLAY_CAMERA distance={Vector3.Distance(camera.transform.position,pivot)} player={game.PlayerTransform(0).position} camera={camera.transform.position}");
    Assert.Greater(Vector3.Distance(camera.transform.position,pivot),3f,"Default spawn must show the character from a third person distance.");
    var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Camera-only narrow wall";blocker.transform.SetParent(root.transform);blocker.transform.position=pivot+offset.normalized*.95f;blocker.transform.localScale=new Vector3(.65f,3,.25f);Physics.SyncTransforms();
    yield return null;yield return null;
    Debug.Log($"GAMEPLAY_CAMERA_WALL distance={Vector3.Distance(camera.transform.position,pivot)}");
    Assert.Greater(Vector3.Distance(camera.transform.position,pivot),2f,"A narrow obstruction must allow the camera to move to a clear shoulder instead of becoming first person.");
    Assert.IsFalse(Physics.Linecast(pivot,camera.transform.position,out var hit,~0,QueryTriggerInteraction.Ignore)&&hit.collider==blocker.GetComponent<Collider>(),"Camera must not cross the wall.");
    blocker.GetComponent<Collider>().isTrigger=true;Physics.SyncTransforms();yield return null;yield return null;
    Assert.AreEqual(offset.magnitude,Vector3.Distance(camera.transform.position,pivot),.05f,"Triggers must not shorten the camera boom.");
    var previous=camera.transform.position;presentation.SetLookAngles(137,20);yield return null;yield return null;
    Assert.Greater(Vector3.Distance(previous,camera.transform.position),3f,"Public look input must orbit freely, beyond cardinal directions.");
    blocker.SetActive(false);presentation.SetLookAngles(0,20);yield return null;yield return null;
    Assert.AreEqual(offset.magnitude,Vector3.Distance(camera.transform.position,pivot),.05f,"Camera must recover after leaving the obstacle.");
    blocker.SetActive(true);blocker.GetComponent<Collider>().isTrigger=false;blocker.transform.localScale=new Vector3(20,12,.25f);Physics.SyncTransforms();yield return null;yield return null;
    Assert.Less(Vector3.Distance(camera.transform.position,pivot),1f,"A wall covering every alternate shoulder must still block the camera.");
    Assert.IsFalse(blocker.GetComponent<Collider>().bounds.Contains(camera.transform.position),"Fallback camera must remain in front of an unavoidable wall.");
    blocker.SetActive(false);Physics.SyncTransforms();yield return null;yield return null;
    var output=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../Builds/VisualAudit/GameplayCamera"));System.IO.Directory.CreateDirectory(output);
    var target=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=RenderTexture.active;camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"third-person.png"),pixels.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;Object.Destroy(target);Object.Destroy(pixels);
   }finally{Object.Destroy(root);if(System.IO.File.Exists(settings))System.IO.File.Delete(settings);}
   yield return null;
  }
 }
}
