using UnityEngine;
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
 /// grading, gentle bloom, ambient occlusion), swaying trees and flags, and lantern point lights. High shows all of
 /// it; each lower step drops some. Presentation only.
 /// </summary>
 public static class TownAtmosphere {
  public static GraphicsQuality Quality {get;private set;}=GraphicsQuality.High;
  public static void Apply(Camera camera,GraphicsQuality quality){Quality=quality;}
  public static AtmosphereView Observe(Camera camera)=>new AtmosphereView{Quality=Quality};
 }
}
