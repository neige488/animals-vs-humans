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
  /// <summary>Share of steering an animal keeps while stunned by a bubble.</summary>
  public const float StunnedControl=.35f;
  /// <summary>Knockback decay (m/s per second). Ground keeps the original value; the air slides further.</summary>
  public const float GroundKnockbackFriction=20, AirKnockbackFriction=6;
  /// <summary>Hit-stop: this character holds still (no steering, gravity or knockback) until it ends.</summary>
  public static bool Frozen(PlayerState player)=>player.HitStopRemaining>0;
  /// <summary>Requested direction after impact rules: a stunned animal steers weakly but never loses control.</summary>
  public static MotionIntent Restrain(MotionIntent intent,PlayerState player) {
   if(player.StunRemaining>0){intent.DirectionX*=StunnedControl;intent.DirectionZ*=StunnedControl;}
   return intent;
  }
  /// <summary>Decays a knockback velocity toward zero without reversing it.</summary>
  public static void DecayKnockback(ref float x,ref float y,ref float z,bool grounded,double seconds) {
   float length=(float)Math.Sqrt(x*x+y*y+z*z);if(length<1e-6f){x=y=z=0;return;}
   float next=Math.Max(0,length-(grounded?GroundKnockbackFriction:AirKnockbackFriction)*(float)Math.Max(0,seconds));
   float scale=next/length;x*=scale;y*=scale;z*=scale;
  }
  public static MotionProfile Profile(PlaytestValues rules,PlayerState player) {
   var modifiers=AnimalBalance.For(rules,player);float start,stop;Weight(player,out start,out stop);
   return new MotionProfile{MaxSpeed=player.Faction==Faction.Human?rules.HumanSpeed:rules.AnimalSpeed*modifiers.Speed,
    AccelerationSeconds=rules.InertiaSeconds*start,DecelerationSeconds=rules.InertiaSeconds*stop,AirControl=rules.AirControl};
  }
  // Species personality lives in the curve, not in the agreed speed/jump/knockback multipliers.
  // Values scale the shared inertia slider, so zero inertia stays instant for every species.
  static void Weight(PlayerState player,out float start,out float stop) {
   start=1;stop=1;if(player.Faction!=Faction.Animal)return;
   switch(player.CharacterId) {
    case "animal-fox":start=.8f;stop=.9f;break;          // nimble
    case "animal-wolf":start=1;stop=1;break;              // steady runner
    case "animal-bear_grizzly":start=1.7f;stop=1.6f;break;// heavy both ways
    case "animal-boar":start=1.2f;stop=1.8f;break;        // charges past its stop
    case "animal-rabbit_brown":start=.6f;stop=.7f;break;  // springy
    case "animal-penguin":start=1.1f;stop=1.9f;break;     // belly-slides to a stop
   }
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
   float nx=ux*nextAlong+side.VelocityX,nz=uz*nextAlong+side.VelocityZ;
   // Turning blends the new heading in while the old one decays; never let the sum outrun the species.
   float limit=Math.Max(profile.MaxSpeed,(float)Math.Sqrt(current.VelocityX*current.VelocityX+current.VelocityZ*current.VelocityZ));
   float magnitude=(float)Math.Sqrt(nx*nx+nz*nz);
   if(magnitude>limit&&magnitude>1e-6f){nx*=limit/magnitude;nz*=limit/magnitude;}
   return new MotionState{VelocityX=nx,VelocityZ=nz};
  }
  static MotionState Toward(float x,float z,float tx,float tz,float step) {
   float dx=tx-x,dz=tz-z;float d=(float)Math.Sqrt(dx*dx+dz*dz);
   if(d<=step||d<1e-6f)return new MotionState{VelocityX=tx,VelocityZ=tz};
   return new MotionState{VelocityX=x+dx/d*step,VelocityZ=z+dz/d*step};
  }
 }
}
