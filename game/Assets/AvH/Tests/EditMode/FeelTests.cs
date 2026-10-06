using System.Linq;
using NUnit.Framework;
namespace AvH.Tests {
 // Public session boundary for the impact rules: attack tell, hit-stop, stun and their public events.
 public class FeelTests {
  internal static PlaytestSession Chase(System.Action<PlaytestValues> tune,int seed=123) {
   var s=new PlaytestSession(seed);s.StartSolo("tester");s.Advance(20);
   s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;v.InitialAttackGrace=0;v.TransformAttackGrace=0;tune?.Invoke(v);s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettingsNow(0));
   s.Advance(2.01);return s;
  }
  internal static void Instant(PlaytestValues v){v.AttackWindupSeconds=0;v.HitStopSeconds=0;v.HitStunSeconds=0;}
  /// <summary>Restores the click-frame rule (all impact values 0) live, as the host panel does.</summary>
  internal static void Instant(PlaytestSession s){s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;Instant(v);s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettingsNow(0));}
  /// <summary>One click-frame swing judged by host physics against this victim.</summary>
  internal static bool Swing(PlaytestSession s,int attacker,int victim,int round)=>s.TryStartAttack(attacker,round)&&s.TryMeleeHit(attacker,victim,round);
  static (int attacker,int victim) Pair(PlaytestSession s) {
   var state=s.Observe();int attacker=state.Players.First(p=>p.Faction==Faction.Animal).Slot;int victim=state.Players.First(p=>p.Faction==Faction.Human).Slot;
   s.RecordWorldPosition(victim,state.Players[attacker].Position);return (attacker,victim);
  }
  static FeelEvent[] Events(PlaytestSession s,FeelEventKind kind)=>s.Observe().Events.Where(e=>e.Kind==kind).ToArray();

  [Test] public void ZeroImpactValuesJudgeOnTheClickStepWithoutFreezeOrTell() {
   var s=Chase(Instant);var (attacker,victim)=Pair(s);int round=s.Observe().Round;
   Assert.IsTrue(s.TryStartAttack(attacker,round));Assert.IsTrue(s.AttackDue(attacker),"No tell: the swing is judged on the click step");
   Assert.IsTrue(s.TryMeleeHit(attacker,victim,round));
   var state=s.Observe();Assert.AreEqual(Faction.Animal,state.Players[victim].Faction);
   Assert.AreEqual(0,state.Players[attacker].HitStopRemaining);Assert.AreEqual(0,state.Players[victim].HitStopRemaining);
   Assert.IsEmpty(Events(s,FeelEventKind.AttackWindup),"No windup event without a windup");
   var hit=Events(s,FeelEventKind.AttackHit).Single();Assert.AreEqual(attacker,hit.Actor);Assert.AreEqual(victim,hit.Target);
   Assert.AreEqual(victim,Events(s,FeelEventKind.Transform).Single().Target);
  }

  [Test] public void WindupDelaysTheJudgementAndPublishesTellThenHit() {
   var s=Chase(v=>v.AttackWindupSeconds=.15f);var (attacker,victim)=Pair(s);int round=s.Observe().Round;
   Assert.IsTrue(s.TryStartAttack(attacker,round));
   var tell=Events(s,FeelEventKind.AttackWindup).Single();Assert.AreEqual(attacker,tell.Actor);
   Assert.AreEqual(ActionPhase.Windup,s.Observe().Players[attacker].Action);
   Assert.IsFalse(s.AttackDue(attacker));Assert.IsFalse(s.TryMeleeHit(attacker,victim,round),"Contact during the tell is not yet a hit");
   Assert.IsFalse(s.TryStartAttack(attacker,round),"One swing at a time");
   s.Advance(.1);Assert.IsFalse(s.AttackDue(attacker));Assert.AreEqual(Faction.Human,s.Observe().Players[victim].Faction);
   s.Advance(.051);Assert.IsTrue(s.AttackDue(attacker));
   Assert.IsTrue(s.TryMeleeHit(attacker,victim,round));
   var hit=Events(s,FeelEventKind.AttackHit).Single();Assert.AreEqual(attacker,hit.Actor);Assert.AreEqual(victim,hit.Target);
   Assert.AreEqual(.15,hit.HostTime-tell.HostTime,.002,"The hit lands one windup after the tell");
   Assert.AreEqual(ActionPhase.HitStop,s.Observe().Players[attacker].Action,"A landed swing first holds for the hit-stop");
   Assert.IsFalse(s.AttackDue(attacker),"A judged swing is consumed");
   s.Advance(.07);Assert.AreEqual(ActionPhase.Swing,s.Observe().Players[attacker].Action,"...then finishes its follow-through");
  }

  [Test] public void MissPublishesSwingAndRecoversBeforeTheNextTell() {
   var s=Chase(v=>v.AttackWindupSeconds=.15f);var state=s.Observe();int attacker=state.Players.First(p=>p.Faction==Faction.Animal).Slot;int round=state.Round;
   Assert.IsTrue(s.TryStartAttack(attacker,round));s.Advance(.16);Assert.IsTrue(s.AttackDue(attacker));
   Assert.IsTrue(s.ResolveAttackMiss(attacker,round));
   Assert.AreEqual(attacker,Events(s,FeelEventKind.AttackMiss).Single().Actor);Assert.IsFalse(s.AttackDue(attacker));
   Assert.IsFalse(s.TryStartAttack(attacker,round),"The miss swing finishes before another tell");
   s.Advance(.26);Assert.IsTrue(s.TryStartAttack(attacker,round));
  }

  [Test] public void GraceStillBlocksTheTellAfterTransformation() {
   var s=Chase(v=>{v.AttackWindupSeconds=0;v.TransformAttackGrace=1;});var (attacker,victim)=Pair(s);int round=s.Observe().Round;
   Assert.IsTrue(s.TryStartAttack(attacker,round)&&s.TryMeleeHit(attacker,victim,round));
   Assert.IsFalse(s.TryStartAttack(victim,round),"A new animal follows the transform attack grace");
   s.Advance(1.01);Assert.IsTrue(s.TryStartAttack(victim,round));
  }

  [Test] public void HitStopFreezesOnlyTheAttackerAndVictimWhileTheMatchContinues() {
   var s=Chase(v=>{Instant(v);v.HitStopSeconds=.06f;});var (attacker,victim)=Pair(s);int round=s.Observe().Round;
   int bystander=s.Observe().Players.First(p=>p.Slot!=attacker&&p.Slot!=victim).Slot;
   Assert.IsTrue(Swing(s,attacker,victim,round));
   var state=s.Observe();double clock=state.SecondsRemaining;
   Assert.AreEqual(.06,state.Players[attacker].HitStopRemaining,1e-6);Assert.AreEqual(.06,state.Players[victim].HitStopRemaining,1e-6);
   Assert.AreEqual(ActionPhase.HitStop,state.Players[attacker].Action);Assert.IsTrue(CharacterMotion.Frozen(state.Players[victim]));
   Assert.AreEqual(0,state.Players[bystander].HitStopRemaining,"Other characters keep playing");Assert.IsFalse(CharacterMotion.Frozen(state.Players[bystander]));
   s.Advance(.03);Assert.AreEqual(clock-.03,s.Observe().SecondsRemaining,1e-6,"The match clock never stops");
   s.Advance(.04);Assert.IsFalse(CharacterMotion.Frozen(s.Observe().Players[attacker]));Assert.AreEqual(ActionPhase.Swing,s.Observe().Players[attacker].Action);
  }

  [Test] public void BubbleStunWeakensOnlyAnimalControlAndZeroMeansNoStun() {
   var s=Chase(v=>v.HitStunSeconds=.2f);var state=s.Observe();int animal=state.Players.First(p=>p.Faction==Faction.Animal).Slot;int human=state.Players.First(p=>p.Faction==Faction.Human).Slot;
   Assert.IsTrue(s.RecordBubbleHit(animal,human));
   var stunned=s.Observe().Players[animal];Assert.AreEqual(.2,stunned.StunRemaining,1e-6);Assert.AreEqual(ActionPhase.Stunned,stunned.Action);
   var stagger=Events(s,FeelEventKind.Stagger).Single();Assert.AreEqual(animal,stagger.Target);Assert.AreEqual(human,stagger.Actor);
   var weak=CharacterMotion.Restrain(new MotionIntent{DirectionX=1,Grounded=true},stunned);
   Assert.Less(weak.DirectionX,.5f,"Control is weakened");Assert.Greater(weak.DirectionX,0,"...but never removed");
   Assert.IsFalse(s.RecordBubbleHit(human,0),"A friendly push is not an impact stun");Assert.AreEqual(0,s.Observe().Players[human].StunRemaining);
   s.Advance(.21);var recovered=s.Observe().Players[animal];Assert.AreEqual(0,recovered.StunRemaining);
   Assert.AreEqual(1,CharacterMotion.Restrain(new MotionIntent{DirectionX=1},recovered).DirectionX);
   var off=Chase(Instant);animal=off.Observe().Players.First(p=>p.Faction==Faction.Animal).Slot;
   Assert.IsTrue(off.RecordBubbleHit(animal,human));Assert.AreEqual(0,off.Observe().Players[animal].StunRemaining,"Zero stun keeps full control");
   Assert.AreEqual(1,CharacterMotion.Restrain(new MotionIntent{DirectionX=1},off.Observe().Players[animal]).DirectionX);
  }

  [Test] public void KnockbackSlidesFurtherInTheAirThanOnTheGround() {
   float gx=8,gy=0,gz=0;CharacterMotion.DecayKnockback(ref gx,ref gy,ref gz,true,.1);
   Assert.AreEqual(6,gx,1e-4,"Ground friction keeps the original 20 m/s² decay");
   float ax=8,ay=0,az=0;CharacterMotion.DecayKnockback(ref ax,ref ay,ref az,false,.1);
   Assert.Greater(ax,gx+.5f,"Less friction in the air");Assert.Less(ax,8);
   float x=.5f,y=.5f,z=0;CharacterMotion.DecayKnockback(ref x,ref y,ref z,true,1);Assert.AreEqual(0,x);Assert.AreEqual(0,y,"Never reverses");
  }

  [Test] public void RoundEndCutsFreezeStunAndPendingTells() {
   var s=Chase(v=>{Instant(v);v.HitStopSeconds=.06f;});var state=s.Observe();int attacker=state.Players.First(p=>p.Faction==Faction.Animal).Slot;
   foreach(var victim in state.Players.Where(p=>p.Faction==Faction.Human).Select(p=>p.Slot)){s.RecordWorldPosition(victim,s.Observe().Players[attacker].Position);Assert.IsTrue(Swing(s,attacker,victim,state.Round));s.Advance(.51);}
   Assert.AreEqual(RoundPhase.Results,s.Observe().Phase);
   Assert.IsTrue(s.Observe().Players.All(p=>p.HitStopRemaining==0&&p.StunRemaining==0&&p.Action==ActionPhase.None));
  }
 }
}
