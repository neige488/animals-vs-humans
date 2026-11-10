using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
namespace AvH.Tests {
 // Public session boundary for special abilities: who may press Q when, what it does to whom, and what it leaves behind.
 public class AbilityTests {
  const string Walker="human-man_casual",Fox="animal-fox";
  static CharacterDefinition Def(string id)=>new CharacterDefinition(id,id,Abilities.Rarity(id));
  /// <summary>A chase where every human plays <paramref name="human"/> and every animal <paramref name="animal"/>, everyone far apart.</summary>
  static PlaytestSession Play(string human,string animal,Action<PlaytestValues> tune=null,int seed=7) {
   var s=new PlaytestSession(seed);s.StartSolo("tester",humans:new[]{Def(human)},animals:new[]{Def(animal)});s.Advance(20);
   s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;v.InitialAttackGrace=0;v.TransformAttackGrace=0;FeelTests.Instant(v);tune?.Invoke(v);s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettingsNow(0));
   s.Advance(2.01);Assert.AreEqual(RoundPhase.Chase,s.Observe().Phase);
   foreach(var p in s.Observe().Players)s.RecordWorldPosition(p.Slot,new WorldPosition(p.Slot*40,0,500));
   s.TakeImpulses();return s;
  }
  static int[] Slots(PlaytestSession s,Faction f)=>s.Observe().Players.Where(p=>p.Faction==f).Select(p=>p.Slot).ToArray();
  static PlayerState P(PlaytestSession s,int slot)=>s.Observe().Players[slot];
  static void At(PlaytestSession s,int slot,float x,float z,float y=0)=>s.RecordWorldPosition(slot,new WorldPosition(x,y,z));
  static bool Use(PlaytestSession s,int slot,float yaw=0,float pitch=0)=>s.TryUseAbility(slot,s.Observe().Round,yaw,pitch);
  static int Hits(PlaytestSession s,AbilityKind kind,int target)=>s.Observe().Events.Count(e=>e.Kind==FeelEventKind.AbilityHit&&e.Target==target&&(AbilityKind)(int)e.Strength==kind);
  static AbilityKind[] Kinds=>Abilities.All.Select(a=>a.Kind).ToArray();

