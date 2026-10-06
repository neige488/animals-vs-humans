using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace AvH {
 public enum LocomotionGait { Idle, Walk, Run, Airborne, Landing }
 /// <summary>What the animator is currently showing. Read-only presentation facts.</summary>
 public struct CharacterAnimationView {
  public LocomotionGait Gait;
  /// <summary>0 = idle pose, 1 = full locomotion.</summary>
  public float MoveWeight;
  /// <summary>0 = walk clip, 1 = run clip.</summary>
  public float RunBlend, AirWeight;
  /// <summary>Stride cycles per second (always positive) and +1 forward / -1 reversed stride.</summary>
  public float CyclesPerSecond; public int StrideDirection;
  /// <summary>Human leg heading relative to aim (degrees); the upper body counter-twists to keep aiming.</summary>
  public float LegYaw;
  /// <summary>Positive = squashed (landing), negative = stretched (take-off or rebound).</summary>
  public float Squash, BodyPitch, BodyRoll;
  public bool DrivesAnimator;
  public string IdleClip, WalkClip, RunClip, AirClip;
 }
 /// <summary>
 /// Presentation-only locomotion. One public input (<see cref="LocomotionState"/>) drives speed-blended walk/run,
 /// cadence matched to ground speed, airborne/landing, human strafe/backpedal and species body character.
 /// It never moves the body or changes rules. Vendor controllers are untouched: a Playables graph overrides them.
 /// </summary>
 public sealed class CharacterAnimator : MonoBehaviour {
  struct Character {
   public float WalkRate, RunRate, Lean, Roll, Waddle, Squash, Bounce;
   public Character(float walkRate,float runRate,float lean,float roll,float waddle,float squash,float bounce){WalkRate=walkRate;RunRate=runRate;Lean=lean;Roll=roll;Waddle=waddle;Squash=squash;Bounce=bounce;}
  }
  // Rates match the vendor controller state speeds so top speed looks like the original clips.
  // Lean/roll in degrees; Squash scales landing compression; Bounce is the spring damping ratio (low = springy).
  static Character For(Faction faction,string id) {
   if(faction==Faction.Human)return new Character(1,1,0,0,0,.8f,.7f);
   switch(id) {
    case "animal-fox":return new Character(2,1.5f,5,9,0,1,.6f);
    case "animal-wolf":return new Character(1,1,5,7,0,1,.65f);
    case "animal-bear_grizzly":return new Character(1,1.5f,2.5f,4,0,1.8f,1);
    case "animal-boar":return new Character(2,1.5f,8,4,0,1.2f,.8f);
    case "animal-rabbit_brown":return new Character(2.4f,1.5f,3,6,0,.9f,.22f);
    case "animal-penguin":return new Character(1,1,2,3,7,1.1f,.5f);
    default:return new Character(1,1,4,6,0,1,.6f);
   }
  }
  const float StartSpeed=.25f,StopSpeed=.12f,StopHold=.1f,AirDelay=.08f,LandSeconds=.2f;
  PlayableGraph graph;AnimationMixerPlayable mixer;AnimationClipPlayable idle,walk,run,air;
  AnimationClip idleClip,walkClip,runClip,airClip;bool airIsJump;
  Transform visual;Vector3 baseScale;Quaternion baseRotation;BubbleGunPose pose;Character character;float referenceSpeed;
  bool moving,airborne,lastGrounded=true;float stillTime,airTime,landTimer,phase,idleTime,lastSpeed,lastYaw,lastFall;
  float squash,squashVelocity,pitch,roll;
  CharacterAnimationView view;
  public void Bind(Transform model,Faction faction,string characterId,BubbleGunPose gunPose) {
   Release();visual=model;pose=gunPose;character=For(faction,characterId);
   baseScale=model.localScale;baseRotation=model.localRotation;
   referenceSpeed=Mathf.Max(.1f,CharacterMotion.Profile(new PlaytestValues(),new PlayerState{Faction=faction,CharacterId=characterId}).MaxSpeed);
   moving=airborne=false;lastGrounded=true;stillTime=airTime=landTimer=phase=idleTime=squash=squashVelocity=pitch=roll=0;lastYaw=transform.eulerAngles.y;lastSpeed=0;
   view=new CharacterAnimationView{Gait=LocomotionGait.Idle,StrideDirection=1};
   Animator animator=null;foreach(var a in model.GetComponentsInChildren<Animator>())if(a.runtimeAnimatorController!=null){animator=a;break;}
   if(animator==null)return;
   var clips=animator.runtimeAnimatorController.animationClips;
   idleClip=Find(clips,"Idle_Generic","Idle_Breathing","_Idle");walkClip=Find(clips,"Common_Walk","Jump_Walk","_Walk");runClip=Find(clips,"Run_InPlace","_Run");
   var jump=Find(clips,"Jump_Up");airIsJump=jump!=null;airClip=jump??runClip;
   if(idleClip==null||walkClip==null||runClip==null)return;
   graph=PlayableGraph.Create("AvH locomotion "+name);graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
   mixer=AnimationMixerPlayable.Create(graph,4);
   idle=Clip(idleClip,0);walk=Clip(walkClip,1);run=Clip(runClip,2);air=Clip(airClip,3);
   var output=AnimationPlayableOutput.Create(graph,"Locomotion",animator);output.SetSourcePlayable(mixer);
   graph.Play();Weights(1,0,0,0);
   view.IdleClip=idleClip.name;view.WalkClip=walkClip.name;view.RunClip=runClip.name;view.AirClip=airClip.name;view.DrivesAnimator=true;
  }
  AnimationClipPlayable Clip(AnimationClip clip,int input) {
   var playable=AnimationClipPlayable.Create(graph,clip);playable.SetSpeed(0);playable.SetApplyFootIK(true);playable.SetApplyPlayableIK(false);
   graph.Connect(playable,0,mixer,input);return playable;
  }
  static AnimationClip Find(AnimationClip[] clips,params string[] patterns) {
   foreach(var pattern in patterns)foreach(var clip in clips) {
    if(clip==null)continue;
    bool match=pattern.StartsWith("_")?clip.name.EndsWith(pattern,StringComparison.OrdinalIgnoreCase):clip.name.IndexOf(pattern,StringComparison.OrdinalIgnoreCase)>=0;
    if(match)return clip;
   }
   return null;
  }
  public CharacterAnimationView Observe(){var v=view;v.DrivesAnimator=graph.IsValid()&&graph.IsPlaying();return v;}
  public void Apply(LocomotionState state,float seconds) {
   if(seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return;
   float dt=seconds;var velocity=new Vector3(state.VelocityX,0,state.VelocityZ);float speed=velocity.magnitude;
   // Gait hysteresis: start instantly, but only call it stopped after holding still briefly.
   // Thresholds shrink with a very slow configured top speed so a slow walk never glides in the idle pose.
   float start=state.TopSpeed>0?Mathf.Min(StartSpeed,state.TopSpeed*.5f):StartSpeed,stop=state.TopSpeed>0?Mathf.Min(StopSpeed,state.TopSpeed*.25f):StopSpeed;
   if(speed>start){moving=true;stillTime=0;}
   else if(speed<stop){stillTime+=dt;if(stillTime>=StopHold)moving=false;}
   else stillTime=0;
   float walkSpeed=referenceSpeed*.3f,runSpeed=referenceSpeed*.85f;
   float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(walkSpeed,runSpeed,speed));
   view.RunBlend=Mathf.MoveTowards(view.RunBlend,blend,dt/.08f);
   view.MoveWeight=Mathf.MoveTowards(view.MoveWeight,moving?1:0,dt/.1f);
   // Airborne only after a short grace so stairs and kerbs do not flash the air pose.
   bool wasAirborne=airborne;
   if(state.Grounded){airTime=0;airborne=false;}else{airTime+=dt;if(airTime>=AirDelay)airborne=true;}
   // Take-off stretch, then a landing squash scaled by impact speed and species weight.
   if(lastGrounded&&!state.Grounded&&state.VerticalSpeed>2){squash=-.08f;squashVelocity=0;}
   if(wasAirborne&&!airborne){landTimer=LandSeconds;float impact=Mathf.Max(0,-Mathf.Min(lastFall,state.VerticalSpeed));squash=Mathf.Clamp(impact/40f*character.Squash,.03f,.35f);squashVelocity=0;}
   lastFall=state.VerticalSpeed;lastGrounded=state.Grounded;
   landTimer=Mathf.Max(0,landTimer-(airborne?0:dt));
   view.AirWeight=Mathf.MoveTowards(view.AirWeight,airborne?1:0,dt/.06f);
   // Human stride: legs turn toward the move heading, reversing the cycle when backing away.
   int direction=1;float legTarget=0;
   if(pose!=null&&moving&&speed>stop) {
    float moveYaw=Mathf.Atan2(velocity.x,velocity.z)*Mathf.Rad2Deg;float relative=Mathf.DeltaAngle(state.AimYaw,moveYaw);
    float limit=view.StrideDirection<0?100:125;
    direction=Mathf.Abs(relative)<=limit?1:-1;
    legTarget=Mathf.Clamp(direction>0?relative:Mathf.DeltaAngle(0,relative+180),-75,75);
   } else if(pose!=null)direction=view.StrideDirection==0?1:view.StrideDirection;
   view.StrideDirection=direction;
   view.LegYaw=Mathf.MoveTowards(view.LegYaw,legTarget,dt*600);
   if(pose!=null)pose.LegYaw=view.LegYaw;
   // Cadence follows ground speed against the species' default top speed across the whole settings range.
   float walkCycles=walkClip==null?0:character.WalkRate/walkClip.length*speed/Mathf.Max(.01f,walkSpeed);
   float runCycles=runClip==null?0:character.RunRate/runClip.length*speed/referenceSpeed;
   float cycles=walkClip==null||runClip==null?1.6f*speed/referenceSpeed:Mathf.Lerp(walkCycles,runCycles,view.RunBlend);
   view.CyclesPerSecond=moving?cycles:0;
   if(moving&&!airborne)phase=Mathf.Repeat(phase+direction*cycles*dt,1);
   idleTime+=dt;
   // Body character: lean into acceleration, roll into turns, penguin waddle.
   float accel=(speed-lastSpeed)/dt;lastSpeed=speed;float yaw=transform.eulerAngles.y;float yawRate=Mathf.DeltaAngle(lastYaw,yaw)/dt;lastYaw=yaw;
   float pitchTarget=pose!=null?0:Mathf.Clamp(accel/Mathf.Max(1,referenceSpeed*4),-1,1)*character.Lean*1.6f+(moving?character.Lean*.25f*view.RunBlend:0);
   float rollTarget=pose!=null?0:-Mathf.Clamp(yawRate/360f,-1,1)*character.Roll+(airborne?0:Mathf.Sin(phase*Mathf.PI*4)*character.Waddle*view.MoveWeight);
   pitch=Mathf.Lerp(pitch,pitchTarget,1-Mathf.Exp(-dt/.06f));roll=Mathf.Lerp(roll,rollTarget,1-Mathf.Exp(-dt/.05f));
   // Damped spring returns squash to rest; low damping (rabbit) overshoots into a springy rebound.
   const float stiffness=300;float damping=2*Mathf.Sqrt(stiffness)*character.Bounce;
   for(float left=dt;left>0;left-=.005f){float h=Mathf.Min(.005f,left);squashVelocity+=(-stiffness*squash-damping*squashVelocity)*h;squash+=squashVelocity*h;}
   if(Mathf.Abs(squash)<.0005f&&Mathf.Abs(squashVelocity)<.01f){squash=0;squashVelocity=0;}
   view.Squash=squash;view.BodyPitch=pitch;view.BodyRoll=roll;
   view.Gait=airborne?LocomotionGait.Airborne:landTimer>0?LocomotionGait.Landing:!moving?LocomotionGait.Idle:view.RunBlend>=.5f?LocomotionGait.Run:LocomotionGait.Walk;
   Present(state);
  }
  void Present(LocomotionState state) {
   if(visual!=null) {
    visual.localScale=new Vector3(baseScale.x*(1+squash*.5f),baseScale.y*(1-squash),baseScale.z*(1+squash*.5f));
    if(pose==null)visual.localRotation=baseRotation*Quaternion.Euler(pitch,0,roll);
   }
   if(!graph.IsValid())return;
   float ground=1-view.AirWeight;
   Weights(ground*(1-view.MoveWeight),ground*view.MoveWeight*(1-view.RunBlend),ground*view.MoveWeight*view.RunBlend,view.AirWeight);
   idle.SetTime(Mathf.Repeat(idleTime,idleClip.length));
   walk.SetTime(phase*walkClip.length);run.SetTime(phase*runClip.length);
   // Rabbit plays its own jump by vertical speed; others hold an extended stride while airborne.
   float airPhase=airIsJump?Mathf.Lerp(.45f,.15f,Mathf.InverseLerp(-8,8,state.VerticalSpeed)):.3f;
   air.SetTime(airPhase*airClip.length);
  }
  void Weights(float a,float b,float c,float d){mixer.SetInputWeight(0,a);mixer.SetInputWeight(1,b);mixer.SetInputWeight(2,c);mixer.SetInputWeight(3,d);}
  void Release(){if(graph.IsValid())graph.Destroy();if(pose!=null)pose.LegYaw=0;}
  void OnDestroy(){Release();}
 }
}
