using System.Collections.Generic;
using UnityEngine;
namespace AvH {
 /// <summary>
 /// Puts the matte low-poly shader (Resources/LowPoly) on opaque Standard materials: flat two-tone light and a rim of
 /// sunlight on silhouettes. Presentation only; see-through and special materials keep their own shader.
 /// </summary>
 public static class LowPolyLook {
  public const float SceneryRim=.22f,CharacterRim=.85f;
  static Shader shader;static bool looked;
  static readonly Dictionary<(Material,bool),Material> converted=new Dictionary<(Material,bool),Material>();
  static Shader Shader_(){if(!looked){looked=true;shader=Resources.Load<Shader>("LowPoly");if(shader!=null&&!shader.isSupported)shader=null;}return shader;}
  /// <summary>A plain coloured material in the map's look.</summary>
  public static Material Plain(Color color) {
   var s=Shader_();if(s==null)return null;
   var m=new Material(s);m.color=color;m.SetFloat("_Rim",SceneryRim);return m;
  }
  /// <summary>The low-poly twin of an opaque Standard material (shared by everything that uses the source), or the source itself.</summary>
  public static Material Of(Material source,bool character) {
   var s=Shader_();if(s==null||source==null||source.shader==s)return source;
   if(!source.shader.name.StartsWith("Standard")||source.renderQueue>=2450)return source;
   var key=(source,character);
   if(converted.TryGetValue(key,out var twin)&&twin!=null)return twin;
   twin=new Material(s){name=source.name+" (low poly)",hideFlags=HideFlags.HideAndDontSave};
   twin.color=source.color;twin.mainTexture=source.mainTexture;twin.mainTextureScale=source.mainTextureScale;twin.mainTextureOffset=source.mainTextureOffset;
   twin.SetColor("_EmissionColor",source.IsKeywordEnabled("_EMISSION")?source.GetColor("_EmissionColor"):Color.black);
   twin.SetFloat("_Rim",character?CharacterRim:SceneryRim);twin.enableInstancing=source.enableInstancing;
   converted[key]=twin;return twin;
  }
  /// <summary>Restyles every renderer under a root. Bodies (anything under a CharacterController) are left to <see cref="Character"/>.</summary>
  public static void Scenery(Transform root){if(root!=null)Swap(root,false);}
  public static void Character(Transform root){if(root!=null)Swap(root,true);}
  static readonly List<Material> buffer=new List<Material>();
  static void Swap(Transform root,bool character) {
   if(Shader_()==null)return;
   foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) {
    if(renderer is ParticleSystemRenderer||renderer is TrailRenderer||renderer is LineRenderer)continue;
    if(!character&&renderer.GetComponentInParent<CharacterController>()!=null)continue;
    renderer.GetSharedMaterials(buffer);bool changed=false;
    for(int i=0;i<buffer.Count;i++){var twin=Of(buffer[i],character);if(twin!=buffer[i]){buffer[i]=twin;changed=true;}}
    if(changed)renderer.sharedMaterials=buffer.ToArray();
   }
  }
 }
}
