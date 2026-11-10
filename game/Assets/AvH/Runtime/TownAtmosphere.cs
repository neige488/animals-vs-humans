using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
namespace AvH {
 /// <summary>What a camera's town atmosphere currently renders.</summary>
 public struct AtmosphereView {
  public GraphicsQuality Quality;
  public bool PostProcessing,ToneMapping,ColorGrading,Bloom,AmbientOcclusion;
  public int PixelLights;
 }
 /// <summary>What a village's living dressing currently does: swaying trees and flags, lit lanterns.</summary>
 public struct TownDressingView {
  public int SwayingObjects,LanternLights;
  public float SwayAmplitude;
  public bool LanternShadows;
 }
 /// <summary>
 /// The town's look per local graphics quality, on the Built-in pipeline: post-processing (tone mapping, colour
 /// grading, gentle bloom, ambient occlusion) through the Post Processing Stack v2, swaying trees and flags, and
 /// lantern point lights (<see cref="TownDressing"/>). High shows all of it; Medium keeps the grade and bloom, softer
 /// sway and half the lanterns; Low renders plain, still and unlit. Presentation only.
 /// </summary>
 public static class TownAtmosphere {
  public static GraphicsQuality Quality {get;private set;}=GraphicsQuality.High;
  /// <summary>Changes on every apply so village dressing can follow.</summary>
  public static int Version {get;private set;}
  // The global volume lives on the Default layer; post layers only listen to it.
  const int VolumeLayer=0;
  static PostProcessResources resources;static bool resourcesLoaded;
  static PostProcessVolume volume;static ColorGrading grading;static Bloom bloom;static AmbientOcclusion occlusion;static Vignette vignette;
  static MapLook look=MapLook.For(GameMaps.Town);
  /// <summary>The colour grade of a map's time of day (<see cref="TownLighting.Apply"/> sets it).</summary>
  public static void SetLook(MapLook next){look=next??MapLook.For(GameMaps.Town);if(volume!=null)Grade();}
  static void Grade() {
   grading.postExposure.Override(look.Exposure);grading.temperature.Override(look.Temperature);grading.tint.Override(look.Tint);grading.saturation.Override(look.Saturation);grading.contrast.Override(look.Contrast);
   // Split toning: the shade leans to the sky's colour and the lit side to the sun's (trackball offset from neutral grey).
   var shade=look.ShadowTint;var bright=look.HighlightTint;
   grading.lift.Override(new Vector4(shade.r-.5f,shade.g-.5f,shade.b-.5f,0)*.7f+new Vector4(1,1,1,0));
   grading.gain.Override(new Vector4(bright.r-.5f,bright.g-.5f,bright.b-.5f,0)*.7f+new Vector4(1,1,1,0));
   bloom.intensity.Override(look.Bloom);bloom.color.Override(Color.Lerp(Color.white,look.Sun,.6f));
  }

  public static void Apply(Camera camera,GraphicsQuality quality) {
   Quality=quality;Version++;
   QualitySettings.pixelLightCount=quality==GraphicsQuality.High?4:quality==GraphicsQuality.Medium?2:0;
   if(!EnsureVolume())return;
   grading.enabled.Override(quality!=GraphicsQuality.Low);bloom.enabled.Override(quality!=GraphicsQuality.Low);vignette.enabled.Override(quality!=GraphicsQuality.Low);occlusion.enabled.Override(quality==GraphicsQuality.High);
   bloom.fastMode.Override(quality!=GraphicsQuality.High);
   if(camera==null)return;
   var layer=camera.GetComponent<PostProcessLayer>();
   if(quality==GraphicsQuality.Low){if(layer!=null)layer.enabled=false;return;}
   if(layer==null) {
    layer=camera.gameObject.AddComponent<PostProcessLayer>();layer.Init(resources);
    layer.volumeLayer=1<<VolumeLayer;layer.volumeTrigger=camera.transform;
    // MSAA from the quality step stays the anti-aliasing; the stack adds none of its own.
    layer.antialiasingMode=PostProcessLayer.Antialiasing.None;layer.stopNaNPropagation=true;
   }
   layer.enabled=true;
  }
  public static AtmosphereView Observe(Camera camera) {
   var layer=camera==null?null:camera.GetComponent<PostProcessLayer>();bool on=layer!=null&&layer.enabled&&volume!=null;
   return new AtmosphereView{Quality=Quality,PostProcessing=on,
    ColorGrading=on&&grading.enabled.value,ToneMapping=on&&grading.enabled.value&&grading.tonemapper.value!=Tonemapper.None,
    Bloom=on&&bloom.enabled.value,AmbientOcclusion=on&&occlusion.enabled.value,PixelLights=QualitySettings.pixelLightCount};
  }
  static bool EnsureVolume() {
   if(volume!=null)return true;
   if(!resourcesLoaded){resourcesLoaded=true;var holder=Resources.Load<TownPostProcessResources>("TownPostProcess");resources=holder==null?null:holder.Resources;
    if(resources==null)Debug.LogWarning("후처리 리소스(Resources/TownPostProcess)가 없어 후처리 없이 표시합니다. 플레이는 계속합니다.");}
   if(resources==null)return false;
   // A warm, soft market afternoon: neutral tone mapping (ACES bleached the sky and awnings of this low-poly palette), gentle highlight bloom, contact shadows under eaves and stalls.
   grading=ScriptableObject.CreateInstance<ColorGrading>();grading.enabled.Override(true);
   grading.gradingMode.Override(SystemInfo.supportsComputeShaders&&SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)?GradingMode.HighDefinitionRange:GradingMode.LowDefinitionRange);
   grading.tonemapper.Override(Tonemapper.Neutral);grading.postExposure.Override(.15f);grading.temperature.Override(3);grading.saturation.Override(20);grading.contrast.Override(14);
   bloom=ScriptableObject.CreateInstance<Bloom>();bloom.enabled.Override(true);bloom.intensity.Override(.4f);bloom.threshold.Override(1.15f);bloom.softKnee.Override(.5f);bloom.diffusion.Override(5);bloom.color.Override(new Color(1,.93f,.82f));
   occlusion=ScriptableObject.CreateInstance<AmbientOcclusion>();occlusion.enabled.Override(true);// Multi-scale volumetric obscurance needs compute shaders (Metal, D3D11); otherwise the scalable variant.
   bool compute=SystemInfo.supportsComputeShaders;occlusion.mode.Override(compute?AmbientOcclusionMode.MultiScaleVolumetricObscurance:AmbientOcclusionMode.ScalableAmbientObscurance);
   occlusion.intensity.Override(.7f);occlusion.thicknessModifier.Override(1.2f);occlusion.radius.Override(.6f);occlusion.quality.Override(AmbientOcclusionQuality.Medium);
   // A faint darkening toward the corners keeps the eye on the middle of the screen, where the crosshair is.
   vignette=ScriptableObject.CreateInstance<Vignette>();vignette.enabled.Override(true);vignette.intensity.Override(.24f);vignette.smoothness.Override(.5f);vignette.roundness.Override(1);
   volume=PostProcessManager.instance.QuickVolume(VolumeLayer,100,grading,bloom,occlusion,vignette);Grade();
   if(Application.isPlaying)Object.DontDestroyOnLoad(volume.gameObject);
   return true;
  }
 }

}
