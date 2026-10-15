using System;
using System.Collections.Generic;
namespace AvH {
 public struct BotAimOutput { public float Yaw,Pitch; public bool CanFire; }
 /// <summary>
 /// One bot's human-like aim. The aim follows the exact heading the bot sees, but as it was a reaction time ago,
 /// plus a slowly wandering error; firing at a newly seen target waits for that reaction time.
 /// Zero delay and zero error return the exact aim and fire whenever the target is visible (the original bot).
 /// Deterministic per seed so bot matches stay reproducible.
 /// </summary>
 public sealed class BotAim {
  // Error wander: a new random offset every WanderSeconds, eased between, so the aim drifts rather than jitters.
  const double WanderSeconds=.6,HistorySeconds=1.2;
  readonly int seed;
  readonly List<(double time,float yaw,float pitch)> seen=new List<(double,float,float)>();
  int target=-1;double acquiredAt;bool acquired;float unwrappedYaw;
  public BotAim(int seed){this.seed=seed;}
  public void Reset(){seen.Clear();acquired=false;target=-1;}
  public BotAimOutput Step(double time,int targetSlot,bool visible,float exactYaw,float exactPitch,float delaySeconds,float errorDegrees) {
   delaySeconds=Math.Max(0,delaySeconds);errorDegrees=Math.Max(0,errorDegrees);
   if(!visible||targetSlot!=target){acquired=false;target=targetSlot;}
   if(visible&&!acquired){acquired=true;acquiredAt=time;}
   bool canFire=visible&&time-acquiredAt>=delaySeconds-1e-5;
   // Keep a continuous heading so a delayed sample never interpolates the long way round.
   unwrappedYaw=seen.Count==0?exactYaw:unwrappedYaw+DeltaAngle(unwrappedYaw,exactYaw);
   seen.Add((time,unwrappedYaw,exactPitch));
   while(seen.Count>2&&seen[1].time<time-Math.Max(delaySeconds,0)-HistorySeconds)seen.RemoveAt(0);
   if(delaySeconds==0&&errorDegrees==0)return new BotAimOutput{Yaw=exactYaw,Pitch=exactPitch,CanFire=canFire};
   Sample(time-delaySeconds,out float yaw,out float pitch);
   return new BotAimOutput{Yaw=Normalize(yaw+errorDegrees*Wander(time,0)),Pitch=pitch+errorDegrees*Wander(time,1),CanFire=canFire};
  }
  void Sample(double at,out float yaw,out float pitch) {
   int i=seen.Count-1;while(i>0&&seen[i].time>at)i--;
   var a=seen[i];if(i==seen.Count-1||a.time>=at){yaw=a.yaw;pitch=a.pitch;return;}
   var b=seen[i+1];float t=(float)((at-a.time)/(b.time-a.time));yaw=a.yaw+(b.yaw-a.yaw)*t;pitch=a.pitch+(b.pitch-a.pitch)*t;
  }
  // Smooth value noise in [-1, 1] from the seed, the time segment and the axis.
  float Wander(double time,int axis) {
   double s=time/WanderSeconds;long k=(long)Math.Floor(s);float f=(float)(s-k);f=f*f*(3-2*f);
   float a=Random(k,axis),b=Random(k+1,axis);return a+(b-a)*f;
  }
  float Random(long k,int axis){unchecked{uint h=(uint)(seed*73856093)^(uint)(k*19349663)^(uint)(axis*83492791+1);h^=h>>13;h*=0x5bd1e995;h^=h>>15;return h/(float)uint.MaxValue*2-1;}}
  static float DeltaAngle(float from,float to){float d=(to-from)%360;if(d>180)d-=360;if(d<-180)d+=360;return d;}
  static float Normalize(float degrees){degrees%=360;if(degrees>=180)degrees-=360;if(degrees<-180)degrees+=360;return degrees;}
 }
}
