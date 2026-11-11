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
   birthSecondsRemaining=0;birthBatchAge=double.MaxValue;events.Clear();propKnocks.Clear();lastShove.Clear();ClearActions();ResetAbilities();
   foreach(var p in players){p.Ammo=settings.Current.Magazine;p.ReloadRemaining=0;p.AttackGraceRemaining=0;p.FireCooldownRemaining=0;}
  }
  // Round end cuts every tell, freeze and stun in progress.
  void ClearActions(){dueAttacks.Clear();toolHits.Clear();foreach(var p in players){p.ToolUseRemaining=0;p.AttackWindupRemaining=0;p.SwingRemaining=0;p.HitStopRemaining=0;p.StunRemaining=0;ClearEffects(p);}}
  static ActionPhase ActionOf(PlayerState p)=>p.HitStopRemaining>0?ActionPhase.HitStop:p.StunRemaining>0?ActionPhase.Stunned:p.AttackWindupRemaining>0?ActionPhase.Windup:p.SwingRemaining>0?ActionPhase.Swing:ActionPhase.None;
  void Publish(FeelEventKind kind,int actor,int target,WorldPosition position,float strength=1) {
   events.Add(new FeelEvent{Id=++nextEventId,Round=round,Kind=kind,Actor=actor,Target=target,Position=position,HostTime=hostTime,Strength=strength});
   if(events.Count>CombatRules.EventLimit)events.RemoveRange(0,events.Count-CombatRules.EventLimit);
  }
  void AdvanceCombat(double elapsed) {
   birthSecondsRemaining=Math.Max(0,birthSecondsRemaining-elapsed);birthBatchAge+=elapsed;
   double now=hostTime+elapsed;events.RemoveAll(e=>now-e.HostTime>CombatRules.EventWindowSeconds);
   foreach(var p in players) {
    p.FireCooldownRemaining=Math.Max(0,p.FireCooldownRemaining-elapsed);p.ToolUseRemaining=Math.Max(0,p.ToolUseRemaining-elapsed);
    p.AttackGraceRemaining=Math.Max(0,p.AttackGraceRemaining-elapsed);
    // A tell and a swing pause while their character is frozen: both resume after the freeze.
    double frozen=Math.Min(elapsed,p.HitStopRemaining);
    p.SwingRemaining=Math.Max(0,p.SwingRemaining-(elapsed-frozen));
    p.HitStopRemaining=Math.Max(0,p.HitStopRemaining-elapsed);p.StunRemaining=Math.Max(0,p.StunRemaining-elapsed);
    if(p.AttackWindupRemaining>0){p.AttackWindupRemaining=Math.Max(0,p.AttackWindupRemaining-(elapsed-frozen));if(p.AttackWindupRemaining==0)dueAttacks.Add(p.Slot);}
    if(p.ReloadRemaining>0){p.ReloadRemaining=Math.Max(0,p.ReloadRemaining-elapsed);if(p.ReloadRemaining==0)p.Ammo=settings.Current.Magazine;}
   }
   AdvanceAbilities(elapsed);
  }
  bool CanAct(int slot,int roundId)=>slot>=0 && slot<players.Length && roundId==round && phase!=RoundPhase.Results;
  public bool TryFire(int slot,int roundId) {
   if(!CanAct(slot,roundId))return false;var p=players[slot];
   bool ammo=Tools.UsesAmmo(p.Tool);
   if(p.Faction!=Faction.Human || ammo&&p.Ammo<=0 || p.ReloadRemaining>0 || p.FireCooldownRemaining>0 || Active(p,AbilityKind.Balloon))return false;
   if(ammo)p.Ammo--;p.FireCooldownRemaining=settings.Current.FireInterval*Tools.IntervalScale(p.Tool);p.ToolUseRemaining=Tools.ActionSeconds(p.Tool);return true;
  }
  public bool TryReload(int slot,int roundId) {
   if(!CanAct(slot,roundId))return false;var p=players[slot];
   if(p.Faction!=Faction.Human || !Tools.UsesAmmo(p.Tool) || p.Ammo==settings.Current.Magazine || p.ReloadRemaining>0)return false;
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
   if(Active(p,AbilityKind.Stampede))return false;
   p.StunRemaining=Math.Max(p.StunRemaining,settings.Current.HitStunSeconds);
   Publish(FeelEventKind.Stagger,ownerSlot,victimSlot,p.Position);return true;
  }
  /// <summary>
  /// Called only by the authoritative physics adapter when a shot shoves a human (friendly push).
  /// No stun and no faction change: the event only tells everyone who shoved whom.
  /// </summary>
  public bool RecordFriendlyShove(int victimSlot,int ownerSlot) {
   if(victimSlot<0||victimSlot>=players.Length||ownerSlot<0||ownerSlot>=players.Length||victimSlot==ownerSlot||phase==RoundPhase.Results)return false;
   if(players[victimSlot].Faction!=Faction.Human)return false;
   lastShove[victimSlot]=(ownerSlot,hostTime);Publish(FeelEventKind.Shoved,ownerSlot,victimSlot,players[victimSlot].Position);return true;
  }
  /// <summary>
  /// Called only by the authoritative physics adapter when any tool use reaches a character.
  /// An animal staggers (with a stun unless the tool is a gust); a human is only shoved.
  /// A blower hits many times a second: one public event per shooter and target per <see cref="GustEventSeconds"/>.
  /// </summary>
  public bool RecordToolHit(int victimSlot,int ownerSlot,ToolKind tool) {
   if(victimSlot<0||victimSlot>=players.Length||ownerSlot<0||ownerSlot>=players.Length||victimSlot==ownerSlot||phase==RoundPhase.Results)return false;
   var p=players[victimSlot];
   if(p.Faction==Faction.Human)lastShove[victimSlot]=(ownerSlot,hostTime);
   if(tool==ToolKind.Blower){double last;var key=ownerSlot*64+victimSlot;if(toolHits.TryGetValue(key,out last)&&hostTime-last<GustEventSeconds)return false;toolHits[key]=hostTime;}
   if(p.Faction==Faction.Human){Publish(FeelEventKind.Shoved,ownerSlot,victimSlot,p.Position);return true;}
   if(phase!=RoundPhase.Chase)return false;
   if(Active(p,AbilityKind.Stampede))return false;
   if(Tools.Stuns(tool))p.StunRemaining=Math.Max(p.StunRemaining,settings.Current.HitStunSeconds);
   Publish(FeelEventKind.Stagger,ownerSlot,victimSlot,p.Position);return true;
  }
  public const double GustEventSeconds=.8;
  /// <summary>A fall within this long of a shove is credited to the shover.</summary>
  public const double FallCreditSeconds=4;
  readonly Dictionary<int,(int by,double at)> lastShove=new Dictionary<int,(int,double)>();
  /// <summary>
  /// Called only by the authoritative physics adapter when a body falls off the bottom of the map. On a map where a fall
  /// turns a human (<see cref="GameMaps.FallTurnsAnimal"/>), a human in the chase becomes an animal, credited to whoever
  /// shoved them in the last <see cref="FallCreditSeconds"/>. <paramref name="restart"/> is where the body starts again:
  /// the animals' start for animals in the chase, the round spawn otherwise.
  /// </summary>
  public bool RecordFall(int slot,out WorldPosition restart) {
   restart=default;if(slot<0||slot>=players.Length)return false;var p=players[slot];
   bool turned=false;
   if(phase==RoundPhase.Chase&&p.Faction==Faction.Human&&GameMaps.FallTurnsAnimal(map)) {
    int by=lastShove.TryGetValue(slot,out var s)&&hostTime-s.at<=FallCreditSeconds?s.by:-1;
    Publish(FeelEventKind.FellOff,by,slot,p.Position);
    p.Faction=Faction.Animal;p.AttackGraceRemaining=settings.Current.TransformAttackGrace;
    AssignCharacter(p,NextAnimal());p.ReloadRemaining=0;p.FireCooldownRemaining=0;p.StunRemaining=0;
    PublishBirth(new[]{p});turned=true;
   }
   p.Position=phase==RoundPhase.Chase&&p.Faction==Faction.Animal&&GameMaps.AnimalsStartApart(map)?GameMaps.AnimalStart(map,slot%6):Spawn(slot);
   restart=p.Position;lastShove.Remove(slot);
   if(turned){Publish(FeelEventKind.Transform,-1,slot,p.Position);
    if(players.All(q=>q.Faction==Faction.Animal)){phase=RoundPhase.Results;remaining=settings.Current.ResultSeconds;phaseStartedAt=hostTime;phaseDeadline=hostTime+remaining;winner=Faction.Animal;ClearActions();}}
   return turned;
  }
  readonly Dictionary<int,double> toolHits=new Dictionary<int,double>();
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
   if(slot<0||slot>=players.Length||float.IsNaN(impactSpeed))return false;
   LandAbility(players[slot]);
   if(impactSpeed<=HardLandingSpeed)return false;
   Publish(FeelEventKind.Landing,slot,-1,players[slot].Position,Math.Min(1,(impactSpeed-HardLandingSpeed)/12f));return true;
  }
  /// <summary>
  /// An animal starts a swing. With a configured tell the swing is judged one windup later
  /// (<see cref="AttackDue"/>); with zero windup it is due on this same step, as the click-frame rule.
  /// </summary>
  public bool TryStartAttack(int slot,int roundId) {
   if(!CanAct(slot,roundId)||phase!=RoundPhase.Chase)return false;var p=players[slot];
   if(p.Faction!=Faction.Animal||p.AttackGraceRemaining>0||p.FireCooldownRemaining>0||p.AttackWindupRemaining>0||p.HitStopRemaining>0||Helpless(p))return false;
   if(dueAttacks.Contains(slot))return true;
   if(Active(p,AbilityKind.Disguise))EndAbility(p);
   // A tiger on the hunt strikes without a tell.
   double windup=Active(p,AbilityKind.Hunt)?0:settings.Current.AttackWindupSeconds;
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
   if(Helpless(attacker))return false;
   dueAttacks.Remove(attackerSlot);
   attacker.FireCooldownRemaining=.5;attacker.SwingRemaining=CombatRules.SwingSeconds;
   if(BlockInfection(attacker,victim))return true;
   // Hit-stop freezes only the two characters in the hit; everyone else and the match clock keep running.
   attacker.HitStopRemaining=victim.HitStopRemaining=settings.Current.HitStopSeconds;
   victim.Faction=Faction.Animal;victim.AttackGraceRemaining=settings.Current.TransformAttackGrace;
   AssignCharacter(victim,NextAnimal());
   victim.ReloadRemaining=0;victim.FireCooldownRemaining=0;PublishBirth(new[]{victim});
   Publish(FeelEventKind.AttackHit,attackerSlot,victimSlot,victim.Position);Publish(FeelEventKind.Transform,attackerSlot,victimSlot,victim.Position);
   AfterBite(attacker,victim);
   if(players.All(p=>p.Faction==Faction.Animal)){phase=RoundPhase.Results;remaining=settings.Current.ResultSeconds;phaseStartedAt=hostTime;phaseDeadline=hostTime+remaining;winner=Faction.Animal;ClearActions();}
   return true;
  }

 }
}
