using System;
namespace AvH {
 public sealed partial class PlaytestSession {
  void ResetCombat() {
   foreach(var p in players){p.Ammo=settings.Current.Magazine;p.ReloadRemaining=0;p.AttackGraceRemaining=0;p.FireCooldownRemaining=0;}
  }
  void AdvanceCombat(double elapsed) {
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
 }
}
