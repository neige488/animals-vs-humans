using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 // Sound through the same game boundary: public events, round phases and character state drive 3D voices.
 // Batch mode has no audio device, so tests read the director's observable voices (real AudioSource settings).
 public class AudioPlayTests {
  string profile;GameObject root;UnityPlaytestSession world;
  [TearDown] public void Cleanup(){if(root!=null)Object.Destroy(root);if(profile!=null&&File.Exists(profile))File.Delete(profile);}
  // Slot 0 and slot 4 get the requested factions after a one second preparation without attack grace.
  IEnumerator Create(Faction slot0,Faction slot4,System.Action<PlaytestValues> tune=null) {
   profile=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   var setup=new PlaytestSession(1,profile);setup.BeginSettingsEdit(0);var v=setup.ObserveSettings().Edit;v.PreparationSeconds=1;v.InitialAttackGrace=0;v.TransformAttackGrace=0;tune?.Invoke(v);setup.UpdateSettingsEdit(0,v);Assert.IsTrue(setup.ApplySettingsNow(0,true));
   int seed=0;for(;seed<2000;seed++){var probe=new PlaytestSession(seed,profile);probe.StartSolo("probe");probe.Advance(1.01);var s=probe.Observe();if(s.Players[0].Faction==slot0&&s.Players[4].Faction==slot4&&s.Players[8].Faction==Faction.Human)break;}
   Assert.Less(seed,2000);
   root=new GameObject("audio world");world=root.AddComponent<UnityPlaytestSession>();world.AutomaticStep=false;world.BotAutomationEnabled=false;world.StartSolo("host",seed,profile);
   world.Audio.AutomaticUpdate=false;
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(100,-.5f,0);floor.transform.localScale=new Vector3(120,1,60);
   yield return new WaitForFixedUpdate();
  }
  void Warp(int slot,Vector3 position){var body=world.PlayerTransform(slot).GetComponent<CharacterController>();body.enabled=false;body.transform.position=position;body.enabled=true;world.Session.RecordWorldPosition(slot,new WorldPosition(position.x,position.y,position.z));}
  void Spread(){foreach(var p in world.Observe().Players)Warp(p.Slot,new Vector3(60+p.Slot*7,0,-20));Warp(0,new Vector3(100,0,0));Warp(4,new Vector3(100,0,1.2f));Warp(8,new Vector3(110,0,0));Physics.SyncTransforms();}
  void Step(int steps,params (int slot,PlayerInput input)[] inputs){for(int i=0;i<steps;i++){foreach(var (slot,input) in inputs)world.SubmitInput(slot,input);world.Step(.02f);}}
  AudioVoiceView[] Voices(string cue)=>world.Audio.Observe().Voices.Where(v=>v.Cue==cue).ToArray();
  static void Spatial(AudioVoiceView voice,Vector3 at,string what) {
   Assert.AreEqual(1,voice.SpatialBlend,1e-4,what+" is a 3D sound");
   Assert.Less(Vector3.Distance(voice.Position,at),1.5f,what+" sounds where it happened");
   Assert.Greater(voice.MaxDistance,voice.MinDistance,what+" fades with distance");
  }

  [UnityTest] public IEnumerator PublicEventsPlay3DSoundsWhereTheyHappen() {
   yield return Create(Faction.Animal,Faction.Human,v=>{v.AttackWindupSeconds=.15f;v.HitStopSeconds=0;});
   world.Step(.02f);world.Step(1f);
   Assert.AreEqual(2,world.Audio.Observe().Count("transform"),"Both initial animals are announced by a transformation sound");
   Spread();Step(5);world.Audio.Advance(5);
   Step(1,(0,new PlayerInput{Attack=true,Yaw=0}));
   var snarl=Voices("snarl");Assert.AreEqual(1,snarl.Length,"The attack tell is heard");Spatial(snarl[0],world.PlayerTransform(0).position,"The tell");
   Step(8,(0,new PlayerInput{Yaw=0}));
   Assert.AreEqual(Faction.Animal,world.Observe().Players[4].Faction);
   Spatial(Voices("hit").Single(),world.PlayerTransform(4).position,"The hit");
   Spatial(Voices("transform").Single(),world.PlayerTransform(4).position,"The transformation");
   world.Audio.Advance(5);
   // A swing into empty air.
   Warp(0,new Vector3(130,0,0));Physics.SyncTransforms();Step(30,(0,new PlayerInput{Yaw=180}));
   Step(1,(0,new PlayerInput{Attack=true,Yaw=180}));Step(10,(0,new PlayerInput{Yaw=180}));
   Spatial(Voices("swing").Single(),world.PlayerTransform(0).position,"The missed swing");
   // A human fires, reloads, and the bubble pops on the animal.
   world.Audio.Advance(5);
   Warp(8,new Vector3(100,0,-4));Physics.SyncTransforms();Step(30);
   Step(1,(8,new PlayerInput{Attack=true,Yaw=0}));
   Spatial(Voices("fire").Single(),world.PlayerTransform(8).position+Vector3.up*1.3f,"The bubble shot");
   for(int i=0;i<30&&Voices("pop").Length==0;i++)Step(1,(8,new PlayerInput{Yaw=0}));
   Assert.AreEqual(1,Voices("pop").Length,"The bubble pop is heard");Assert.AreEqual(1,Voices("pop")[0].SpatialBlend,1e-4);
   Assert.AreEqual(1,Voices("stagger").Length,"The staggered animal is heard");
   Step(1,(8,new PlayerInput{Reload=true,Yaw=0}));
   Spatial(Voices("reload").Single(),world.PlayerTransform(8).position,"The reload");
   var sources=root.GetComponentsInChildren<AudioSource>().Where(s=>s.clip!=null&&!s.loop).ToArray();
   Assert.IsNotEmpty(sources);foreach(var s in sources)Assert.AreEqual(1,s.spatialBlend,1e-4,"Every effect source is 3D: "+s.clip.name);
  }

  [UnityTest] public IEnumerator FootstepsFollowTheStrideAndAnimalsGrowlWhileChasing() {
   yield return Create(Faction.Animal,Faction.Human,v=>{v.HitStopSeconds=0;});
   world.Step(.02f);world.Step(1f);Spread();Warp(0,new Vector3(70,0,10));Warp(8,new Vector3(70,0,-10));Physics.SyncTransforms();Step(10);world.Audio.Advance(5);
   var state=world.Observe();var animal=AudioDirector.For(Faction.Animal,state.Players[0].CharacterId);var human=AudioDirector.For(Faction.Human,state.Players[8].CharacterId);
   Assert.AreNotEqual(human.Step,animal.Step,"Animals and humans have different footsteps");
   int humanSteps=world.Audio.Observe().Count(human.Step),animalSteps=world.Audio.Observe().Count(animal.Step);
   Step(25);
   Assert.AreEqual(humanSteps,world.Audio.Observe().Count(human.Step),"Standing still makes no footsteps");
   int heardHuman=0;float pitch=0;
   for(int i=0;i<50;i++){Step(1,(8,new PlayerInput{Forward=1,Yaw=90}),(0,new PlayerInput{Forward=1,Yaw=90}));
    foreach(var voice in world.Audio.Observe().Voices.Where(x=>x.Cue==animal.Step&&Vector3.Distance(x.Position,world.PlayerTransform(0).position)<1.5f))pitch=voice.Pitch;
    if(world.Audio.Observe().Voices.Any(x=>x.Cue==human.Step&&Vector3.Distance(x.Position,world.PlayerTransform(8).position)<1.5f))heardHuman++;}
   int walked=world.Audio.Observe().Count(human.Step)-humanSteps;
   Assert.GreaterOrEqual(walked,3,"One second of running is several footfalls");Assert.LessOrEqual(walked,12,"...one per stride foot, not one per frame");
   Assert.Greater(heardHuman,0,"The footstep is a 3D sound at the runner's feet");
   Assert.GreaterOrEqual(world.Audio.Observe().Count(animal.Step)-animalSteps,3,"The animal's paws are heard too");
   Assert.AreEqual(animal.StepPitch,pitch,.1f,"Footsteps carry the species' weight (pitch)");
   int growls=world.Audio.Observe().Count(animal.Growl);
   for(int i=0;i<900;i++){var dir=i/150%2==0?90:270;Step(1,(0,new PlayerInput{Forward=1,Yaw=dir}));world.Audio.Advance(.02f);}
   Assert.Greater(world.Audio.Observe().Count(animal.Growl),growls,"A chasing animal growls now and then");
   Assert.Less(world.Audio.Observe().Count(animal.Growl),growls+5,"...but only occasionally");
  }

  [UnityTest] public IEnumerator MusicFollowsPreparationChaseAndTheLastThirtySecondsWithCrossfadesOverVillageAmbience() {
   yield return Create(Faction.Animal,Faction.Human,v=>{v.RoundSeconds=40;v.ResultSeconds=2;});
   world.Step(.02f);
   var view=world.Audio.Observe();
   Assert.AreEqual("music-prepare",view.Music,"Preparation has its own calm music");Assert.AreEqual("ambience",view.Ambience,"The village ambience loops underneath");
   world.Audio.Advance(3);
   foreach(var s in root.GetComponentsInChildren<AudioSource>().Where(s=>s.loop&&s.clip!=null))Assert.AreEqual(0,s.spatialBlend,1e-4,"Music and ambience are not positional: "+s.clip.name);
   Assert.AreEqual(2,root.GetComponentsInChildren<AudioSource>().Count(s=>s.loop&&s.clip!=null&&s.volume>0),"One music track and the ambience sound once settled");
   world.Step(1f);Assert.AreEqual(RoundPhase.Chase,world.Observe().Phase);
   view=world.Audio.Observe();Assert.AreEqual("music-chase",view.Music,"The chase changes the music");Assert.AreEqual("music-prepare",view.FadingMusic,"...by crossfading out the previous track");
   world.Audio.Advance(.5f);var mid=world.Audio.Observe();Assert.Greater(mid.MusicLevel,0);Assert.Less(mid.MusicLevel,1,"The crossfade takes a moment");
   world.Audio.Advance(3);view=world.Audio.Observe();Assert.AreEqual(1,view.MusicLevel,1e-4);Assert.IsNull(view.FadingMusic,"The old track stops after the crossfade");
   for(int i=0;i<10;i++)world.Step(.5f);Assert.Greater(world.Observe().SecondsRemaining,30);Assert.AreEqual("music-chase",world.Audio.Observe().Music);
   for(int i=0;i<12;i++)world.Step(.5f);Assert.LessOrEqual(world.Observe().SecondsRemaining,30);
   Assert.AreEqual("music-final",world.Audio.Observe().Music,"The last thirty seconds raise the tension");
   while(world.Observe().Phase==RoundPhase.Chase)world.Step(.5f);
   Assert.AreEqual("music-prepare",world.Audio.Observe().Music,"Results calm down again");
   Assert.AreEqual("ambience",world.Audio.Observe().Ambience);
  }

  [UnityTest] public IEnumerator TwelvePlayersAtOnceStayUnderTheVoiceCapAndKeepTheImportantSounds() {
   root=new GameObject("voice cap");var audio=root.AddComponent<AudioDirector>();audio.AutomaticUpdate=false;
   for(int slot=0;slot<12;slot++)for(int n=0;n<4;n++)audio.Play(slot%2==0?"step-paw":"step-human",new Vector3(slot,0,n));
   var view=audio.Observe();
   Assert.LessOrEqual(view.Voices.Length,AudioDirector.MaxVoices,"Simultaneous voices are capped");
   Assert.LessOrEqual(view.Voices.Count(v=>v.Cue.StartsWith("step")),AudioDirector.MaxFootstepVoices,"Footsteps alone never fill the mix");
   Assert.Greater(view.Dropped,0,"Excess requests are dropped instead of stacking");
   for(int slot=0;slot<12;slot++){audio.Play("growl-wolf",new Vector3(slot,0,0));audio.Play("swing",new Vector3(slot,0,1));}
   for(int slot=0;slot<12;slot++){audio.Play("hit",new Vector3(slot,0,2));audio.Play("transform",new Vector3(slot,0,3));}
   view=audio.Observe();
   Assert.LessOrEqual(view.Voices.Length,AudioDirector.MaxVoices);
   Assert.GreaterOrEqual(view.Voices.Count(v=>v.Cue=="hit"||v.Cue=="transform"),AudioDirector.MaxVoices/2,"Hits and transformations take voices from lesser sounds");
   Assert.AreEqual(0,view.Voices.Count(v=>v.Cue.StartsWith("step")),"Footsteps give way first");
   Assert.LessOrEqual(root.GetComponentsInChildren<AudioSource>().Count(s=>!s.loop),AudioDirector.MaxVoices,"Sources are pooled, never one per request");
   audio.Advance(5);Assert.AreEqual(0,audio.Observe().Voices.Length,"Voices free up when their sound ends");
   Object.Destroy(root);root=null;
   // A live twelve-player round with every bot moving.
   yield return Create(Faction.Animal,Faction.Human);world.BotAutomationEnabled=true;
   world.Step(.02f);int peak=0;
   for(int i=0;i<400;i++){world.Step(.02f);world.Audio.Advance(.02f);peak=Mathf.Max(peak,world.Audio.Observe().Voices.Length);}
   Assert.Greater(world.Audio.Observe().Count("step-human")+world.Audio.Observe().Count("step-paw"),20,"Twelve runners make plenty of footsteps");
   Assert.LessOrEqual(peak,AudioDirector.MaxVoices,"...without ever exceeding the voice cap");
   Assert.LessOrEqual(world.Audio.GetComponentsInChildren<AudioSource>().Count(s=>!s.loop),AudioDirector.MaxVoices);
  }

  [UnityTest] public IEnumerator MissingSoundFilesFallBackToSynthesisKeepPlayingAndAreReported() {
   root=new GameObject("missing sounds");var audio=root.AddComponent<AudioDirector>();audio.AutomaticUpdate=false;
   audio.UseLoader(_=>null);
   LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("합성음 대체.*snarl"));
   LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("합성음 대체.*music-prepare"));
   Assert.IsTrue(audio.Play("snarl",Vector3.zero),"A missing clip still sounds");
   Assert.IsTrue(audio.Observe().Voices.Single().Synthesized);
   audio.SetPhase(RoundPhase.Preparation,20);
   var view=audio.Observe();Assert.AreEqual("music-prepare",view.Music,"Missing music is synthesized too");
   CollectionAssert.IsSupersetOf(view.Fallbacks,new[]{"snarl","music-prepare","ambience"},"Every substituted cue is reported");
   CollectionAssert.DoesNotContain(view.Fallbacks,"fire","Sounds that are synthesized by design are not a fallback");
   audio.Play("snarl",Vector3.one);Assert.AreEqual(1,audio.Observe().Fallbacks.Count(c=>c=="snarl"),"Each fallback is reported once");
   Object.Destroy(root);root=null;
   // A whole round still plays with every file missing.
   yield return Create(Faction.Animal,Faction.Human);world.Audio.UseLoader(_=>null);world.BotAutomationEnabled=true;
   for(int i=0;i<100;i++){world.Step(.02f);world.Audio.Advance(.02f);}
   Assert.AreEqual(RoundPhase.Chase,world.Observe().Phase,"Play is never blocked by missing sound");
   Assert.Greater(AudioCatalog.Entries.Sum(e=>world.Audio.Observe().Count(e.Cue)),0,"Synthesized sounds play during the round");
   // The committed CC0 files and, where the purchased assets are configured, the owned sounds load without fallback.
   root.AddComponent<AudioDirector>();var real=root.GetComponents<AudioDirector>().Last();real.AutomaticUpdate=false;
   foreach(var entry in AudioCatalog.Entries.Where(e=>e.Origin==SoundOrigin.Cc0))real.Play(entry.Cue,Vector3.zero);
   real.SetPhase(RoundPhase.Chase,100);
   CollectionAssert.IsEmpty(real.Observe().Fallbacks,"Committed CC0 sounds always load");
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   if(catalog!=null&&catalog.Sounds!=null&&catalog.Sounds.Length>0){foreach(var entry in AudioCatalog.Entries.Where(e=>e.Origin==SoundOrigin.Owned))real.Play(entry.Cue,Vector3.zero);CollectionAssert.IsEmpty(real.Observe().Fallbacks,"Configured owned sounds load");}
  }

  [UnityTest] public IEnumerator AnEmptyMagazineClicksOncePerTriggerPull() {
   yield return Create(Faction.Animal,Faction.Human,v=>{v.Magazine=1;});
   world.Step(.02f);world.Step(1f);Spread();Step(5);
   world.PresentTrigger(8,true);world.PresentTrigger(8,false);
   Assert.AreEqual(0,world.Audio.Observe().Count("dry-fire"),"A loaded gun does not click");
   Step(1,(8,new PlayerInput{Attack=true,Yaw=180}));Assert.AreEqual(0,world.Observe().Players[8].Ammo);
   world.PresentTrigger(8,true);world.PresentTrigger(8,true);world.PresentTrigger(8,true);
   Assert.AreEqual(1,world.Audio.Observe().Count("dry-fire"),"Pulling the trigger on an empty magazine clicks once");
   var click=world.Audio.Observe().Voices.Single(v=>v.Cue=="dry-fire");Assert.AreEqual(1,click.SpatialBlend,1e-4);
   world.PresentTrigger(8,false);world.Audio.Advance(.3f);world.PresentTrigger(8,true);
   Assert.AreEqual(2,world.Audio.Observe().Count("dry-fire"),"...and again on the next pull");
   world.PresentTrigger(0,false);world.PresentTrigger(0,true);
   Assert.AreEqual(2,world.Audio.Observe().Count("dry-fire"),"Animals have no gun to click");
  }

  [Test] public void CommittedEffectClipsAreMonoSoTheyPositionCleanlyAndMusicStreams() {
   foreach(var entry in AudioCatalog.Entries.Where(e=>e.Origin==SoundOrigin.Cc0))foreach(var name in entry.Clips) {
    var clip=Resources.Load<AudioClip>(name);Assert.IsNotNull(clip,name);
    bool bed=entry.Cue.StartsWith("music")||entry.Cue=="ambience";
    if(bed)Assert.AreEqual(AudioClipLoadType.Streaming,clip.loadType,"Long loops stream instead of sitting decoded in memory: "+name);
    else Assert.AreEqual(1,clip.channels,"A positional effect is mono: "+name);
   }
  }

  [UnityTest] public IEnumerator RemoteViewersHearTheSamePublicEventsOnceAndNeverOnJoin() {
   var source=new PlaytestSession(123);source.StartSolo("source");source.BeginSettingsEdit(0);var v=source.ObserveSettings().Edit;v.InitialAttackGrace=0;v.AttackWindupSeconds=0;source.UpdateSettingsEdit(0,v);Assert.IsTrue(source.ApplySettingsNow(0));
   source.Advance(20.01);var state=source.Observe();int animal=state.Players.First(p=>p.Faction==Faction.Animal).Slot;int other=state.Players.Last(p=>p.Faction==Faction.Animal).Slot;
   Assert.IsTrue(source.TryStartAttack(animal,state.Round)&&source.ResolveAttackMiss(animal,state.Round),"An earlier swing exists before the join");
   root=new GameObject("remote audio");world=root.AddComponent<UnityPlaytestSession>();world.StartRemote(source.Observe());world.Audio.AutomaticUpdate=false;
   world.ApplyRemoteSnapshot(source.Observe());
   Assert.AreEqual(0,world.Audio.Observe().Count("swing"),"Joining does not replay earlier swings");
   Assert.IsTrue(source.TryStartAttack(other,state.Round)&&source.ResolveAttackMiss(other,state.Round));
   world.ApplyRemoteSnapshot(source.Observe());
   Assert.AreEqual(1,world.Audio.Observe().Count("swing"),"A new swing is heard on the remote view");
   var swing=Voices("swing").Single();Assert.AreEqual(1,swing.SpatialBlend,1e-4);
   var p=source.Observe().Players[other].Position;Assert.Less(Vector3.Distance(swing.Position,new Vector3(p.X,p.Y,p.Z)),1.5f);
   world.ApplyRemoteSnapshot(source.Observe());Assert.AreEqual(1,world.Audio.Observe().Count("swing"),"Repeated snapshots never replay it");
   int human=source.Observe().Players.First(x=>x.Faction==Faction.Human).Slot;
   Assert.IsTrue(source.TryFire(human,state.Round)&&source.TryReload(human,state.Round));
   world.ApplyRemoteSnapshot(source.Observe());Assert.AreEqual(1,world.Audio.Observe().Count("reload"),"A remote reload is heard from the public state");
   yield return null;
  }
 }
}
