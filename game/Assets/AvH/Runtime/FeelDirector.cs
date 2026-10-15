using UnityEngine;
namespace AvH {
 /// <summary>
 /// One viewer's camera feel. It reads public events from the session state and turns only the ones
 /// this viewer is part of (hit, was hit, swung, staggered, landed or a heavy landing right beside)
 /// into a capped camera shake. Hosts and clients use the same path; it never changes rules.
 /// </summary>
 public sealed class FeelDirector {
  public const float MaxShakeDegrees=2.5f;
  const float DecayPerSecond=1.6f,NearLanding=3;
  float trauma,clock;int lastId;bool primed;
  /// <summary>Local screen-shake preference (off = never shakes).</summary>
  public bool Enabled=true;
  /// <summary>Local quality scale for the shake amplitude (0..1).</summary>
  public float Scale=1;
  public float Trauma=>trauma;
  /// <summary>Current shake amplitude in degrees; never above <see cref="MaxShakeDegrees"/>.</summary>
  public float ShakeDegrees=>Enabled?MaxShakeDegrees*Mathf.Clamp01(Scale)*trauma*trauma:0;
  public void Reset(){trauma=0;primed=false;lastId=0;}
  /// <summary>Consumes events newer than the last seen id. The first observation (join, new session) only primes.</summary>
  public void Observe(SessionState state,int viewer,float seconds) {
   if(seconds>0&&!float.IsInfinity(seconds)){trauma=Mathf.Max(0,trauma-DecayPerSecond*seconds);clock+=seconds;}
   if(state?.Events==null||state.Players==null||viewer<0||viewer>=state.Players.Length)return;
   int newest=0;foreach(var e in state.Events)newest=Mathf.Max(newest,e.Id);
   if(!primed||newest<lastId){primed=true;lastId=newest;return;}
   foreach(var e in state.Events)if(e.Id>lastId)trauma=Mathf.Min(1,trauma+Relevance(e,state,viewer));
   lastId=Mathf.Max(lastId,newest);
  }
  /// <summary>How much an event concerns this viewer (0 = not at all).</summary>
  public static float Relevance(FeelEvent e,SessionState state,int viewer) {
   switch(e.Kind) {
    case FeelEventKind.AttackHit:return e.Target==viewer?.65f:e.Actor==viewer?.45f:0;
    case FeelEventKind.AttackMiss:return e.Actor==viewer?.12f:0;
    case FeelEventKind.Stagger:return e.Target==viewer?.4f:0;
    case FeelEventKind.Landing:
     float weight=.2f+.4f*Mathf.Clamp01(e.Strength);if(e.Actor==viewer)return weight;
     var me=state.Players[viewer].Position;float d=Vector3.Distance(new Vector3(me.X,me.Y,me.Z),new Vector3(e.Position.X,e.Position.Y,e.Position.Z));
     return d<NearLanding?weight*.8f*(1-d/NearLanding):0;
    default:return 0;
   }
  }
  /// <summary>Rotation-only shake, so it can never push the camera through a wall.</summary>
  /// <summary>Angle (degrees) of the last rotation returned by <see cref="Shake"/>.</summary>
  public float LastShakeAngle {get;private set;}
  public Quaternion Shake() {
   float a=ShakeDegrees;LastShakeAngle=0;if(a<=0)return Quaternion.identity;
   float t=clock*22;var wobble=new Vector3(Mathf.PerlinNoise(t,.3f)*2-1,Mathf.PerlinNoise(.7f,t)*2-1,(Mathf.PerlinNoise(t,5.1f)*2-1)*.5f);
   float size=wobble.magnitude;if(size<1e-4f)return Quaternion.identity;
   // Angle is bounded by the amplitude whatever the noise does.
   LastShakeAngle=a*Mathf.Min(1,size*1.6f);return Quaternion.AngleAxis(LastShakeAngle,wobble/size);
  }
 }
}
