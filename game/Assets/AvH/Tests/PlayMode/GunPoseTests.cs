using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class GunPoseTests {
  [UnityTest] public IEnumerator OwnedHumanHoldsEmitterWithBothHandsWhileAiming() {
   Assert.IsNotNull(Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog"));
   var root=new GameObject("gun pose test");var game=root.AddComponent<UnityPlaytestSession>();
   game.AutomaticStep=false;game.BotAutomationEnabled=false;game.StartSolo("pose",123);
   try {
    var body=game.PlayerTransform(0);var animator=body.GetComponentsInChildren<Animator>().First(a=>a.isHuman);
    var gun=body.Find("Bubble emitter");
    foreach(float yaw in new[]{0f,65f,145f,270f}) {
     for(int f=0;f<20;f++){game.SubmitInput(0,new PlayerInput{Yaw=yaw,Pitch=-15,Forward=.4f});game.Step(.02f);yield return null;}
     Assert.Less(Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightHand).position,gun.TransformPoint(new Vector3(0,-.1f,0))),.08f,"Right hand must hold the rear grip");
     Assert.Less(Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftHand).position,gun.TransformPoint(new Vector3(-.07f,-.035f,.22f))),.08f,"Left hand must support the barrel");
     Assert.Greater(gun.position.y,body.position.y+1.05f,"Emitter must be carried above the waist");
     if(System.Environment.GetCommandLineArgs().Contains("-avhCapture"))Capture(body,yaw);
    }
   } finally {Object.Destroy(root);}
   yield return null;
  }
  static void Capture(Transform body,float yaw) {
   var cameraObject=new GameObject("Pose camera");var camera=cameraObject.AddComponent<Camera>();
   var sunObject=new GameObject("Pose sun");var sun=sunObject.AddComponent<Light>();TownLighting.Apply(camera,sun);
   var rt=new RenderTexture(1000,900,24);var pixels=new Texture2D(1000,900,TextureFormat.RGB24,false);var old=RenderTexture.active;
   try {var aim=Quaternion.Euler(0,yaw,0);camera.transform.position=body.position+aim*new Vector3(2,1.8f,3.1f);camera.transform.LookAt(body.position+Vector3.up*1.05f);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1000,900),0,0);pixels.Apply();
    var dir=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../Builds/GunPose"));System.IO.Directory.CreateDirectory(dir);System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir,"hold-"+yaw+".png"),pixels.EncodeToPNG());
   } finally {RenderTexture.active=old;Object.Destroy(pixels);Object.Destroy(rt);Object.Destroy(cameraObject);Object.Destroy(sunObject);}
  }
 }
}
