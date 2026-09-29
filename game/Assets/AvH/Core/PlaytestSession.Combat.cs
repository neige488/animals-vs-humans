using System;
using System.Linq;
namespace AvH {
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
  void PublishBirth(int count) {
   if(birthBatchAge<=.5 && births.Length==1)births[0].Count+=count;
   else births=new[]{new BirthNotice{Kind=animalKind,Rarity=animalRarity,Count=count}};
   birthSecondsRemaining=4;birthBatchAge=0;
  }
  // Host physics confirms reach and line of sight; clients submit input, never hit claims.
  public bool TryMeleeHit(int attackerSlot,int victimSlot,int roundId) {
   if(!CanAct(attackerSlot,roundId)||phase!=RoundPhase.Chase||victimSlot<0||victimSlot>=players.Length)return false;
   var attacker=players[attackerSlot];var victim=players[victimSlot];
   if(attacker.Faction!=Faction.Animal||victim.Faction!=Faction.Human||attacker.AttackGraceRemaining>0||attacker.FireCooldownRemaining>0)return false;
   double x=attacker.Position.X-victim.Position.X,y=attacker.Position.Y-victim.Position.Y,z=attacker.Position.Z-victim.Position.Z;
   if(x*x+y*y+z*z>1.8*1.8)return false;
   attacker.FireCooldownRemaining=.5;
   victim.Faction=Faction.Animal;victim.AttackGraceRemaining=settings.Current.TransformAttackGrace;
   victim.ReloadRemaining=0;victim.FireCooldownRemaining=0;PublishBirth(1);
   if(players.All(p=>p.Faction==Faction.Animal)){phase=RoundPhase.Results;remaining=settings.Current.ResultSeconds;phaseDeadline=hostTime+remaining;winner=Faction.Animal;}
   return true;
  }

 }
}
