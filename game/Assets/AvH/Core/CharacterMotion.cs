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
  /// <summary>
  /// Jump forgiveness, as action games do it: a press this long before landing still jumps on touchdown (buffer),
  /// and a press this long after walking off a ledge still jumps (coyote time).
  /// </summary>
  public const float JumpBufferSeconds=.12f,CoyoteSeconds=.12f;
  /// <summary>Whether a buffered jump may start now: on the ground, or just off it while not already rising.</summary>
  public static bool CanJump(bool grounded,float airborneSeconds,float verticalSpeed,float buffer)=>buffer>0&&(grounded||airborneSeconds<CoyoteSeconds&&verticalSpeed<=0);
  /// <summary>Knockback decay (m/s per second). Ground keeps the original value; the air slides further.</summary>
  public const float GroundKnockbackFriction=20, AirKnockbackFriction=6;
  /// <summary>Hit-stop: this character holds still (no steering, gravity or knockback) until it ends.</summary>
  public static bool Frozen(PlayerState player)=>player.HitStopRemaining>0;
  /// <summary>Requested direction after impact rules: a stunned animal steers weakly but never loses control.</summary>
  public static MotionIntent Restrain(MotionIntent intent,PlayerState player) {
   float control=Control(player);intent.DirectionX*=control;intent.DirectionZ*=control;
   return intent;
  }
  /// <summary>Share of steering after every restraint: held or lifted 0, slowed half, stunned weak.</summary>
  public static float Control(PlayerState p)=>p.RootRemaining>0||p.LiftRemaining>0?0:(p.StunRemaining>0?StunnedControl:1)*(p.SlowRemaining>0?SlowedControl:1);
  public const float SlowedControl=.5f;
  static bool On(PlayerState p,AbilityKind kind)=>p.Ability==kind&&p.AbilityActiveRemaining>0;
  /// <summary>A straight-line ability steers for its user: the body runs along <see cref="PlayerState.AbilityYaw"/>.</summary>
  public static bool Locked(PlayerState p)=>On(p,AbilityKind.Charge)||On(p,AbilityKind.Stampede)||On(p,AbilityKind.Kickboard);
  /// <summary>Jump height (m) right now: species, a cat's spring, and none at all while held, lifted or charging.</summary>
  public static float JumpHeight(PlaytestValues rules,PlayerState p) {
   if(p.RootRemaining>0||p.LiftRemaining>0||Locked(p))return 0;
   float height=p.Faction==Faction.Human?rules.HumanJump:rules.AnimalJump*AnimalBalance.For(rules,p).Jump;
   return On(p,AbilityKind.CatSpring)?height*1.5f:height;
  }
  /// <summary>Gravity for this body: bubbles and balloons rise, wings and umbrellas cap the fall, zero gravity floats.</summary>
  public static AirRule Air(PlayerState p) {
   var air=new AirRule{Gravity=p.LowGravityRemaining>0?.35f:1};
   if(p.LiftRemaining>0)air.Rise=2.2f;
   else if(On(p,AbilityKind.Balloon)){if(p.AbilityActiveRemaining>Abilities.Of(AbilityKind.Balloon).Duration-Abilities.BalloonRiseSeconds)air.Rise=1.7f;else air.FallCap=1.8f;}
   else if(On(p,AbilityKind.Flap)||On(p,AbilityKind.Umbrella))air.FallCap=2.2f;
   return air;
  }
  /// <summary>One step of vertical speed under <see cref="Air"/>.</summary>
  public static float Fall(float vertical,AirRule air,float seconds) {
   if(air.Rise>0)return vertical<air.Rise?Math.Min(air.Rise,vertical+40*seconds):Math.Max(air.Rise,vertical-22*seconds);
   vertical-=22*air.Gravity*seconds;
   return air.FallCap>0&&vertical<-air.FallCap?-air.FallCap:vertical;
  }
  static float AbilitySpeed(PlayerState p) {
   if(p.AbilityActiveRemaining<=0)return 1;
   switch(p.Ability){case AbilityKind.BellySlide:return 1.4f;case AbilityKind.Charge:return 1.9f;case AbilityKind.Stampede:return 1.7f;case AbilityKind.Hunt:return 1.35f;case AbilityKind.Kickboard:return 1.7f;case AbilityKind.Balloon:return .6f;default:return 1;}
  }
  /// <summary>Decays a knockback velocity toward zero without reversing it.</summary>
  public static void DecayKnockback(ref float x,ref float y,ref float z,bool grounded,double seconds) {
   float length=(float)Math.Sqrt(x*x+y*y+z*z);if(length<1e-6f){x=y=z=0;return;}
   float next=Math.Max(0,length-(grounded?GroundKnockbackFriction:AirKnockbackFriction)*(float)Math.Max(0,seconds));
   float scale=next/length;x*=scale;y*=scale;z*=scale;
  }
  /// <summary>
  /// Body radius (m) of the character's collision capsule, sized to its model so a bear no longer sinks into its
  /// neighbours and a rabbit no longer bumps into air. Height stays 1.8 m for every body.
  /// </summary>
  public static float BodyRadius(Faction faction,string characterId) {
   if(faction!=Faction.Animal)return .38f;
   switch(characterId){case "animal-bear_grizzly":return .5f;case "animal-boar":return .46f;case "animal-wolf":return .42f;case "animal-fox":return .34f;case "animal-rabbit_brown":return .3f;case "animal-penguin":return .36f;
    case "animal-cat_orange":case "animal-hen":case "animal-squirrel":return .3f;case "animal-deer":case "animal-goat":return .4f;case "animal-crocodile":case "animal-tiger":return .46f;case "animal-gorilla_silver":case "animal-rhino":return .5f;default:return .38f;}
  }
  /// <summary>How fast a body turns toward where it moves (degrees per second): heavy animals swing round, light ones snap.</summary>
  public static float TurnDegrees(Faction faction,string characterId) {
   if(faction!=Faction.Animal)return 480;
   switch(characterId){case "animal-bear_grizzly":return 240;case "animal-boar":return 300;case "animal-penguin":return 320;case "animal-wolf":return 460;case "animal-fox":return 620;case "animal-rabbit_brown":return 660;
    case "animal-cat_orange":case "animal-squirrel":return 640;case "animal-hen":return 520;case "animal-deer":case "animal-tiger":return 440;case "animal-goat":case "animal-eagle":return 400;case "animal-crocodile":return 260;case "animal-gorilla_silver":return 280;case "animal-rhino":return 220;default:return 480;}
  }
  public static MotionProfile Profile(PlaytestValues rules,PlayerState player) {
   var modifiers=AnimalBalance.For(rules,player);float start,stop;Weight(player,out start,out stop);
   // A belly slide keeps its line: slow to turn, slow to stop.
   if(On(player,AbilityKind.BellySlide)){start*=4;stop*=4;}
   return new MotionProfile{MaxSpeed=(player.Faction==Faction.Human?rules.HumanSpeed:rules.AnimalSpeed*modifiers.Speed)*AbilitySpeed(player),
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
    case "animal-cat_orange":start=.7f;stop=.8f;break;
    case "animal-hen":start=.8f;stop=.8f;break;
    case "animal-squirrel":start=.6f;stop=.7f;break;
    case "animal-deer":start=.9f;stop=1.1f;break;
    case "animal-goat":start=1;stop=1.2f;break;
    case "animal-crocodile":start=1.5f;stop=1.4f;break;
    case "animal-eagle":start=1;stop=1;break;
    case "animal-gorilla_silver":start=1.5f;stop=1.5f;break;
    case "animal-rhino":start=1.8f;stop=2;break;
    case "animal-tiger":start=.9f;stop=1.1f;break;
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
