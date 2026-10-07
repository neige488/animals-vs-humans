using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
namespace AvH {
 /// <summary>
 /// Points a build at the post-processing package's shaders and textures (Resources/TownPostProcess), so a camera
 /// given a post stack at runtime renders in a standalone player too.
 /// </summary>
 public sealed class TownPostProcessResources : ScriptableObject {
  public PostProcessResources Resources;
 }
}
