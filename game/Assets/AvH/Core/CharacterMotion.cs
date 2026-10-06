using System;
namespace AvH {
 /// <summary>Horizontal motion of one character, in world metres per second.</summary>
 [Serializable] public struct MotionState { public float VelocityX, VelocityZ; }
 /// <summary>Requested world direction (magnitude 0..1) and whether the body is on the ground.</summary>
 [Serializable] public struct MotionIntent { public float DirectionX, DirectionZ; public bool Grounded; }
 /// <summary>Effective tuning for one character after host settings and species are applied.</summary>
 public struct MotionProfile { public float MaxSpeed, AccelerationSeconds, DecelerationSeconds, AirControl; }
 /// <summary>Host-side movement rule. Unity-free and deterministic for the same inputs.</summary>
 public static class CharacterMotion {
  public static MotionProfile Profile(PlaytestValues rules,PlayerState player) {
   var modifiers=AnimalBalance.For(rules,player);
   return new MotionProfile{MaxSpeed=player.Faction==Faction.Human?rules.HumanSpeed:rules.AnimalSpeed*modifiers.Speed,
    AccelerationSeconds=rules.InertiaSeconds,DecelerationSeconds=rules.InertiaSeconds,AirControl=rules.AirControl};
  }
  public static MotionState Next(MotionState current,MotionIntent intent,MotionProfile profile,double seconds) {
   float x=intent.DirectionX,z=intent.DirectionZ;float length=(float)Math.Sqrt(x*x+z*z);
   if(length>1){x/=length;z/=length;length=1;}
   float targetX=x*profile.MaxSpeed,targetZ=z*profile.MaxSpeed;
   float control=intent.Grounded?1:Math.Max(.0001f,profile.AirControl);
   float accelSeconds=profile.AccelerationSeconds/control,decelSeconds=profile.DecelerationSeconds/control;
   // Zero tuning is the original rule: velocity equals the request on the same step.
   if(accelSeconds<=0&&decelSeconds<=0)return new MotionState{VelocityX=targetX,VelocityZ=targetZ};
   float dt=(float)Math.Max(0,seconds);
   float accelStep=accelSeconds<=0?float.MaxValue:profile.MaxSpeed/accelSeconds*dt;
   float decelStep=decelSeconds<=0?float.MaxValue:profile.MaxSpeed/decelSeconds*dt;
   if(length<1e-6f) return Toward(current.VelocityX,current.VelocityZ,0,0,decelStep);
   // Split current velocity into the requested heading and the drift across it.
   float ux=x/length,uz=z/length,along=current.VelocityX*ux+current.VelocityZ*uz;
   float sideX=current.VelocityX-along*ux,sideZ=current.VelocityZ-along*uz;
   float wanted=length*profile.MaxSpeed;
   float nextAlong;
   if(along<0)nextAlong=Math.Min(wanted,along+Math.Max(accelStep,decelStep));
   else if(along<wanted)nextAlong=Math.Min(wanted,along+accelStep);
   else nextAlong=Math.Max(wanted,along-decelStep);
   var side=Toward(sideX,sideZ,0,0,decelStep);
   return new MotionState{VelocityX=ux*nextAlong+side.VelocityX,VelocityZ=uz*nextAlong+side.VelocityZ};
  }
  static MotionState Toward(float x,float z,float tx,float tz,float step) {
   float dx=tx-x,dz=tz-z;float d=(float)Math.Sqrt(dx*dx+dz*dz);
   if(d<=step||d<1e-6f)return new MotionState{VelocityX=tx,VelocityZ=tz};
   return new MotionState{VelocityX=x+dx/d*step,VelocityZ=z+dz/d*step};
  }
 }
}