  [Test] public void EveryCharacterHasOneAbilityThatWaitsOrRunsOut() {
   var all=Abilities.All;
   Assert.AreEqual(32,all.Count);Assert.AreEqual(16,all.Count(a=>a.CharacterId.StartsWith("animal-")));Assert.AreEqual(16,all.Count(a=>a.CharacterId.StartsWith("human-")));
   Assert.AreEqual(32,all.Select(a=>a.Kind).Distinct().Count());Assert.AreEqual(32,all.Select(a=>a.CharacterId).Distinct().Count());Assert.AreEqual(32,all.Select(a=>a.Name).Distinct().Count());
   foreach(var a in all){Assert.IsTrue(a.Cooldown>0||a.Uses>0,a.Name+": 쿨타임이나 사용 한도가 있어야 한다");Assert.IsNotEmpty(a.Hint);Assert.Contains(a.Rarity,Abilities.Grades);}
   foreach(string side in new[]{"animal-","human-"}) {
    var mine=all.Where(a=>a.CharacterId.StartsWith(side)).ToArray();
    foreach(string grade in Abilities.Grades)Assert.IsTrue(mine.Any(a=>a.Rarity==grade),side+grade);
    // A showier grade waits longer (or is counted): never a shorter wait than the grade below.
    for(int g=0;g<2;g++) {
     var low=mine.Where(a=>Abilities.Grade(a.Rarity)==g&&a.Uses==0);var high=mine.Where(a=>Abilities.Grade(a.Rarity)==g+1&&a.Uses==0);
     Assert.LessOrEqual(low.Max(a=>a.Cooldown),high.Min(a=>a.Cooldown),side+Abilities.Grades[g]);
    }
    Assert.IsTrue(mine.Where(a=>a.Rarity==Abilities.Legend).All(a=>a.Uses>0),"전설은 횟수 제한");
   }
  }
  [TestCaseSource(nameof(Kinds))] public void WaitsOrRunsOutBeforeTheNextUse(AbilityKind kind) {
   var def=Abilities.Of(kind);bool animal=def.CharacterId.StartsWith("animal-");
   var s=Play(animal?Walker:def.CharacterId,animal?def.CharacterId:Fox);int slot=Slots(s,animal?Faction.Animal:Faction.Human)[0];
   Assert.AreEqual(kind,P(s,slot).Ability);StringAssert.Contains("준비됨",Abilities.Status(P(s,slot)));
   Assert.IsTrue(Use(s,slot));
   Assert.AreEqual(def.Cooldown,P(s,slot).AbilityCooldownRemaining,1e-5);Assert.AreEqual(def.Duration,P(s,slot).AbilityActiveRemaining,1e-5);
   Assert.AreEqual(1,s.Observe().Events.Count(e=>e.Kind==FeelEventKind.Ability&&e.Actor==slot&&(AbilityKind)(int)e.Strength==kind));
   Assert.IsFalse(Use(s,slot),"바로 다시 쓸 수 없다");
   double wait=Math.Max(def.Cooldown,def.Duration);
   s.Advance(wait-.2);Assert.IsFalse(Use(s,slot),"기다림이 끝나기 전");
   s.Advance(.3);int uses=1;
   for(int i=0;i<4;i++){if(Use(s,slot))uses++;s.Advance(wait+.1);}
   Assert.AreEqual(def.Uses>0?def.Uses:5,uses);
   if(def.Uses>0){Assert.AreEqual(0,P(s,slot).AbilityUsesRemaining);StringAssert.Contains("다 썼음",Abilities.Status(P(s,slot)));}
  }
  [Test] public void StatusLineSaysWhatQDoesNow() {
   var s=Play("human-man_clown",Fox);int clown=Slots(s,Faction.Human)[0];
   Assert.AreEqual("Q 풍선 [에픽] · 준비됨",Abilities.Status(P(s,clown)));
   Use(s,clown);StringAssert.Contains("사용 중 6.0초",Abilities.Status(P(s,clown)));
   s.Advance(6.5);StringAssert.Contains("초 뒤",Abilities.Status(P(s,clown)));
   var chef=Play("human-man_chef",Fox);int cook=Slots(chef,Faction.Human)[0];StringAssert.Contains("남은 3번",Abilities.Status(P(chef,cook)));
   Assert.AreEqual("",Abilities.Status(new PlayerState()));
  }
  [Test] public void OnlyInTheChaseAndOnlyWhenSwitchedOn() {
   var waiting=new PlaytestSession(3);waiting.StartSolo("tester",humans:new[]{Def(Walker)},animals:new[]{Def(Fox)});
   Assert.IsFalse(waiting.TryUseAbility(0,1,0,0),"준비 시간");
   var off=Play(Walker,Fox,v=>v.Abilities=false);Assert.IsFalse(Use(off,Slots(off,Faction.Human)[0]));
   var s=Play(Walker,Fox);int slot=Slots(s,Faction.Human)[0];
   Assert.IsFalse(s.TryUseAbility(slot,s.Observe().Round+1,0,0),"다른 라운드");Assert.IsFalse(s.TryUseAbility(slot,s.Observe().Round,float.NaN,0));Assert.IsFalse(s.TryUseAbility(99,s.Observe().Round,0,0));
   Assert.IsTrue(Use(s,slot));
  }
  [Test] public void RoarStopsNearbyHumansAndAHelmetTakesOne() {
   var s=Play("human-man_construction_worker","animal-bear_grizzly");int bear=Slots(s,Faction.Animal)[0];var h=Slots(s,Faction.Human);
   At(s,bear,0,0);At(s,h[0],0,3);At(s,h[1],3,0);At(s,h[2],0,30);
   Assert.IsTrue(Use(s,h[0]));Assert.IsTrue(Use(s,bear));
   Assert.AreEqual(0,P(s,h[0]).RootRemaining,"안전모가 막는다");Assert.AreEqual(0,P(s,h[0]).AbilityActiveRemaining,"안전모는 한 번 쓰면 끝");Assert.AreEqual(1,Hits(s,AbilityKind.Helmet,h[0]));
   Assert.AreEqual(1,P(s,h[1]).RootRemaining,1e-6);Assert.AreEqual(AbilityKind.Roar,P(s,h[1]).Hold);Assert.AreEqual(0,CharacterMotion.Control(P(s,h[1])));Assert.AreEqual(1,Hits(s,AbilityKind.Roar,h[1]));
   Assert.AreEqual(0,P(s,h[2]).RootRemaining,"멀리 있으면 안 닿는다");
   Assert.IsFalse(Use(s,h[1]),"멈춘 동안에는 능력을 못 쓴다");
   s.Advance(1.05);Assert.AreEqual(0,P(s,h[1]).RootRemaining);Assert.AreEqual(AbilityKind.None,P(s,h[1]).Hold);Assert.AreEqual(1,CharacterMotion.Control(P(s,h[1])));Assert.IsTrue(Use(s,h[1]));
  }
  [TestCase("human-man_cowboy",AbilityKind.Lasso,2.0,false)]
  [TestCase("human-woman_scientist",AbilityKind.IceRay,2.5,false)]
  [TestCase("human-woman_soldier",AbilityKind.BigBubble,2.0,true)]
  public void AimedHoldCatchesTheAnimalInTheCrosshairOnly(string human,AbilityKind kind,double seconds,bool lifts) {
   var s=Play(human,Fox);var a=Slots(s,Faction.Animal);var h=Slots(s,Faction.Human);
   At(s,h[0],0,0);At(s,h[1],100,0);At(s,a[0],0,6);At(s,a[1],100,-6);
   Assert.IsTrue(Use(s,h[1],0),"빗나가도 쓴 것이다");Assert.AreEqual(0,P(s,a[1]).RootRemaining+P(s,a[1]).LiftRemaining,"등 뒤는 못 맞힌다");Assert.Greater(P(s,h[1]).AbilityCooldownRemaining,0);
   Assert.IsTrue(Use(s,h[0],0));var caught=P(s,a[0]);
   Assert.AreEqual(seconds,lifts?caught.LiftRemaining:caught.RootRemaining,1e-6);Assert.AreEqual(kind,caught.Hold);Assert.AreEqual(1,Hits(s,kind,a[0]));
   Assert.AreEqual(0,CharacterMotion.Control(caught));Assert.AreEqual(0,CharacterMotion.JumpHeight(new PlaytestValues(),caught));
   if(lifts)Assert.Greater(CharacterMotion.Air(caught).Rise,0,"비눗방울은 띄운다");
   At(s,h[0],0,5.5f);Assert.IsFalse(s.TryStartAttack(a[0],s.Observe().Round)&&s.TryMeleeHit(a[0],h[0],s.Observe().Round),"잡힌 동안에는 공격하지 못한다");
   s.Advance(seconds+.05);Assert.AreEqual(AbilityKind.None,P(s,a[0]).Hold);Assert.AreEqual(1,CharacterMotion.Control(P(s,a[0])));
  }
  [Test] public void RhinoInFullChargeShrugsEverythingOff() {
   var s=Play("human-man_cowboy","animal-rhino");var a=Slots(s,Faction.Animal);var h=Slots(s,Faction.Human);
   At(s,a[0],0,0);At(s,h[0],0,-6);At(s,h[1],2,-6);
   Assert.IsTrue(Use(s,a[0],90));Assert.IsTrue(CharacterMotion.Locked(P(s,a[0])));
   Assert.IsTrue(Use(s,h[0],0));Assert.AreEqual(0,P(s,a[0]).RootRemaining);Assert.IsFalse(s.RecordBubbleHit(a[0],h[0]));
   s.Advance(2.05);Assert.IsFalse(CharacterMotion.Locked(P(s,a[0])));
   Assert.IsTrue(Use(s,h[1],-18));Assert.AreEqual(2,P(s,a[0]).RootRemaining,1e-6);
  }
  [Test] public void ChargeShovesEachHumanOncePerRun() {
   var s=Play(Walker,"animal-boar");int boar=Slots(s,Faction.Animal)[0],human=Slots(s,Faction.Human)[0];
   At(s,boar,0,0);At(s,human,0,1);Assert.IsTrue(Use(s,boar));
   s.Advance(.05);var first=s.TakeImpulses();Assert.AreEqual(1,first.Count(i=>i.Slot==human));Assert.Greater(first.First(i=>i.Slot==human).Z,0,"돌진하는 쪽에서 멀어지게");
   s.Advance(.05);Assert.AreEqual(0,s.TakeImpulses().Count(i=>i.Slot==human));Assert.AreEqual(1,Hits(s,AbilityKind.Charge,human));
  }
  [Test] public void HeadbuttHitsOnlyWhatIsInFront() {
   var s=Play(Walker,"animal-goat");int goat=Slots(s,Faction.Animal)[0];var h=Slots(s,Faction.Human);
   At(s,goat,0,0);At(s,h[0],0,2);At(s,h[1],0,-2);Assert.IsTrue(Use(s,goat,0));
   var shoved=s.TakeImpulses();Assert.AreEqual(1,shoved.Count(i=>i.Slot==h[0]));Assert.AreEqual(0,shoved.Count(i=>i.Slot==h[1]));
   Assert.Greater(P(s,h[0]).SlowRemaining,0);Assert.AreEqual(CharacterMotion.SlowedControl,CharacterMotion.Control(P(s,h[0])));Assert.AreEqual(0,P(s,h[1]).SlowRemaining);
  }
  [Test] public void SlamPushesEveryoneAroundAndShakesTheirView() {
   var s=Play(Walker,"animal-gorilla_silver");int ape=Slots(s,Faction.Animal)[0];var h=Slots(s,Faction.Human);
   At(s,ape,0,0);At(s,h[0],0,3);At(s,h[1],-3,0);At(s,h[2],0,9);Assert.IsTrue(Use(s,ape));
   var shoved=s.TakeImpulses();Assert.IsTrue(shoved.Any(i=>i.Slot==h[0]&&i.Z>0&&i.Y>0));Assert.IsTrue(shoved.Any(i=>i.Slot==h[1]&&i.X<0));Assert.IsFalse(shoved.Any(i=>i.Slot==h[2]));
   Assert.AreEqual(2,P(s,h[0]).ShakeRemaining,1e-6);Assert.AreEqual(0,P(s,h[2]).ShakeRemaining);
  }
  [Test] public void AcornShakesTheHumanItHits() {
   var s=Play(Walker,"animal-squirrel");int squirrel=Slots(s,Faction.Animal)[0];var h=Slots(s,Faction.Human);
   At(s,squirrel,0,0);At(s,h[0],0,10);At(s,h[1],0,-10);Assert.IsTrue(Use(s,squirrel,0));
   Assert.AreEqual(1.5,P(s,h[0]).ShakeRemaining,1e-6);Assert.AreEqual(0,P(s,h[1]).ShakeRemaining);Assert.AreEqual(Faction.Human,P(s,h[0]).Faction,"도토리로는 잡지 못한다");
  }
  [Test] public void JumpsLeaveAsImpulsesForThePhysicsBody() {
   foreach(var (animal,up,forward) in new[]{("animal-rabbit_brown",8.5f,0f),("animal-hen",7.5f,0f),("animal-deer",8f,10f),("animal-eagle",11f,5f)}) {
    var s=Play(Walker,animal);int slot=Slots(s,Faction.Animal)[0];Assert.IsTrue(Use(s,slot,90));
    var kick=s.TakeImpulses().Single(i=>i.Slot==slot);Assert.IsTrue(kick.SetVertical);Assert.AreEqual(up,kick.Vertical);Assert.AreEqual(forward,kick.X,1e-3,animal);Assert.AreEqual(0,kick.Z,1e-3);
    Assert.IsEmpty(s.TakeImpulses(),"한 번 꺼내면 사라진다");
   }
   var hook=Play("human-woman_explorer",Fox);int explorer=Slots(hook,Faction.Human)[0];Assert.IsTrue(Use(hook,explorer,0,-30));
   var pull=hook.TakeImpulses().Single(i=>i.Slot==explorer);Assert.AreEqual(13,pull.Z,1e-3);Assert.AreEqual(8.5,pull.Vertical,1e-3,"위를 볼수록 높이 날아간다");
  }
  [Test] public void SlowFallersDriftDownAndTheCatJumpsHigher() {
   var rules=new PlaytestValues();
   foreach(string id in new[]{"animal-hen","human-woman_casual"}) {
    bool animal=id.StartsWith("animal-");var s=Play(animal?Walker:id,animal?id:Fox);int slot=Slots(s,animal?Faction.Animal:Faction.Human)[0];
    Assert.AreEqual(0,CharacterMotion.Air(P(s,slot)).FallCap);Use(s,slot);Assert.AreEqual(2.2f,CharacterMotion.Air(P(s,slot)).FallCap);
    Assert.IsFalse(s.RecordTouchdown(slot),"막 뛰어오른 순간의 접지는 무시");s.Advance(.4);Assert.IsTrue(s.RecordTouchdown(slot));Assert.AreEqual(0,CharacterMotion.Air(P(s,slot)).FallCap,"땅에 닿으면 끝");
   }
   var cat=Play(Walker,"animal-cat_orange");int c=Slots(cat,Faction.Animal)[0];float normal=CharacterMotion.JumpHeight(rules,P(cat,c));Use(cat,c);
   Assert.AreEqual(normal*1.5f,CharacterMotion.JumpHeight(rules,P(cat,c)),1e-4);cat.Advance(4.05);Assert.AreEqual(normal,CharacterMotion.JumpHeight(rules,P(cat,c)),1e-4);
  }
  [Test] public void EagleDiveEndsInAShockwaveOnTouchdown() {
   var s=Play(Walker,"animal-eagle");int eagle=Slots(s,Faction.Animal)[0];var h=Slots(s,Faction.Human);
   At(s,eagle,0,0);At(s,h[0],0,3);At(s,h[1],0,12);Use(s,eagle);s.TakeImpulses();
   Assert.IsFalse(s.RecordTouchdown(eagle));s.Advance(.5);Assert.IsTrue(s.RecordTouchdown(eagle));
   var shoved=s.TakeImpulses();Assert.IsTrue(shoved.Any(i=>i.Slot==h[0]));Assert.IsFalse(shoved.Any(i=>i.Slot==h[1]));
   Assert.AreEqual(1.5,P(s,h[0]).SlowRemaining,1e-6);Assert.AreEqual(0,P(s,eagle).AbilityActiveRemaining);Assert.IsFalse(s.RecordTouchdown(eagle));
  }
  [Test] public void TigerHuntSkipsTheWindupAndRunsFaster() {
   var s=Play(Walker,"animal-tiger",v=>v.AttackWindupSeconds=.4f);var a=Slots(s,Faction.Animal);int round=s.Observe().Round;
   float normal=CharacterMotion.Profile(new PlaytestValues(),P(s,a[0])).MaxSpeed;
   Assert.IsTrue(s.TryStartAttack(a[1],round));Assert.IsFalse(s.AttackDue(a[1]),"보통은 준비 동작이 있다");
   Assert.IsTrue(Use(s,a[0]));Assert.IsTrue(s.TryStartAttack(a[0],round));Assert.IsTrue(s.AttackDue(a[0]));
   Assert.AreEqual(normal*1.35f,CharacterMotion.Profile(new PlaytestValues(),P(s,a[0])).MaxSpeed,1e-3);
  }
  [Test] public void CrocodileBiteSlowsTheHumansAroundItsCatch() {
   var s=Play(Walker,"animal-crocodile");int croc=Slots(s,Faction.Animal)[0];var h=Slots(s,Faction.Human);
   At(s,croc,0,0);At(s,h[0],0,.5f);At(s,h[1],0,4);At(s,h[2],0,20);Assert.IsTrue(Use(s,croc));
   Assert.IsTrue(FeelTests.Swing(s,croc,h[0],s.Observe().Round));Assert.AreEqual(Faction.Animal,P(s,h[0]).Faction);
   Assert.AreEqual(3,P(s,h[1]).SlowRemaining,1e-6);Assert.AreEqual(0,P(s,h[2]).SlowRemaining);Assert.AreEqual(0,P(s,croc).AbilityActiveRemaining,"한 번 물면 끝");
  }
  [Test] public void FoxDisguiseEndsWhenItMovesOrAttacks() {
   var s=Play(Walker,Fox);var a=Slots(s,Faction.Animal);At(s,a[0],0,0);At(s,a[1],50,0);Use(s,a[0]);Use(s,a[1]);
   s.Advance(1);Assert.Greater(P(s,a[0]).AbilityActiveRemaining,0,"가만히 있으면 유지");
   At(s,a[0],1,0);s.Advance(.05);Assert.AreEqual(0,P(s,a[0]).AbilityActiveRemaining);
   Assert.IsTrue(s.TryStartAttack(a[1],s.Observe().Round));Assert.AreEqual(0,P(s,a[1]).AbilityActiveRemaining);
  }
  [Test] public void VaccineBlocksOneInfectionThenWearsOff() {
   var s=Play("human-woman_doctor",Fox);int fox=Slots(s,Faction.Animal)[0],doctor=Slots(s,Faction.Human)[0],round=s.Observe().Round;
   At(s,fox,0,0);At(s,doctor,0,.5f);Assert.IsTrue(Use(s,doctor));
   Assert.IsTrue(FeelTests.Swing(s,fox,doctor,round));Assert.AreEqual(Faction.Human,P(s,doctor).Faction);Assert.AreEqual(2,P(s,doctor).InvulnerableRemaining,1e-6);
   Assert.IsTrue(s.TakeImpulses().Any(i=>i.Slot==fox),"막힌 동물은 튕겨 나간다");Assert.AreEqual(1,Hits(s,AbilityKind.Vaccine,fox));
   s.Advance(1.3);FeelTests.Swing(s,fox,doctor,round);Assert.AreEqual(Faction.Human,P(s,doctor).Faction,"잠깐의 무적");
   s.Advance(1.3);Assert.IsTrue(FeelTests.Swing(s,fox,doctor,round));Assert.AreEqual(Faction.Animal,P(s,doctor).Faction);
  }
  [Test] public void TurningIntoAnAnimalSwapsTheAbilityAndClearsEffects() {
   var s=Play("human-man_construction_worker","animal-goat");int goat=Slots(s,Faction.Animal)[0],human=Slots(s,Faction.Human)[0];
   At(s,goat,0,0);At(s,human,0,.5f);Use(s,human);Assert.IsTrue(FeelTests.Swing(s,goat,human,s.Observe().Round));
   var turned=P(s,human);Assert.AreEqual(AbilityKind.Headbutt,turned.Ability);Assert.AreEqual(0,turned.AbilityActiveRemaining);Assert.AreEqual(0,turned.AbilityCooldownRemaining);
   s.Advance(.6);Assert.IsTrue(Use(s,human),"새 동물의 능력을 바로 쓸 수 있다");
  }
  [Test] public void KickboardRidesStraightAndFallsOverAtAWall() {
   var s=Play(Walker,Fox);int rider=Slots(s,Faction.Human)[0];float normal=CharacterMotion.Profile(new PlaytestValues(),P(s,rider)).MaxSpeed;
   Assert.IsFalse(s.RecordCrash(rider));Assert.IsTrue(Use(s,rider,45));
   Assert.IsTrue(CharacterMotion.Locked(P(s,rider)));Assert.AreEqual(45,P(s,rider).AbilityYaw);Assert.AreEqual(normal*1.7f,CharacterMotion.Profile(new PlaytestValues(),P(s,rider)).MaxSpeed,1e-3);
   Assert.IsTrue(s.RecordCrash(rider));Assert.AreEqual(1,P(s,rider).RootRemaining,1e-6);Assert.AreEqual(AbilityKind.Kickboard,P(s,rider).Hold);Assert.IsFalse(CharacterMotion.Locked(P(s,rider)));Assert.IsFalse(s.RecordCrash(rider));
  }
  [Test] public void BalloonFloatsUnarmedAndPopsWhenAnAnimalTouchesIt() {
   var s=Play("human-man_clown",Fox);int clown=Slots(s,Faction.Human)[0],fox=Slots(s,Faction.Animal)[0],round=s.Observe().Round;
   At(s,clown,0,0);Assert.IsTrue(Use(s,clown));Assert.Greater(CharacterMotion.Air(P(s,clown)).Rise,0);Assert.IsFalse(s.TryFire(clown,round),"풍선을 잡은 손으로는 못 쏜다");
   s.Advance(3.1);Assert.AreEqual(0,CharacterMotion.Air(P(s,clown)).Rise);Assert.Greater(CharacterMotion.Air(P(s,clown)).FallCap,0,"다 오르면 천천히 내려온다");
   At(s,fox,0,1);s.Advance(.05);Assert.AreEqual(0,P(s,clown).AbilityActiveRemaining);Assert.AreEqual(1,Hits(s,AbilityKind.Balloon,clown));Assert.IsTrue(s.TryFire(clown,round));
  }
  [Test] public void WhistleSlowsAnimalsWithinEarshot() {
   var s=Play("human-woman_police",Fox);var a=Slots(s,Faction.Animal);int officer=Slots(s,Faction.Human)[0];
   At(s,officer,0,0);At(s,a[0],0,4);At(s,a[1],0,8);Assert.IsTrue(Use(s,officer));
   Assert.AreEqual(2,P(s,a[0]).SlowRemaining,1e-6);Assert.AreEqual(0,P(s,a[1]).SlowRemaining);
   Assert.AreEqual(CharacterMotion.SlowedControl,CharacterMotion.Control(P(s,a[0])));Assert.AreEqual(1,CharacterMotion.Control(P(s,a[1])));
  }
  [Test] public void WaterCannonKeepsPushingTheAnimalInItsStream() {
   var s=Play("human-man_fire",Fox);var a=Slots(s,Faction.Animal);int fire=Slots(s,Faction.Human)[0];
   At(s,fire,0,0);At(s,a[0],0,4);At(s,a[1],0,-4);Assert.IsTrue(Use(s,fire,0));
   s.Advance(.1);var first=s.TakeImpulses();Assert.IsTrue(first.Any(i=>i.Slot==a[0]&&i.Z>0));Assert.IsFalse(first.Any(i=>i.Slot==a[1]),"등 뒤는 젖지 않는다");
   s.RecordAim(fire,180,0);s.Advance(.1);var turned=s.TakeImpulses();Assert.IsTrue(turned.Any(i=>i.Slot==a[1]&&i.Z<0),"물줄기는 시선을 따라간다");
   s.Advance(2);s.TakeImpulses();s.Advance(.1);Assert.IsEmpty(s.TakeImpulses(),"2초 뒤에는 물이 끊긴다");
  }
  [Test] public void BananaPeelTripsTheAnimalThatStepsOnIt() {
   var s=Play("human-man_chef",Fox);int chef=Slots(s,Faction.Human)[0],fox=Slots(s,Faction.Animal)[0];
   At(s,chef,0,0);Assert.IsTrue(Use(s,chef));At(s,chef,0,30);s.Advance(.1);
   var peel=s.Observe().Gadgets.Single();Assert.AreEqual(AbilityKind.Banana,peel.Kind);Assert.AreEqual(chef,peel.Owner);Assert.AreEqual(0,peel.Position.Z);
   At(s,fox,.5f,0);s.Advance(.05);
   Assert.AreEqual(1,P(s,fox).RootRemaining,.06);Assert.AreEqual(AbilityKind.Banana,P(s,fox).Hold);Assert.IsEmpty(s.Observe().Gadgets,"밟힌 껍질은 사라진다");Assert.AreEqual(1,Hits(s,AbilityKind.Banana,fox));
  }
  [Test] public void SpringPadBouncesWhoeverStepsOnIt() {
   var s=Play("human-woman_mechanic",Fox);int mechanic=Slots(s,Faction.Human)[0],fox=Slots(s,Faction.Animal)[0];
   At(s,mechanic,0,0);Assert.IsTrue(Use(s,mechanic));s.Advance(.05);
   Assert.AreEqual(13,s.TakeImpulses().Single(i=>i.Slot==mechanic).Vertical,"놓은 사람부터 튕긴다");
   s.Advance(.5);Assert.IsEmpty(s.TakeImpulses(),"한 사람은 1초에 한 번");
   At(s,mechanic,0,30);At(s,fox,0,.5f);s.Advance(.05);Assert.IsTrue(s.TakeImpulses().Single(i=>i.Slot==fox).SetVertical,"동물도 튕긴다");
   s.Advance(45);Assert.IsEmpty(s.Observe().Gadgets);
  }
  [Test] public void ScarecrowStandsAloneAndFallsAfterAWhile() {
   var s=Play("human-man_farm",Fox);int farmer=Slots(s,Faction.Human)[0];
   At(s,farmer,0,0);Use(s,farmer);s.Advance(7.5);Assert.AreEqual(1,s.Observe().Gadgets.Length);s.Advance(.6);Assert.IsEmpty(s.Observe().Gadgets);
   var two=Play("human-man_farm",Fox);var h=Slots(two,Faction.Human);Use(two,h[0]);Use(two,h[1]);
   Assert.AreEqual(2,two.Observe().Gadgets.Length);Assert.AreEqual(2,two.Observe().Gadgets.Select(g=>g.Id).Distinct().Count());
  }
  [Test] public void SmokeHidesItsHumanFromAnimalsThatAreNotAdjacent() {
   var s=Play("human-man_ninja",Fox);int ninja=Slots(s,Faction.Human)[0],fox=Slots(s,Faction.Animal)[0];var h=Slots(s,Faction.Human);
   At(s,ninja,0,0);At(s,h[1],0,20);At(s,fox,0,8);Assert.IsTrue(Use(s,ninja));var state=s.Observe();
   Assert.IsTrue(PlaytestSession.Smoked(state,state.Players[ninja],state.Players[fox]));Assert.IsFalse(PlaytestSession.Smoked(state,state.Players[h[1]],state.Players[fox]),"연기 밖");
   At(s,fox,0,2);state=s.Observe();Assert.IsFalse(PlaytestSession.Smoked(state,state.Players[ninja],state.Players[fox]),"코앞이면 보인다");
   At(s,fox,0,8);s.Advance(5.1);state=s.Observe();Assert.IsFalse(PlaytestSession.Smoked(state,state.Players[ninja],state.Players[fox]),"연기가 걷힌 뒤");
  }
  [Test] public void ZeroGravityLiftsAnimalsAndLightensEveryoneInside() {
   var s=Play("human-man_astronaut",Fox);int pilot=Slots(s,Faction.Human)[0];var a=Slots(s,Faction.Animal);
   At(s,pilot,0,0);At(s,a[0],0,3);At(s,a[1],0,12);Assert.IsTrue(Use(s,pilot));
   var kicks=s.TakeImpulses();Assert.AreEqual(7,kicks.Single(i=>i.Slot==a[0]).Vertical);Assert.IsFalse(kicks.Any(i=>i.Slot==a[1]));
   s.Advance(.05);Assert.AreEqual(.35f,CharacterMotion.Air(P(s,a[0])).Gravity);Assert.AreEqual(.35f,CharacterMotion.Air(P(s,pilot)).Gravity);Assert.AreEqual(1,CharacterMotion.Air(P(s,a[1])).Gravity);
   s.Advance(6.3);Assert.AreEqual(1,CharacterMotion.Air(P(s,a[0])).Gravity);Assert.IsEmpty(s.Observe().Gadgets);
  }
  [Test] public void ANewRoundClearsGadgetsAndEffects() {
   var s=Play("human-man_farm","animal-bear_grizzly",v=>v.RoundSeconds=30);int farmer=Slots(s,Faction.Human)[0],bear=Slots(s,Faction.Animal)[0];
   At(s,bear,0,0);At(s,farmer,0,3);
   int round=s.Observe().Round;while(s.Observe().Round==round){if(s.Observe().Phase==RoundPhase.Chase){Use(s,farmer);Use(s,bear);}s.Advance(1);}
   var state=s.Observe();Assert.IsEmpty(state.Gadgets);
   foreach(var p in state.Players){Assert.AreEqual(0,p.AbilityCooldownRemaining);Assert.AreEqual(0,p.RootRemaining);Assert.AreEqual(Abilities.For(p.CharacterId).Kind,p.Ability);}
  }
  [Test] public void RandomDrawFollowsTheGradeOdds() {
   var humans=Abilities.All.Where(a=>a.CharacterId.StartsWith("human-")).Select(a=>Def(a.CharacterId)).ToArray();
   var animals=Abilities.All.Where(a=>a.CharacterId.StartsWith("animal-")).Select(a=>Def(a.CharacterId)).ToArray();
   int[] human=new int[4],animal=new int[4];bool before=PlaytestValues.DefaultRandomCharacters;PlaytestValues.DefaultRandomCharacters=true;
   try {
    for(int n=0;n<600;n++) {int seed=n*7919+11;
     var s=new PlaytestSession(seed);s.StartSolo("tester",humans:humans,animals:animals);
     var drawn=s.Observe().Players;foreach(var p in drawn){Assert.AreEqual(Abilities.Rarity(p.CharacterId),p.CharacterRarity);human[Abilities.Grade(p.CharacterRarity)]++;}
     foreach(var grade in drawn.GroupBy(p=>p.CharacterRarity)){int size=humans.Count(c=>c.Rarity==grade.Key);Assert.AreEqual(Math.Min(size,grade.Count()),grade.Select(p=>p.CharacterId).Distinct().Count(),"같은 등급 안에서는 안 겹치게");}
     s.Advance(20);foreach(var p in s.Observe().Players.Where(p=>p.Faction==Faction.Animal))animal[Abilities.Grade(p.CharacterRarity)]++;
    }
   } finally {PlaytestValues.DefaultRandomCharacters=before;}
   for(int g=0;g<4;g++) {
    Assert.AreEqual(Abilities.Odds[g],100.0*human[g]/human.Sum(),3,"사람 "+Abilities.Grades[g]);
    Assert.AreEqual(Abilities.Odds[g],100.0*animal[g]/animal.Sum(),5,"동물 "+Abilities.Grades[g]);
   }
   TestContext.WriteLine($"human {string.Join("/",human)} animal {string.Join("/",animal)}");
  }
  [Test] public void AbilityStateAndGadgetsCrossTheWire() {
   var s=Play("human-man_farm","animal-bear_grizzly");int farmer=Slots(s,Faction.Human)[0],bear=Slots(s,Faction.Animal)[0];
   At(s,bear,0,0);At(s,farmer,0,3);Use(s,farmer);Use(s,bear);s.Advance(.25);var sent=s.Observe();
   SessionState got;using(var m=new MemoryStream(RoomWire.Encode(sent)))using(var r=new BinaryReader(m))got=RoomWire.Read<SessionState>(r);
   Assert.AreEqual(AbilityKind.Scarecrow,got.Players[farmer].Ability);Assert.AreEqual(sent.Players[farmer].AbilityCooldownRemaining,got.Players[farmer].AbilityCooldownRemaining);
   Assert.AreEqual(.75,got.Players[farmer].RootRemaining,1e-6);Assert.AreEqual(AbilityKind.Roar,got.Players[farmer].Hold);
   Assert.AreEqual(30-.25,got.Players[bear].AbilityCooldownRemaining,1e-5);
   var decoy=got.Gadgets.Single();Assert.AreEqual(AbilityKind.Scarecrow,decoy.Kind);Assert.AreEqual(farmer,decoy.Owner);Assert.AreEqual(3,decoy.Position.Z);Assert.AreEqual(7.75,decoy.Remaining,1e-6);
   Assert.IsTrue(got.Events.Any(e=>e.Kind==FeelEventKind.Ability&&(AbilityKind)(int)e.Strength==AbilityKind.Roar));
   var input=new NetworkInput{Ability=true,Yaw=12};NetworkInput back;using(var m=new MemoryStream(RoomWire.Encode(input)))using(var r=new BinaryReader(m))back=RoomWire.Read<NetworkInput>(r);
   Assert.IsTrue(back.Ability);
  }
 }
}
