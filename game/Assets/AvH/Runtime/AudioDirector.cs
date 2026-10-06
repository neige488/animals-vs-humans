using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AvH {
 /// <summary>One sounding effect voice as the listener would hear it: its real AudioSource settings and place.</summary>
 public struct AudioVoiceView {
  public string Cue;public Vector3 Position;
  public float SpatialBlend,MinDistance,MaxDistance,Volume,Pitch,Remaining;
  public int Priority;public bool Synthesized;
 }
 /// <summary>Observable sound state (tests, diagnostics). Batch mode has no device, so this is the evidence of what plays.</summary>
 public sealed class AudioView {
  public AudioVoiceView[] Voices=Array.Empty<AudioVoiceView>();
  internal Dictionary<string,int> Played=new Dictionary<string,int>();
  /// <summary>How many times a cue has been voiced since the director started.</summary>
  public int Count(string cue)=>Played.TryGetValue(cue,out var n)?n:0;
 }
 /// <summary>
 /// Presentation only: turns public events, round phases and character motion into 3D sound.
 /// It never changes rules. Clips come from <see cref="AudioCatalog"/> (CC0 Resources, owned catalog);
 /// anything missing is synthesized so play continues.
 /// </summary>
 public sealed class AudioDirector : MonoBehaviour {
  public bool AutomaticUpdate=true;
  sealed class Voice {public AudioSource Source;public string Cue;public float Until;public int Priority;public bool Synthesized;}
  readonly List<Voice> voices=new List<Voice>();
  readonly Dictionary<string,int> played=new Dictionary<string,int>();
  readonly Dictionary<string,(AudioClip[] clips,bool synthesized)> bank=new Dictionary<string,(AudioClip[],bool)>();
  readonly List<AudioClip> generated=new List<AudioClip>();
  readonly System.Random random=new System.Random(7);
  Func<string,AudioClip> loader;float clock;
  /// <summary>Replaces where clips come from (tests reproduce missing files with a loader that returns null).</summary>
  public void UseLoader(Func<string,AudioClip> load){loader=load;bank.Clear();}
  static AudioClip DefaultLoad(string clip) {
   if(clip.StartsWith("Audio/",StringComparison.Ordinal))return Resources.Load<AudioClip>(clip);
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");return catalog==null?null:catalog.Sound(clip);
  }
  struct Profile {public int Priority;public float Volume,Min,Max;public Profile(int p,float v,float min,float max){Priority=p;Volume=v;Min=min;Max=max;}}
  // Priority (higher survives), base volume and the distance band each cue is heard over.
  static Profile For(string cue) {
   switch(cue) {
    case "transform":return new Profile(10,.8f,3,45);
    case "hit":return new Profile(9,.9f,3,40);
    case "snarl":return new Profile(8,.8f,2.5f,40);
    case "swing":return new Profile(7,.6f,2,30);
    case "dry-fire":return new Profile(7,.7f,1,15);
    case "reload":return new Profile(6,.6f,1.5f,22);
    case "stagger":return new Profile(6,.55f,2,30);
    case "pop":return new Profile(5,.45f,1.5f,28);
    case "fire":return new Profile(5,.3f,1.5f,28);
    case "land":return new Profile(3,.6f,2,30);
    default:
     if(cue.StartsWith("growl",StringComparison.Ordinal))return new Profile(4,.7f,4,45);
     if(cue.StartsWith("step",StringComparison.Ordinal))return new Profile(1,.35f,1.5f,25);
     return new Profile(2,.5f,2,30);
   }
  }
  /// <summary>Plays one 3D effect at a world position. Returns false when it was not voiced.</summary>
  public bool Play(string cue,Vector3 position,float volume=1,float pitch=1) {
   if(string.IsNullOrEmpty(cue)||float.IsNaN(position.x)||float.IsNaN(position.y)||float.IsNaN(position.z))return false;
   var profile=For(cue);var clips=Clips(cue);if(clips.clips.Length==0)return false;
   var voice=voices.FirstOrDefault(v=>v.Until<=clock);
   if(voice==null){var go=new GameObject("Voice");go.transform.SetParent(transform,false);var s=go.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=1;s.rolloffMode=AudioRolloffMode.Logarithmic;s.dopplerLevel=0;s.spread=0;voice=new Voice{Source=s};voices.Add(voice);}
   var source=voice.Source;source.Stop();
   var clip=clips.clips[random.Next(clips.clips.Length)];
   source.transform.position=position;source.clip=clip;source.minDistance=profile.Min;source.maxDistance=profile.Max;
   source.volume=Mathf.Clamp01(profile.Volume*volume);source.pitch=Mathf.Clamp(pitch*(.95f+(float)random.NextDouble()*.1f),.3f,3);source.priority=Mathf.Clamp(128-profile.Priority*10,0,256);
   voice.Cue=cue;voice.Priority=profile.Priority;voice.Synthesized=clips.synthesized;voice.Until=clock+clip.length/source.pitch;
   source.Play();played[cue]=(played.TryGetValue(cue,out var n)?n:0)+1;return true;
  }
  (AudioClip[] clips,bool synthesized) Clips(string cue) {
   if(bank.TryGetValue(cue,out var cached))return cached;
   var entry=AudioCatalog.Find(cue);var load=loader??DefaultLoad;
   var clips=entry==null?new AudioClip[0]:entry.Clips.Select(c=>{try{return load(c);}catch(Exception){return null;}}).Where(c=>c!=null).ToArray();
   bool synthesized=clips.Length==0;
   if(synthesized){var clip=SoundSynth.Make(cue);if(clip!=null){generated.Add(clip);clips=new[]{clip};}}
   return bank[cue]=(clips,synthesized);
  }
  public void Advance(float seconds){if(seconds>0&&!float.IsInfinity(seconds))clock+=seconds;}
  void Update(){if(AutomaticUpdate)Advance(Time.unscaledDeltaTime);}
  public AudioView Observe()=>new AudioView{
   Played=new Dictionary<string,int>(played),
   Voices=voices.Where(v=>v.Until>clock).Select(v=>new AudioVoiceView{Cue=v.Cue,Position=v.Source.transform.position,SpatialBlend=v.Source.spatialBlend,MinDistance=v.Source.minDistance,MaxDistance=v.Source.maxDistance,Volume=v.Source.volume,Pitch=v.Source.pitch,Remaining=v.Until-clock,Priority=v.Priority,Synthesized=v.Synthesized}).ToArray()
  };
  /// <summary>Per-species voice: step cue and pitch, vocal cue and pitch, and the attack-tell snarl pitch.</summary>
  public struct Species {public string Step,Growl;public float StepPitch,StepVolume,GrowlPitch,SnarlPitch;}
  public static Species For(Faction faction,string characterId) {
   if(faction==Faction.Human)return new Species{Step="step-human",StepPitch=1,StepVolume=.6f};
   switch(characterId) {
    case "animal-bear_grizzly":return new Species{Step="step-paw",StepPitch=.62f,StepVolume=1,Growl="growl-bear",GrowlPitch=1,SnarlPitch=.85f};
    case "animal-boar":return new Species{Step="step-paw",StepPitch=.8f,StepVolume=.9f,Growl="growl-bear",GrowlPitch=1.35f,SnarlPitch=1.1f};
    case "animal-wolf":return new Species{Step="step-paw",StepPitch=1,StepVolume=.7f,Growl="growl-wolf",GrowlPitch=1,SnarlPitch=1.15f};
    case "animal-fox":return new Species{Step="step-paw",StepPitch=1.2f,StepVolume=.55f,Growl="growl-wolf",GrowlPitch=1.45f,SnarlPitch=1.4f};
    case "animal-rabbit_brown":return new Species{Step="step-paw",StepPitch=1.45f,StepVolume=.45f,Growl="growl-rabbit",GrowlPitch=1.3f,SnarlPitch=1.7f};
    case "animal-penguin":return new Species{Step="step-paw",StepPitch=1.3f,StepVolume=.5f,Growl="growl-penguin",GrowlPitch=1,SnarlPitch=1.6f};
    default:return new Species{Step="step-paw",StepPitch=1,StepVolume=.7f,Growl="growl-wolf",GrowlPitch=1.2f,SnarlPitch=1.2f};
   }
  }
  void OnDestroy(){foreach(var clip in generated)if(clip!=null)Destroy(clip);}
 }
}
