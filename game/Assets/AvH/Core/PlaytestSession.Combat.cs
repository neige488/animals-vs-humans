using System;
using System.Linq;
namespace AvH {
 public static class CombatRules {public const float MeleeDistance=1.8f;}
 public sealed partial class PlaytestSession {
  double birthSecondsRemaining, birthBatchAge;
  void ResetCombat() {
   birthSecondsRemaining=0;birthBatchAge=double.MaxValue;
   foreach(var p in players){p.Ammo=settings.Current.Magazine;p.ReloadRemaining=0;p.AttackGraceRemaining=0;p.FireCooldownRemaining=0;}
  }
  void AdvanceCombat(double elapsed) {
   birthSecondsRemaining=Math.Max(0,birthSecondsRemaining-elapsed);birthBatchAge+=elapsed;
   foreach(var p in players) {
    p.FireCooldownRemaining=Math.Max(0,p.FireCooldownRemaining-elapsed);
    p.AttackGraceRemaining=Math.Max(0,p.AttackGraceRemaining-elapsed);
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
  // Host physics confirms reach and line of sight; clients submit input, never hit claims.
  public bool TryMeleeHit(int attackerSlot,int victimSlot,int roundId) {
   if(!CanAct(attackerSlot,roundId)||phase!=RoundPhase.Chase||victimSlot<0||victimSlot>=players.Length)return false;
   var attacker=players[attackerSlot];var victim=players[victimSlot];
   if(attacker.Faction!=Faction.Animal||victim.Faction!=Faction.Human||attacker.AttackGraceRemaining>0||attacker.FireCooldownRemaining>0)return false;
   double x=attacker.Position.X-victim.Position.X,y=attacker.Position.Y-victim.Position.Y,z=attacker.Position.Z-victim.Position.Z;
   if(x*x+y*y+z*z>CombatRules.MeleeDistance*CombatRules.MeleeDistance)return false;
   attacker.FireCooldownRemaining=.5;
   victim.Faction=Faction.Animal;victim.AttackGraceRemaining=settings.Current.TransformAttackGrace;
   AssignCharacter(victim,animalRoster[nextAnimal++%animalRoster.Length]);
   victim.ReloadRemaining=0;victim.FireCooldownRemaining=0;PublishBirth(new[]{victim});
   if(players.All(p=>p.Faction==Faction.Animal)){phase=RoundPhase.Results;remaining=settings.Current.ResultSeconds;phaseStartedAt=hostTime;phaseDeadline=hostTime+remaining;winner=Faction.Animal;}
   return true;
  }

 }
}
