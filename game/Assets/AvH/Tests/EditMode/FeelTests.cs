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
   Assert.AreEqual(ActionPhase.Swing,s.Observe().Players[attacker].Action);
   Assert.IsFalse(s.AttackDue(attacker),"A judged swing is consumed");
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
 }
}
