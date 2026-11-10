using UnityEngine;
using UnityEngine.Rendering;
namespace AvH {
 /// <summary>Sun, ambient light, sky and haze for a map's <see cref="MapLook"/>.</summary>
 public static class TownLighting {
  static Material sky;
  /// <summary>The look last applied (the colour grade and the stylised shader follow it).</summary>
  public static MapLook Current {get;private set;}=MapLook.For(GameMaps.Town);
  public static int CurrentMap {get;private set;}
  public static void Apply(Camera camera,Light sun,int map=0) {
   var look=MapLook.For(map);Current=look;CurrentMap=map;
   if(sky==null)sky=new Material(Resources.Load<Shader>("TownSky")){hideFlags=HideFlags.HideAndDontSave};RenderSettings.skybox=sky;
   sky.SetColor("_Horizon",look.Horizon);sky.SetColor("_Zenith",look.Zenith);sky.SetColor("_Cloud",look.Cloud);sky.SetColor("_CloudShade",look.CloudShade);
   sky.SetColor("_SunColor",look.Sun);sky.SetVector("_SunDir",look.ToSun);sky.SetFloat("_CloudAmount",look.CloudAmount);
   camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=look.Horizon;camera.farClipPlane=260;camera.allowHDR=true;
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=look.AmbientSky;RenderSettings.ambientEquatorColor=look.AmbientEquator;RenderSettings.ambientGroundColor=look.AmbientGround;
   // Distance fades into the horizon's own colour, so far buildings sit in the air instead of ending at a grey wall.
   RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=look.Horizon;RenderSettings.fogStartDistance=look.FogStart;RenderSettings.fogEndDistance=look.FogEnd;
   if(sun!=null){sun.type=LightType.Directional;sun.intensity=look.SunIntensity;sun.color=look.Sun;sun.shadows=LightShadows.Soft;sun.shadowStrength=look.ShadowStrength;sun.shadowBias=.03f;sun.shadowNormalBias=.25f;sun.transform.rotation=Quaternion.Euler(look.SunHeight,look.SunHeading,0);}
   QualitySettings.shadowDistance=110;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.antiAliasing=4;
   Shader.SetGlobalColor("_AvHRim",look.Rim);Shader.SetGlobalVector("_AvHSunDir",look.ToSun);
   TownAtmosphere.SetLook(look);
  }
 }
}
