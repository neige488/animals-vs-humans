using UnityEngine;
namespace AvH {
 // Sound for public events and public state, shared by the host and remote views. Presentation only.
 public sealed partial class UnityPlaytestSession {
  AudioDirector audioDirector;
  readonly bool[] heardReloading=new bool[12];
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
   for(int i=0;i<state.Players.Length&&i<heardReloading.Length;i++) {
    var p=state.Players[i];bool reloading=p.Faction==Faction.Human&&p.ReloadRemaining>0;
    if(reloading&&!heardReloading[i]&&audioPrimed&&!prime&&i<bodies.Count)Audio.Play("reload",bodies[i].transform.position+Vector3.up*1.2f);
    heardReloading[i]=reloading;
   }
   audioPrimed=true;
  }
  void ForgetAudio(){audioDirector=null;audioPrimed=false;System.Array.Clear(heardReloading,0,heardReloading.Length);}
 }
}
