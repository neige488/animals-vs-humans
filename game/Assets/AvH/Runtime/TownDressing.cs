using System.Collections.Generic;
using UnityEngine;
namespace AvH {
 /// <summary>
 /// A village's living dressing at the current <see cref="TownAtmosphere.Quality"/>: trees, bushes, pennants and
 /// market ribbons sway on their own pivots (the vendor wind shader is gone since the materials became Standard), and
 /// each lantern carries a shadowless point light. Collision is untouched: trunks keep their separate colliders.
 /// </summary>
 public sealed class TownDressing : MonoBehaviour {
  sealed class Sway {public Transform Target;public Vector3 Pivot,Rest,Axis;public Quaternion Rotation;public float Amplitude,Frequency,Phase;public bool Detail;}
  readonly List<Sway> sways=new List<Sway>();readonly List<Light> lanterns=new List<Light>();
  int applied=-1;float amplitude=1;bool details=true;
  /// <summary>Most lantern lights a village carries (all twelve of the generated market).</summary>
  public const int MaxLanterns=12;
  static readonly Vector3 Wind=new Vector3(.8f,0,.6f).normalized;
  public static TownDressing Dress(Transform village) {
   var dressing=village.gameObject.AddComponent<TownDressing>();
   foreach(var t in village.GetComponentsInChildren<Transform>(true)) {
    if(t.name.StartsWith("SM_Env_Tree"))dressing.Add(t,Bottom(t),Vector3.Cross(Vector3.up,Wind),1.3f,.45f,false);
    else if(t.name.StartsWith("SM_Env_Bush"))dressing.Add(t,Bottom(t),Vector3.Cross(Vector3.up,Wind),2.6f,.7f,true);
    else if(t.name=="District pennant")dressing.Add(t,t.position-t.right*(t.lossyScale.x*.5f),Vector3.up,13,1.1f,false);
    else if(t.name=="Market ribbon")dressing.Add(t,t.position+t.up*(t.lossyScale.y*.5f),t.right,15,1.5f,false);
    else if(t.name.StartsWith("SM_Item_Lantern")&&dressing.lanterns.Count<MaxLanterns)dressing.Lantern(t);
   }
   return dressing;
  }
  static Vector3 Bottom(Transform t){var renderers=t.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return t.position;var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return new Vector3(b.center.x,b.min.y,b.center.z);}
  void Add(Transform target,Vector3 pivot,Vector3 axis,float degrees,float hertz,bool detail) {
   // Neighbours sway out of step: the phase follows the position.
   float phase=(target.position.x*.37f+target.position.z*.23f)%(Mathf.PI*2);
   sways.Add(new Sway{Target=target,Pivot=pivot,Rest=target.position,Rotation=target.rotation,Axis=axis.normalized,Amplitude=degrees,Frequency=hertz*(.85f+.3f*Mathf.Abs(Mathf.Sin(phase*3.1f))),Phase=phase,Detail=detail});
  }
  void Lantern(Transform lantern) {
   var go=new GameObject("Lantern light");go.transform.SetParent(lantern,false);go.transform.position=Bottom(lantern)+Vector3.up*.25f;
   var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=6.5f;light.intensity=1.8f;light.color=new Color(1,.72f,.4f);light.shadows=LightShadows.None;light.renderMode=LightRenderMode.Auto;
   lanterns.Add(light);
  }
  void LateUpdate() {
   if(applied!=TownAtmosphere.Version)ApplyQuality();
   if(amplitude<=0)return;
   float time=Time.time*Mathf.PI*2;
   foreach(var s in sways) {
    if(s.Detail&&!details)continue;
    float angle=s.Amplitude*amplitude*(Mathf.Sin(time*s.Frequency+s.Phase)*.75f+Mathf.Sin(time*s.Frequency*2.3f+s.Phase*1.7f)*.25f);
    var turn=Quaternion.AngleAxis(angle,s.Axis);s.Target.SetPositionAndRotation(s.Pivot+turn*(s.Rest-s.Pivot),turn*s.Rotation);
   }
  }
  void ApplyQuality() {
   applied=TownAtmosphere.Version;var quality=TownAtmosphere.Quality;
   amplitude=quality==GraphicsQuality.High?1:quality==GraphicsQuality.Medium?.55f:0;details=quality==GraphicsQuality.High;
   foreach(var s in sways)if(amplitude<=0||s.Detail&&!details)s.Target.SetPositionAndRotation(s.Rest,s.Rotation);
   for(int i=0;i<lanterns.Count;i++)lanterns[i].enabled=quality==GraphicsQuality.High||quality==GraphicsQuality.Medium&&i%2==0;
  }
  public TownDressingView Observe() {
   if(applied!=TownAtmosphere.Version)ApplyQuality();
   int swaying=0;if(amplitude>0)foreach(var s in sways)if(!s.Detail||details)swaying++;
   int lit=0;bool shadows=false;foreach(var l in lanterns){if(l.enabled)lit++;shadows|=l.shadows!=LightShadows.None;}
   return new TownDressingView{SwayingObjects=swaying,SwayAmplitude=amplitude,LanternLights=lit,LanternShadows=shadows};
  }
 }
}
