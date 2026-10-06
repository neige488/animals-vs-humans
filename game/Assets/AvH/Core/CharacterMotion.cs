using System;
namespace AvH {
 /// <summary>Horizontal motion of one character, in world metres per second.</summary>
 [Serializable] public struct MotionState { public float VelocityX, VelocityZ; }
 /// <summary>Requested world direction (magnitude 0..1) and whether the body is on the ground.</summary>
 [Serializable] public struct MotionIntent { public float DirectionX, DirectionZ; public bool Grounded; }
 /// <summary>Effective tuning for one character after host settings and species are applied.</summary>
 public struct MotionProfile { public float MaxSpeed; }
 /// <summary>Host-side movement rule. Unity-free and deterministic for the same inputs.</summary>
 public static class CharacterMotion {
  public static MotionProfile Profile(PlaytestValues rules,PlayerState player) {
   var modifiers=AnimalBalance.For(rules,player);
   return new MotionProfile{MaxSpeed=player.Faction==Faction.Human?rules.HumanSpeed:rules.AnimalSpeed*modifiers.Speed};
  }
  public static MotionState Next(MotionState current,MotionIntent intent,MotionProfile profile,double seconds) {
   float x=intent.DirectionX,z=intent.DirectionZ;float length=(float)Math.Sqrt(x*x+z*z);
   if(length>1){x/=length;z/=length;}
   return new MotionState{VelocityX=x*profile.MaxSpeed,VelocityZ=z*profile.MaxSpeed};
  }
 }
}
