using UnityEngine;
using UnityEngine.Rendering;
namespace AvH {
 public static class TownLighting {
  static Material sky;
  public static void Apply(Camera camera,Light sun) {
   if(sky==null)sky=new Material(Resources.Load<Shader>("TownSky")){hideFlags=HideFlags.HideAndDontSave};RenderSettings.skybox=sky;
   camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=new Color(.79f,.84f,.79f);camera.farClipPlane=260;camera.allowHDR=true;
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.59f,.69f,.8f);RenderSettings.ambientEquatorColor=new Color(.6f,.59f,.49f);RenderSettings.ambientGroundColor=new Color(.27f,.23f,.18f);
   RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=camera.backgroundColor;RenderSettings.fogStartDistance=70;RenderSettings.fogEndDistance=185;
   if(sun!=null){sun.type=LightType.Directional;sun.intensity=1.25f;sun.color=new Color(1,.91f,.73f);sun.shadows=LightShadows.Soft;sun.shadowBias=.03f;sun.shadowNormalBias=.25f;sun.transform.rotation=Quaternion.Euler(48,-35,0);}
   QualitySettings.shadowDistance=110;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.antiAliasing=4;
  }
 }
}
