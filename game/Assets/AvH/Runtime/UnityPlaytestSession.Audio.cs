using UnityEngine;
namespace AvH {
 // Sound for public events and public state, shared by the host and remote views. Presentation only.
 public sealed partial class UnityPlaytestSession {
  AudioDirector audioDirector;
  readonly bool[] heardReloading=new bool[12],heardAirborne=new bool[12],wasAnimal=new bool[12];
  readonly int[] heardFootfalls=new int[12];readonly float[] growlIn=new float[12];
  readonly System.Random growlRandom=new System.Random(29);
  bool audioPrimed;
  /// <summary>This world's sound: 3D effects, music and ambience.</summary>
  public AudioDirector Audio {get {if(audioDirector==null){var go=new GameObject("Audio director");go.transform.SetParent(transform,false);audioDirector=go.AddComponent<AudioDirector>();}return audioDirector;}}
  Vector3 SoundAt(Transform body,WorldPosition fallback,float up=.9f)=>body!=null?body.position+Vector3.up*up:ToVector(fallback);
  AudioDirector.Species SpeciesOf(int slot){var state=Observe();var p=slot>=0&&slot<state.Players.Length?state.Players[slot]:null;return AudioDirector.For(p?.Faction??Faction.Animal,p?.CharacterId);}
  void PresentEventAudio(FeelEvent e,Transform actor,Transform target) {
   switch(e.Kind) {
    case FeelEventKind.AttackWindup:Audio.Play("snarl",SoundAt(actor,e.Position),1,SpeciesOf(e.Actor).SnarlPitch);break;
    case FeelEventKind.AttackMiss:Audio.Play("swing",actor!=null?actor.position+Vector3.up*.9f+actor.forward*.6f:ToVector(e.Position));break;
    case FeelEventKind.AttackHit:Audio.Play("hit",SoundAt(target,e.Position));break;
    case FeelEventKind.Transform:Audio.Play("transform",SoundAt(target,e.Position));break;
    case FeelEventKind.Stagger:Audio.Play("stagger",SoundAt(target,e.Position,1.2f));break;
    case FeelEventKind.Landing:Audio.Play("land",SoundAt(actor,e.Position,0),.5f+.5f*Mathf.Clamp01(e.Strength));break;
   }
  }
  /// <summary>Sounds read from public state (reload starts). The first observation of a session only primes.</summary>
  void PresentStateAudio(SessionState state,bool prime) {
   if(state?.Players==null)return;
   Audio.SetPhase(state.Phase,state.SecondsRemaining);
   for(int i=0;i<state.Players.Length&&i<heardReloading.Length;i++) {
    var p=state.Players[i];bool reloading=p.Faction==Faction.Human&&p.ReloadRemaining>0;
    if(reloading&&!heardReloading[i]&&audioPrimed&&!prime&&i<bodies.Count)Audio.Play("reload",bodies[i].transform.position+Vector3.up*1.2f);
    heardReloading[i]=reloading;
   }
   audioPrimed=true;
  }
  /// <summary>Footsteps on each footfall the animator shows, a touch-down after air time, and an occasional growl from chasing animals.</summary>
  void PresentMotionAudio(SessionState state,float seconds) {
   if(state?.Players==null)return;
   for(int i=0;i<bodies.Count&&i<state.Players.Length;i++) {
    var animator=bodies[i].GetComponent<CharacterAnimator>();if(animator==null)continue;
    var view=animator.Observe();var p=state.Players[i];var species=AudioDirector.For(p.Faction,p.CharacterId);var feet=bodies[i].transform.position;
    bool striding=view.Gait==LocomotionGait.Walk||view.Gait==LocomotionGait.Run;
    if(view.Footfalls<heardFootfalls[i])heardFootfalls[i]=view.Footfalls; // a new model restarts its count
    if(view.Footfalls>heardFootfalls[i]){heardFootfalls[i]=view.Footfalls;if(striding)Audio.Play(species.Step,feet,species.StepVolume*(view.Gait==LocomotionGait.Run?1:.6f),species.StepPitch);}
    bool air=view.Gait==LocomotionGait.Airborne;if(heardAirborne[i]&&!air)Audio.Play(species.Step,feet,species.StepVolume*1.2f,species.StepPitch*.9f);heardAirborne[i]=air;
    bool animal=p.Faction==Faction.Animal;if(animal&&!wasAnimal[i])growlIn[i]=2+(float)growlRandom.NextDouble()*5;wasAnimal[i]=animal;
    if(animal&&state.Phase==RoundPhase.Chase&&striding){growlIn[i]-=seconds;if(growlIn[i]<=0){Audio.Play(species.Growl,feet+Vector3.up*.9f,1,species.GrowlPitch);growlIn[i]=7+(float)growlRandom.NextDouble()*7;}}
   }
  }
  readonly bool[] triggerHeld=new bool[12];
  /// <summary>
  /// The local viewer's trigger (held state, every frame). Pulling it on an empty magazine clicks once per pull.
  /// Local feedback only: it reads the public ammo count and never changes rules.
  /// </summary>
  public void PresentTrigger(int slot,bool held) {
   if(slot<0||slot>=triggerHeld.Length||slot>=bodies.Count)return;
   bool pulled=held&&!triggerHeld[slot];triggerHeld[slot]=held;if(!pulled)return;
   var state=Observe();if(state.Phase==RoundPhase.Results||slot>=state.Players.Length)return;var p=state.Players[slot];
   if(p.Faction==Faction.Human&&p.Ammo<=0)Audio.Play("dry-fire",bodies[slot].transform.position+Vector3.up*1.3f);
  }
  void ForgetAudio(){System.Array.Clear(triggerHeld,0,12);audioDirector=null;audioPrimed=false;System.Array.Clear(heardReloading,0,heardReloading.Length);System.Array.Clear(heardAirborne,0,12);System.Array.Clear(wasAnimal,0,12);System.Array.Clear(heardFootfalls,0,12);System.Array.Clear(growlIn,0,12);}
 }
}
