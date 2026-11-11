using System;
using System.Collections.Generic;
using System.Linq;
namespace AvH {
 // Special abilities: who may use one, what it does to whom, and the things it leaves in the world.
 // The rules decide effects and timers; shoves leave as impulses for the physics adapter (TakeImpulses).
 public sealed partial class PlaytestSession {
  readonly List<GadgetState> gadgets=new List<GadgetState>();
  readonly List<Impulse> impulses=new List<Impulse>();
  readonly HashSet<int> runHits=new HashSet<int>();
  readonly Dictionary<int,double> padHits=new Dictionary<int,double>(),sprayHits=new Dictionary<int,double>();
  readonly float[] aimYaw=new float[12],aimPitch=new float[12];
  readonly WorldPosition[] disguisedAt=new WorldPosition[12];
  int nextGadgetId;
  /// <summary>Line of sight between two slots, supplied by the physics adapter. Without one everything is in sight.</summary>
  public Func<int,int,bool> Sight;
  /// <summary>Shoves decided since the last call. The physics adapter applies and thereby consumes them.</summary>
  public Impulse[] TakeImpulses(){if(impulses.Count==0)return Array.Empty<Impulse>();var taken=impulses.ToArray();impulses.Clear();return taken;}
  /// <summary>Where a slot is looking; a held ability (water cannon) follows it.</summary>
  public void RecordAim(int slot,float yaw,float pitch){if(slot<0||slot>=aimYaw.Length||float.IsNaN(yaw)||float.IsNaN(pitch))return;aimYaw[slot]=yaw;aimPitch[slot]=pitch;}
  static bool Active(PlayerState p,AbilityKind kind)=>p.Ability==kind&&p.AbilityActiveRemaining>0;
  /// <summary>Held in place or lifted: no attack, no ability.</summary>
  static bool Helpless(PlayerState p)=>p.RootRemaining>0||p.LiftRemaining>0;
  static void ClearEffects(PlayerState p){p.AbilityActiveRemaining=0;p.SlowRemaining=p.RootRemaining=p.LiftRemaining=p.ShakeRemaining=p.InvulnerableRemaining=p.LowGravityRemaining=0;p.Hold=AbilityKind.None;}
  void ResetAbility(PlayerState p) {
   var def=Abilities.For(p.CharacterId);ClearEffects(p);
   p.Ability=def?.Kind??AbilityKind.None;p.AbilityCooldownRemaining=0;p.AbilityUsesRemaining=def?.Uses??0;p.AbilityYaw=0;
  }
  void ResetAbilities(){gadgets.Clear();impulses.Clear();runHits.Clear();padHits.Clear();sprayHits.Clear();foreach(var p in players)ResetAbility(p);}
  void EndAbility(PlayerState p,bool announce=true){if(p.AbilityActiveRemaining<=0)return;p.AbilityActiveRemaining=0;if(announce)Publish(FeelEventKind.AbilityEnd,p.Slot,-1,p.Position,(float)p.Ability);}
  static double Flat(PlayerState a,WorldPosition b){double x=a.Position.X-b.X,z=a.Position.Z-b.Z;return Math.Sqrt(x*x+z*z);}
  static double Space(WorldPosition a,WorldPosition b){double x=a.X-b.X,y=a.Y-b.Y,z=a.Z-b.Z;return Math.Sqrt(x*x+y*y+z*z);}
  static void Heading(float yaw,float pitch,out float x,out float y,out float z){double r=yaw*Math.PI/180,q=pitch*Math.PI/180;x=(float)(Math.Sin(r)*Math.Cos(q));y=(float)-Math.Sin(q);z=(float)(Math.Cos(r)*Math.Cos(q));}
  bool Sees(int from,int to)=>Sight==null||Sight(from,to);
  /// <summary>A helmet takes one shove or hold; a rhino in full charge takes none.</summary>
  bool Shrugs(PlayerState victim) {
   if(Active(victim,AbilityKind.Stampede))return true;
   if(!Active(victim,AbilityKind.Helmet))return false;
   EndAbility(victim,false);Publish(FeelEventKind.AbilityHit,victim.Slot,victim.Slot,victim.Position,(float)AbilityKind.Helmet);return true;
  }
  /// <summary>Shoves <paramref name="victim"/> away from a point. Returns false when the victim shrugged it off.</summary>
  bool Shove(PlayerState by,PlayerState victim,WorldPosition from,float strength,float up) {
   if(Shrugs(victim))return false;
   float x=victim.Position.X-from.X,z=victim.Position.Z-from.Z,length=(float)Math.Sqrt(x*x+z*z);
   if(length<.01f){Heading(aimYaw[by.Slot],0,out x,out _,out z);length=1;}
   float scale=victim.Faction==Faction.Animal?AnimalBalance.For(settings.Current,victim).Knockback:1;
   impulses.Add(new Impulse{Slot=victim.Slot,X=x/length*strength*scale,Y=up*scale,Z=z/length*strength*scale});
   if(victim.Faction==Faction.Human)lastShove[victim.Slot]=(by.Slot,hostTime);
   return true;
  }
  void Kick(PlayerState p,float vertical,float forward=0,float yaw=0){Heading(yaw,0,out var x,out _,out var z);impulses.Add(new Impulse{Slot=p.Slot,X=x*forward,Z=z*forward,SetVertical=true,Vertical=vertical});}
  bool Hold(PlayerState by,PlayerState victim,AbilityKind kind,double seconds) {
   if(Shrugs(victim))return false;
   victim.RootRemaining=Math.Max(victim.RootRemaining,seconds);victim.Hold=kind;victim.AttackWindupRemaining=0;dueAttacks.Remove(victim.Slot);
   if(Active(victim,AbilityKind.Disguise)||CharacterMotion.Locked(victim))EndAbility(victim,false);
   Publish(FeelEventKind.AbilityHit,by.Slot,victim.Slot,victim.Position,(float)kind);return true;
  }
  IEnumerable<PlayerState> Others(PlayerState p,Faction faction,double radius)=>players.Where(q=>q.Slot!=p.Slot&&q.Faction==faction&&Space(q.Position,p.Position)<=radius);
  /// <summary>The enemy nearest the aim line, within range, a cone and sight.</summary>
  PlayerState Aimed(PlayerState p,Faction faction,float range,float yaw,float pitch,double coneDegrees=22) {
   Heading(yaw,pitch,out var fx,out var fy,out var fz);PlayerState best=null;double bestDot=Math.Cos(coneDegrees*Math.PI/180);
   foreach(var q in players) {
    if(q.Slot==p.Slot||q.Faction!=faction)continue;
    double x=q.Position.X-p.Position.X,y=q.Position.Y-p.Position.Y,z=q.Position.Z-p.Position.Z,d=Math.Sqrt(x*x+y*y+z*z);
    if(d>range||d<.01)continue;
    // Up close the cone widens: nobody misses a target at arm's length.
    double dot=(x*fx+y*fy+z*fz)/d+(d<2.5?.25:0);
    if(dot<bestDot||!Sees(p.Slot,q.Slot))continue;
    bestDot=dot;best=q;
   }
   return best;
  }
  /// <summary>
  /// A character uses its ability (Q). Chase only. Fails while it waits, has no uses left, is still running, or its user is
  /// held. A use always starts the wait, even when it reaches nobody.
  /// </summary>
  public bool TryUseAbility(int slot,int roundId,float yaw,float pitch) {
   if(!CanAct(slot,roundId)||phase!=RoundPhase.Chase||!settings.Current.Abilities||float.IsNaN(yaw)||float.IsNaN(pitch))return false;
   var p=players[slot];var def=Abilities.Of(p.Ability);
   if(def==null||p.AbilityCooldownRemaining>0||p.AbilityActiveRemaining>0||def.Uses>0&&p.AbilityUsesRemaining<=0||Helpless(p)||p.HitStopRemaining>0)return false;
   p.AbilityCooldownRemaining=def.Cooldown;p.AbilityActiveRemaining=def.Duration;p.AbilityYaw=yaw;if(def.Uses>0)p.AbilityUsesRemaining--;
   RecordAim(slot,yaw,pitch);
   Publish(FeelEventKind.Ability,slot,-1,p.Position,(float)p.Ability);
   var enemy=p.Faction==Faction.Animal?Faction.Human:Faction.Animal;PlayerState target;
   switch(p.Ability) {
    case AbilityKind.Disguise:disguisedAt[slot]=p.Position;break;
    case AbilityKind.DoubleJump:Kick(p,8.5f);break;
    case AbilityKind.Flap:Kick(p,7.5f);break;
    case AbilityKind.Umbrella:Kick(p,7);break;
    case AbilityKind.Balloon:Kick(p,2);break;
    case AbilityKind.Dive:Kick(p,11,5,yaw);break;
    case AbilityKind.Leap:Kick(p,8,10,yaw);break;
    case AbilityKind.Hook:{Heading(yaw,pitch,out _,out var up,out _);Kick(p,5+7*Math.Max(0,up),13,yaw);break;}
    case AbilityKind.Charge:case AbilityKind.Stampede:case AbilityKind.Kickboard:runHits.RemoveWhere(k=>k/64==slot);break;
    case AbilityKind.Acorn:
     target=Aimed(p,enemy,Abilities.AcornRange,yaw,pitch);
     if(target!=null&&!Shrugs(target)){target.ShakeRemaining=1.5;Publish(FeelEventKind.AbilityHit,slot,target.Slot,target.Position,(float)p.Ability);}
     break;
    case AbilityKind.Headbutt:
     foreach(var q in Others(p,enemy,Abilities.HeadbuttRange).ToArray())if(InFront(p,q,yaw,60)&&Shove(p,q,p.Position,12,4)){q.SlowRemaining=Math.Max(q.SlowRemaining,1.5);Publish(FeelEventKind.AbilityHit,slot,q.Slot,q.Position,(float)p.Ability);}
     break;
    case AbilityKind.Roar:foreach(var q in Others(p,enemy,Abilities.RoarRadius).ToArray())Hold(p,q,AbilityKind.Roar,1);break;
    case AbilityKind.Slam:foreach(var q in Others(p,enemy,Abilities.SlamRadius).ToArray())if(Shove(p,q,p.Position,10,5)){q.ShakeRemaining=Math.Max(q.ShakeRemaining,2);Publish(FeelEventKind.AbilityHit,slot,q.Slot,q.Position,(float)p.Ability);}break;
    case AbilityKind.Whistle:foreach(var q in Others(p,enemy,Abilities.WhistleRadius).ToArray())if(!Shrugs(q)){q.SlowRemaining=Math.Max(q.SlowRemaining,2);Publish(FeelEventKind.AbilityHit,slot,q.Slot,q.Position,(float)p.Ability);}break;
    case AbilityKind.Lasso:target=Aimed(p,enemy,Abilities.LassoRange,yaw,pitch);if(target!=null)Hold(p,target,AbilityKind.Lasso,2);break;
    case AbilityKind.IceRay:target=Aimed(p,enemy,Abilities.IceRange,yaw,pitch);if(target!=null)Hold(p,target,AbilityKind.IceRay,2.5);break;
    case AbilityKind.BigBubble:
     target=Aimed(p,enemy,Abilities.BubbleRange,yaw,pitch);
     if(target!=null&&!Shrugs(target)){target.LiftRemaining=2;target.Hold=AbilityKind.BigBubble;target.AttackWindupRemaining=0;dueAttacks.Remove(target.Slot);EndAbility(target,false);Publish(FeelEventKind.AbilityHit,slot,target.Slot,target.Position,(float)p.Ability);}
     break;
    case AbilityKind.Banana:Place(p,30);break;
    case AbilityKind.SpringPad:Place(p,45);break;
    case AbilityKind.Scarecrow:Place(p,8);break;
    case AbilityKind.Smoke:Place(p,5);break;
    case AbilityKind.ZeroG:Place(p,6);foreach(var q in Others(p,enemy,Abilities.ZeroGRadius).ToArray())if(!Shrugs(q)){Kick(q,7);Publish(FeelEventKind.AbilityHit,slot,q.Slot,q.Position,(float)p.Ability);}break;
   }
   return true;
  }
  static bool InFront(PlayerState p,PlayerState q,float yaw,double coneDegrees){Heading(yaw,0,out var fx,out _,out var fz);double x=q.Position.X-p.Position.X,z=q.Position.Z-p.Position.Z,d=Math.Sqrt(x*x+z*z);return d<.6||(x*fx+z*fz)/d>=Math.Cos(coneDegrees*Math.PI/180);}
  void Place(PlayerState p,double seconds) {
   // One scarecrow, smoke or zone per owner; peels and pads pile up to their use count.
   if(p.Ability!=AbilityKind.Banana&&p.Ability!=AbilityKind.SpringPad)gadgets.RemoveAll(g=>g.Owner==p.Slot&&g.Kind==p.Ability);
   gadgets.Add(new GadgetState{Id=++nextGadgetId,Owner=p.Slot,Kind=p.Ability,Position=p.Position,Remaining=seconds});
  }
  /// <summary>Called only by the authoritative physics adapter when a straight-line run hits a wall. A kickboard rider falls over.</summary>
  public bool RecordCrash(int slot) {
   if(slot<0||slot>=players.Length)return false;var p=players[slot];
   if(!CharacterMotion.Locked(p))return false;
   bool rider=p.Ability==AbilityKind.Kickboard;EndAbility(p);
   if(rider){p.RootRemaining=Math.Max(p.RootRemaining,1);p.Hold=AbilityKind.Kickboard;}
   return true;
  }
  /// <summary>Called only by the authoritative physics adapter when a body touches the ground. An eagle's dive ends in a shockwave.</summary>
  public bool RecordTouchdown(int slot){if(slot<0||slot>=players.Length)return false;return LandAbility(players[slot]);}
  bool LandAbility(PlayerState p) {
   var def=Abilities.Of(p.Ability);if(def==null||p.AbilityActiveRemaining<=0||def.Duration-p.AbilityActiveRemaining<.3)return false;
   switch(p.Ability) {
    case AbilityKind.Dive:
     EndAbility(p);
     if(phase==RoundPhase.Chase)foreach(var q in Others(p,Faction.Human,Abilities.DiveRadius).ToArray())if(Shove(p,q,p.Position,9,4)){q.SlowRemaining=Math.Max(q.SlowRemaining,1.5);Publish(FeelEventKind.AbilityHit,p.Slot,q.Slot,q.Position,(float)p.Ability);}
     return true;
    case AbilityKind.Flap:case AbilityKind.Umbrella:case AbilityKind.Balloon:EndAbility(p,false);return true;
   }
   return false;
  }
  /// <summary>A vaccine or its afterglow stops an infection. The swing is spent either way.</summary>
  bool BlockInfection(PlayerState attacker,PlayerState victim) {
   if(victim.InvulnerableRemaining>0)return true;
   if(!Active(victim,AbilityKind.Vaccine))return false;
   EndAbility(victim,false);victim.InvulnerableRemaining=2;attacker.FireCooldownRemaining=1.2;
   Shove(victim,attacker,victim.Position,9,3);
   Publish(FeelEventKind.AbilityHit,victim.Slot,attacker.Slot,victim.Position,(float)AbilityKind.Vaccine);return true;
  }
  /// <summary>A crocodile's readied bite slows the humans who stood near the one it caught.</summary>
  void AfterBite(PlayerState attacker,PlayerState victim) {
   if(Active(attacker,AbilityKind.Hunt)||!Active(attacker,AbilityKind.Bite)){return;}
   EndAbility(attacker,false);
   foreach(var q in Others(victim,Faction.Human,Abilities.BiteRadius).ToArray())if(!Shrugs(q)){q.SlowRemaining=Math.Max(q.SlowRemaining,3);Publish(FeelEventKind.AbilityHit,attacker.Slot,q.Slot,q.Position,(float)AbilityKind.Bite);}
  }
  void AdvanceAbilities(double elapsed) {
   foreach(var p in players) {
    p.AbilityCooldownRemaining=Math.Max(0,p.AbilityCooldownRemaining-elapsed);
    p.SlowRemaining=Math.Max(0,p.SlowRemaining-elapsed);p.RootRemaining=Math.Max(0,p.RootRemaining-elapsed);p.LiftRemaining=Math.Max(0,p.LiftRemaining-elapsed);
    p.ShakeRemaining=Math.Max(0,p.ShakeRemaining-elapsed);p.InvulnerableRemaining=Math.Max(0,p.InvulnerableRemaining-elapsed);p.LowGravityRemaining=Math.Max(0,p.LowGravityRemaining-elapsed);
    if(p.RootRemaining<=0&&p.LiftRemaining<=0)p.Hold=AbilityKind.None;
    if(p.AbilityActiveRemaining>0){p.AbilityActiveRemaining=Math.Max(0,p.AbilityActiveRemaining-elapsed);if(p.AbilityActiveRemaining==0)Publish(FeelEventKind.AbilityEnd,p.Slot,-1,p.Position,(float)p.Ability);}
   }
   for(int i=gadgets.Count-1;i>=0;i--){gadgets[i].Remaining-=elapsed;if(gadgets[i].Remaining<=0)gadgets.RemoveAt(i);}
   if(phase!=RoundPhase.Chase)return;
   foreach(var p in players) {
    if(p.AbilityActiveRemaining<=0)continue;
    switch(p.Ability) {
     case AbilityKind.Disguise:if(Space(p.Position,disguisedAt[p.Slot])>.4)EndAbility(p);break;
     case AbilityKind.Charge:case AbilityKind.Stampede:
      foreach(var q in Others(p,Faction.Human,1.5).ToArray())if(runHits.Add(p.Slot*64+q.Slot)&&Shove(p,q,p.Position,p.Ability==AbilityKind.Stampede?12:10,3.5f))Publish(FeelEventKind.AbilityHit,p.Slot,q.Slot,q.Position,(float)p.Ability);
      break;
     case AbilityKind.Balloon:
      if(Others(p,Faction.Animal,1.5).Any()){EndAbility(p);Publish(FeelEventKind.AbilityHit,p.Slot,p.Slot,p.Position,(float)p.Ability);}
      break;
     case AbilityKind.WaterCannon:
      Heading(aimYaw[p.Slot],aimPitch[p.Slot],out var fx,out var fy,out var fz);
      foreach(var q in Others(p,Faction.Animal,Abilities.CannonRange).ToArray()) {
       double x=q.Position.X-p.Position.X,y=q.Position.Y-p.Position.Y,z=q.Position.Z-p.Position.Z,d=Math.Sqrt(x*x+y*y+z*z);
       if(d>2&&(x*fx+y*fy+z*fz)/d<.88||!Sees(p.Slot,q.Slot)||Active(q,AbilityKind.Stampede))continue;
       float scale=AnimalBalance.For(settings.Current,q).Knockback*(float)elapsed*34;
       impulses.Add(new Impulse{Slot=q.Slot,X=fx*scale,Y=.25f*scale,Z=fz*scale});
       int key=p.Slot*64+q.Slot;if(!sprayHits.TryGetValue(key,out var last)||hostTime-last>=.5){sprayHits[key]=hostTime;Publish(FeelEventKind.AbilityHit,p.Slot,q.Slot,q.Position,(float)p.Ability);}
      }
      break;
    }
   }
   for(int i=gadgets.Count-1;i>=0;i--) {
    var g=gadgets[i];
    switch(g.Kind) {
     case AbilityKind.Banana:
      var slipped=players.FirstOrDefault(q=>q.Faction==Faction.Animal&&q.LiftRemaining<=0&&Space(q.Position,g.Position)<=.9&&!Active(q,AbilityKind.Stampede));
      if(slipped!=null){gadgets.RemoveAt(i);slipped.RootRemaining=Math.Max(slipped.RootRemaining,1);slipped.Hold=AbilityKind.Banana;slipped.AttackWindupRemaining=0;dueAttacks.Remove(slipped.Slot);EndAbility(slipped,false);Publish(FeelEventKind.AbilityHit,g.Owner,slipped.Slot,slipped.Position,(float)g.Kind);}
      break;
     case AbilityKind.SpringPad:
      foreach(var q in players)if(Space(q.Position,g.Position)<=1&&q.LiftRemaining<=0&&(!padHits.TryGetValue(q.Slot,out var last)||hostTime-last>=1)){padHits[q.Slot]=hostTime;Kick(q,13);Publish(FeelEventKind.AbilityHit,g.Owner,q.Slot,q.Position,(float)g.Kind);}
      break;
     case AbilityKind.ZeroG:foreach(var q in players)if(Space(q.Position,g.Position)<=Abilities.ZeroGRadius+3&&Flat(q,g.Position)<=Abilities.ZeroGRadius)q.LowGravityRemaining=.25;break;
    }
   }
  }
  /// <summary>True when smoke hides this human from an animal that is not right next to them.</summary>
  public static bool Smoked(SessionState state,PlayerState human,PlayerState animal) {
   if(state.Gadgets==null||Space(human.Position,animal.Position)<=Abilities.SmokeSightDistance)return false;
   foreach(var g in state.Gadgets)if(g.Kind==AbilityKind.Smoke&&Space(human.Position,g.Position)<=Abilities.SmokeRadius)return true;
   return false;
  }
 }
}
