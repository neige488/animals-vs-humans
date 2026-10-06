using UnityEngine;
namespace AvH {
 /// <summary>
 /// Applies a local graphics quality step. This slice scales shadows, effect particle counts and the
 /// camera shake amplitude; the environment slice (S5) adds post-processing and foliage sway to the same steps.
 /// </summary>
 public static class DisplayQuality {
  public static void Apply(GraphicsQuality quality) {
   switch(quality) {
    case GraphicsQuality.Low:
     QualitySettings.shadows=ShadowQuality.Disable;QualitySettings.shadowDistance=40;QualitySettings.antiAliasing=0;PrimitiveEffects.Density=.4f;break;
    case GraphicsQuality.Medium:
     QualitySettings.shadows=ShadowQuality.HardOnly;QualitySettings.shadowResolution=ShadowResolution.Medium;QualitySettings.shadowDistance=70;QualitySettings.antiAliasing=2;PrimitiveEffects.Density=.7f;break;
    default:
     QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowDistance=110;QualitySettings.antiAliasing=4;PrimitiveEffects.Density=1;break;
   }
  }
  /// <summary>Camera shake amplitude share for a quality step (screen shake on/off is a separate choice).</summary>
  public static float ShakeScale(GraphicsQuality quality)=>quality==GraphicsQuality.Low?.7f:quality==GraphicsQuality.Medium?.85f:1;
 }
}
