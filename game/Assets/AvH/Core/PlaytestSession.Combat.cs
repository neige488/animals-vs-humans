using System;
using System.Collections.Generic;
using System.Linq;
namespace AvH {
 public static class CombatRules {
  public const float MeleeDistance=1.8f;
  /// <summary>Presented swing after a judged attack. After a miss it is also the recovery before the next tell (only while a tell is configured).</summary>
  public const double SwingSeconds=.25;
  /// <summary>Public events stay observable this long so late snapshots still carry them.</summary>
  public const double EventWindowSeconds=1.5;
  public const int EventLimit=64;
 }
 public sealed partial class PlaytestSession {
  double birthSecondsRemaining, birthBatchAge;
  readonly List<FeelEvent> events=new List<FeelEvent>();
  readonly HashSet<int> dueAttacks=new HashSet<int>();
  int nextEventId;
  void ResetCombat() {
   birthSecondsRemaining=0;birthBatchAge=double.MaxValue;events.Clear();propKnocks.Clear();ClearActions();
   foreach(var p in players){p.Ammo=settings.Current.Magazine;p.ReloadRemaining=0;p.AttackGraceRemaining=0;p.FireCooldownRemaining=0;}
  }
  // Round end cuts every tell, freeze and stun in progress.
  void ClearActions(){dueAttacks.Clear();foreach(var p in players){p.AttackWindupRemaining=0;p.SwingRemaining=0;p.HitStopRemaining=0;p.StunRemaining=0;}}
  static ActionPhase ActionOf(PlayerState p)=>p.HitStopRemaining>0?ActionPhase.HitStop:p.StunRemaining>0?ActionPhase.Stunned:p.AttackWindupRemaining>0?ActionPhase.Windup:p.SwingRemaining>0?ActionPhase.Swing:ActionPhase.None;
  void Publish(FeelEventKind kind,int actor,int target,WorldPosition position,float strength=1) {
   events.Add(new FeelEvent{Id=++nextEventId,Round=round,Kind=kind,Actor=actor,Target=target,Position=position,HostTime=hostTime,Strength=strength});
   if(events.Count>CombatRules.EventLimit)events.RemoveRange(0,events.Count-CombatRules.EventLimit);
  }
  void AdvanceCombat(double elapsed) {
   birthSecondsRemaining=Math.Max(0,birthSecondsRemaining-elapsed);birthBatchAge+=elapsed;
   double now=hostTime+elapsed;events.RemoveAll(e=>now-e.HostTime>CombatRules.EventWindowSeconds);
   foreach(var p in players) {
    p.FireCooldownRemaining=Math.Max(0,p.FireCooldownRemaining-elapsed);
    p.AttackGraceRemaining=Math.Max(0,p.AttackGraceRemaining-elapsed);
    p.SwingRemaining=Math.Max(0,p.SwingRemaining-elapsed);
    // A tell pauses while its character is frozen: the strike moment moves back by the freeze.
    double frozen=Math.Min(elapsed,p.HitStopRemaining);
    p.HitStopRemaining=Math.Max(0,p.HitStopRemaining-elapsed);p.StunRemaining=Math.Max(0,p.StunRemaining-elapsed);
    if(p.AttackWindupRemaining>0){p.AttackWindupRemaining=Math.Max(0,p.AttackWindupRemaining-(elapsed-frozen));if(p.AttackWindupRemaining==0)dueAttacks.Add(p.Slot);}
    if(p.ReloadRemaining>0){p.ReloadRemaining=Math.Max(0,p.ReloadRemaining-elapsed);if(p.ReloadRemaining==0)p.Ammo=settings.Current.Magazine;}
   }
  }
  bool CanAct(int slot,int roundId)=>slot>=0 && slot<players.Length && roundId==round && phase!=RoundPhase.Results;
  public bool TryFire(int slot,int roundId) {
   if(!CanAct(slot,roundId))return false;var p=players[slot];
   if(p.Faction!=Faction.Human || p.Ammo<=0 || p.ReloadRemaining>0 || p.FireCooldownRemaining>0)return false;
   p.Ammo--;p.FireCooldownRemaining=settings.Current.FireInterval;return true;
  }
  public bool TryReload(int slot,int roundId) {
   if(!CanAct(slot,roundId))return false;var p=players[slot];
   if(p.Faction!=Faction.Human || p.Ammo==settings.Current.Magazine || p.ReloadRemaining>0)return false;
   p.ReloadRemaining=settings.Current.ReloadSeconds;return true;
  }
  void PublishBirth(PlayerState[] changed) {
   var grouped=(birthBatchAge<=.5?births:new BirthNotice[0]).ToList();
   foreach(var player in changed){var notice=grouped.FirstOrDefault(b=>b.Kind==player.CharacterName&&b.Rarity==player.CharacterRarity);if(notice==null){notice=new BirthNotice{Kind=player.CharacterName,Rarity=player.CharacterRarity};grouped.Add(notice);}notice.Count++;}
   births=grouped.ToArray();
   birthSecondsRemaining=4;birthBatchAge=0;
  }
  /// <summary>
  /// Called only by the authoritative physics adapter when a bubble actually reaches an animal.
  /// The animal staggers (public event) and, with a configured stun, steers weakly for a moment.
  /// </summary>
  public bool RecordBubbleHit(int victimSlot,int ownerSlot) {
   if(victimSlot<0||victimSlot>=players.Length||phase!=RoundPhase.Chase)return false;var p=players[victimSlot];
   if(p.Faction!=Faction.Animal)return false;
   p.StunRemaining=Math.Max(p.StunRemaining,settings.Current.HitStunSeconds);
   Publish(FeelEventKind.Stagger,ownerSlot,victimSlot,p.Position);return true;
  }
  /// <summary>Called only by the authoritative physics adapter when a body or bubble knocks a light prop (presentation event).</summary>
  public bool RecordPropPush(int prop,int actor,WorldPosition position,float strength) {
   if(prop<0||prop>=short.MaxValue||float.IsNaN(strength)||float.IsInfinity(strength)||phase==RoundPhase.Results)return false;
   double last;if(propKnocks.TryGetValue(prop,out last)&&hostTime-last<PropKnockSeconds)return false;
   propKnocks[prop]=hostTime;Publish(FeelEventKind.PropPushed,actor,prop,position,Math.Max(0,Math.Min(1,strength)));return true;
  }
  /// <summary>One prop knocks at most this often, so a body plowing a crate does not flood the event window.</summary>
  public const double PropKnockSeconds=.25;
  readonly Dictionary<int,double> propKnocks=new Dictionary<int,double>();
  /// <summary>Hard-landing threshold (m/s downward) shared with the landing dust.</summary>
  public const float HardLandingSpeed=4;
  /// <summary>Called only by the authoritative physics adapter when a body touches down hard.</summary>
  public bool RecordLanding(int slot,float impactSpeed) {
   if(slot<0||slot>=players.Length||float.IsNaN(impactSpeed)||impactSpeed<=HardLandingSpeed)return false;
   Publish(FeelEventKind.Landing,slot,-1,players[slot].Position,Math.Min(1,(impactSpeed-HardLandingSpeed)/12f));return true;
  }
  /// <summary>
  /// An animal starts a swing. With a configured tell the swing is judged one windup later
  /// (<see cref="AttackDue"/>); with zero windup it is due on this same step, as the click-frame rule.
  /// </summary>
  public bool TryStartAttack(int slot,int roundId) {
   if(!CanAct(slot,roundId)||phase!=RoundPhase.Chase)return false;var p=players[slot];
   if(p.Faction!=Faction.Animal||p.AttackGraceRemaining>0||p.FireCooldownRemaining>0||p.AttackWindupRemaining>0||p.HitStopRemaining>0)return false;
   if(dueAttacks.Contains(slot))return true;
   double windup=settings.Current.AttackWindupSeconds;
   if(windup<=0){dueAttacks.Add(slot);return true;}
   if(p.SwingRemaining>0)return false;
   p.AttackWindupRemaining=windup;Publish(FeelEventKind.AttackWindup,slot,-1,p.Position);return true;
  }
  /// <summary>True when this animal's swing reaches its strike moment and waits for host physics to judge it.</summary>
  public bool AttackDue(int slot)=>dueAttacks.Contains(slot)&&players[slot].HitStopRemaining<=0;
  /// <summary>Host physics found no reachable human for a due swing.</summary>
  public bool ResolveAttackMiss(int slot,int roundId) {
   if(!CanAct(slot,roundId)||players[slot].HitStopRemaining>0||!dueAttacks.Remove(slot))return false;var p=players[slot];
   // A held button re-swings every step at zero windup; publish one swing per presented swing.
   if(p.SwingRemaining<=0){p.SwingRemaining=CombatRules.SwingSeconds;Publish(FeelEventKind.AttackMiss,slot,-1,p.Position);}
   return true;
  }
  // Host physics confirms reach and line of sight; clients submit input, never hit claims.
  public bool TryMeleeHit(int attackerSlot,int victimSlot,int roundId) {
   if(!CanAct(attackerSlot,roundId)||phase!=RoundPhase.Chase||victimSlot<0||victimSlot>=players.Length||!dueAttacks.Contains(attackerSlot))return false;
   var attacker=players[attackerSlot];var victim=players[victimSlot];
   if(attacker.Faction!=Faction.Animal||victim.Faction!=Faction.Human||attacker.AttackGraceRemaining>0||attacker.FireCooldownRemaining>0||attacker.HitStopRemaining>0)return false;
   double x=attacker.Position.X-victim.Position.X,y=attacker.Position.Y-victim.Position.Y,z=attacker.Position.Z-victim.Position.Z;
   if(x*x+y*y+z*z>CombatRules.MeleeDistance*CombatRules.MeleeDistance)return false;
   dueAttacks.Remove(attackerSlot);
   attacker.FireCooldownRemaining=.5;attacker.SwingRemaining=CombatRules.SwingSeconds;
   // Hit-stop freezes only the two characters in the hit; everyone else and the match clock keep running.
   attacker.HitStopRemaining=victim.HitStopRemaining=settings.Current.HitStopSeconds;
   victim.Faction=Faction.Animal;victim.AttackGraceRemaining=settings.Current.TransformAttackGrace;
   AssignCharacter(victim,animalRoster[nextAnimal++%animalRoster.Length]);
   victim.ReloadRemaining=0;victim.FireCooldownRemaining=0;PublishBirth(new[]{victim});
   Publish(FeelEventKind.AttackHit,attackerSlot,victimSlot,victim.Position);Publish(FeelEventKind.Transform,attackerSlot,victimSlot,victim.Position);
   if(players.All(p=>p.Faction==Faction.Animal)){phase=RoundPhase.Results;remaining=settings.Current.ResultSeconds;phaseStartedAt=hostTime;phaseDeadline=hostTime+remaining;winner=Faction.Animal;ClearActions();}
   return true;
  }

 }
}
