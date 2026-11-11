using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AvH {
 // Generates normal player inputs. No teleport, hit claim, or faction mutation exists here.
 public struct BotNavigationDiagnostics {public int GridBuilds,MaxGridBuildsPerStep;
  /// <summary>Route cells planned by bots, and how many of them lie on (or within 1.5 m of) a teammate's route.</summary>
  public long PlannedCells,SharedCells;}
 public sealed class BotDirector {
  public BotNavigationDiagnostics ObserveNavigation()=>routes.Observe();
  /// <summary>The bot route planner itself (tests and layout checks): grid route from one floor point to another.</summary>
  public List<Vector3> Plan(Vector3 from,Vector3 to,out bool reachable,bool allowDrop=false,Vector3? avoid=null)=>routes.Find(from,to,out reachable,allowDrop,avoid);
  /// <summary>New map geometry: the grid is rebuilt on the next route and every bot re-plans.</summary>
  public void ResetMap(RouteGrid grid){routes.Reset(grid);foreach(var brain in brains){brain.Round=-1;brain.Path.Clear();}}
  sealed class Brain {public int Shelter,Round;public Faction Faction;public float Replan,Flee,Stuck,ProgressTime,LastDistance;public Vector3 Last;public List<Vector3> Path=new List<Vector3>();public int Cursor;public BotAim Aim;public Vector3 Avoid;public float AvoidTime;
    // Grid nodes of the last route (teammates plan round them) and the side this animal closes in from.
    public List<int> Ids=new List<int>();public int ApproachPrey=-1;public Vector3 ApproachDir;
   // Personality: idle shuffle at a shelter, boar charge, rabbit hop.
   public Vector3 Fidget;public float FidgetTime,Charge=-3,Hop,AbilityWait;public Vector3 ChargeDir;}
  readonly Brain[] brains=Enumerable.Range(0,12).Select(i=>new Brain{Shelter=i%3,Aim=new BotAim(i+1)}).ToArray();
  double clock;
  readonly VillageRoutes routes=new VillageRoutes();
  
  const float SmokePenalty=40,DecoyRange=12;
  static bool Disguised(PlayerState p)=>p.Ability==AbilityKind.Disguise&&p.AbilityActiveRemaining>0;
  /// <summary>When a bot presses Q: each ability at the moment a player would reach for it.</summary>
  static bool WantsAbility(SessionState state,PlayerState me,PlayerState target,float distance,bool visible,bool fleeing,Vector3 position) {
   float rise=target.Position.Y-me.Position.Y;
   int near(float radius)=>state.Players.Count(p=>p.Faction!=me.Faction&&Vector3.Distance(V(p.Position),position)<=radius);
   switch(me.Ability) {
    // Animals
    case AbilityKind.Disguise:return me.StunRemaining>0&&distance>5;
    case AbilityKind.DoubleJump:case AbilityKind.Flap:return distance<7&&rise>1.1f;
    case AbilityKind.CatSpring:return distance<9&&rise>1.1f;
    case AbilityKind.BellySlide:return visible&&distance>5&&distance<16;
    case AbilityKind.Acorn:return visible&&distance<Abilities.AcornRange-1&&target.Slot>=0;
    case AbilityKind.Howl:return !visible&&distance>10;
    case AbilityKind.Charge:return visible&&distance>3&&distance<9&&Mathf.Abs(rise)<1;
    case AbilityKind.Stampede:return visible&&distance>3&&distance<13&&Mathf.Abs(rise)<1;
    case AbilityKind.Leap:return visible&&distance>5&&distance<10;
    case AbilityKind.Headbutt:return visible&&distance<2.6f;
    case AbilityKind.Bite:return visible&&distance<6;
    case AbilityKind.Roar:return distance<Abilities.RoarRadius-.8f;
    case AbilityKind.Dive:return visible&&distance>4&&distance<9;
    case AbilityKind.Slam:return distance<Abilities.SlamRadius-1.2f;
    case AbilityKind.Hunt:return visible&&distance<8;
    // Humans
    case AbilityKind.Kickboard:return fleeing&&distance<7;
    case AbilityKind.Hook:return fleeing&&distance<6;
    case AbilityKind.Umbrella:return distance<2.8f||rise<-2&&distance<6;
    case AbilityKind.Helmet:return distance<8;
    case AbilityKind.Banana:return distance<6;
    case AbilityKind.Scarecrow:return distance>5&&distance<14;
    case AbilityKind.SpringPad:return distance<4.5f;
    case AbilityKind.Whistle:return near(Abilities.WhistleRadius-.5f)>0;
    case AbilityKind.Lasso:return visible&&distance<Mathf.Min(7,Abilities.LassoRange-1);
    case AbilityKind.BigBubble:return visible&&distance<Mathf.Min(7,Abilities.BubbleRange-1);
    case AbilityKind.IceRay:return visible&&distance<Mathf.Min(7,Abilities.IceRange-1);
    case AbilityKind.WaterCannon:return visible&&distance<Abilities.CannonRange-1;
    case AbilityKind.Smoke:return distance<7;
    case AbilityKind.ZeroG:return distance<Abilities.ZeroGRadius-.8f;
    case AbilityKind.Balloon:return distance<4.5f;
    case AbilityKind.Vaccine:return distance<3.2f;
    default:return false;
   }
  }
  public void Step(UnityPlaytestSession game,float seconds) {
   var Shelters=game.ShelterPoints;var state=game.Observe();var rules=game.Session.ObserveSettings().Current;routes.BeginStep(seconds);clock+=seconds;
   // Animals share out the humans: each takes the nearest one that is not already well covered, so a pack
   // spreads over several targets instead of all piling onto the closest body.
   var hunted=new Dictionary<int,List<int>>();var prey=new Dictionary<int,PlayerState>();
   foreach(var a in state.Players.Where(p=>(p.IsBot||game.BotsDriveEveryone)&&p.Faction==Faction.Animal)) {
    var at=V(a.Position);PlayerState best=null;float score=float.MaxValue;
    // A human inside smoke is hard to pick out: the animal prefers anyone it can see.
    foreach(var h in state.Players.Where(p=>p.Faction==Faction.Human)){float s=Vector3.Distance(at,V(h.Position))+SpreadPenalty*(hunted.TryGetValue(h.Slot,out var l)?l.Count:0)+(PlaytestSession.Smoked(state,h,a)?SmokePenalty:0);if(s<score){score=s;best=h;}}
    if(best==null)continue;
    // A scarecrow nearer than the real prey fools the animal: it runs over and swings at straw.
    var decoy=state.Gadgets?.Where(g=>g.Kind==AbilityKind.Scarecrow&&Vector3.Distance(at,V(g.Position))<Mathf.Min(DecoyRange,Vector3.Distance(at,V(best.Position)))).OrderBy(g=>Vector3.Distance(at,V(g.Position))).FirstOrDefault();
    if(decoy!=null){prey[a.Slot]=new PlayerState{Slot=-1-decoy.Id,Faction=Faction.Human,Position=decoy.Position};continue;}
    prey[a.Slot]=best;if(!hunted.TryGetValue(best.Slot,out var list))hunted[best.Slot]=list=new List<int>();list.Add(a.Slot);
   }
   foreach(var player in state.Players) {
    if(!player.IsBot&&!game.BotsDriveEveryone)continue;
    var brain=brains[player.Slot];var position=V(player.Position);
    if(brain.Round!=state.Round||brain.Faction!=player.Faction) {brain.Round=state.Round;brain.Faction=player.Faction;brain.Replan=0;brain.Path.Clear();brain.Stuck=0;brain.Shelter=player.Slot%3;brain.Flee=0;brain.ProgressTime=0;brain.LastDistance=float.MaxValue;brain.Aim.Reset();brain.AvoidTime=0;}
    if(state.Phase==RoundPhase.Results){game.SubmitInput(player.Slot,new PlayerInput{RoundId=state.Round});continue;}
    var target=player.Faction==Faction.Animal&&prey.TryGetValue(player.Slot,out var chosen)?chosen:state.Players.Where(p=>p.Faction!=player.Faction&&!Disguised(p)).OrderBy(p=>(V(p.Position)-position).sqrMagnitude).FirstOrDefault();
    float distance=target==null?float.MaxValue:Vector3.Distance(position,V(target.Position));
    brain.Flee-=seconds;brain.Replan-=seconds;brain.AvoidTime-=seconds;
    Vector3 goal;
    if(player.Faction==Faction.Human) {
     if(distance<4 && brain.Flee<=0) {brain.Shelter=Enumerable.Range(0,3).Where(i=>i!=brain.Shelter).OrderByDescending(i=>(Shelters[i]-V(target.Position)).sqrMagnitude).First();brain.Flee=5;brain.Replan=0;}
     goal=Shelters[brain.Shelter]+new Vector3((player.Slot/3-1.5f)*.75f,0,0);
     // At the shelter a person never stands like a statue: every few seconds they shift half a step.
     brain.FidgetTime-=seconds;
     if(brain.FidgetTime<=0){float a=Hash(player.Slot,clock)*Mathf.PI*2,r=.35f+Hash(player.Slot+7,clock)*.55f;brain.Fidget=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);brain.FidgetTime=2.2f+Hash(player.Slot+13,clock)*2.4f;}
     if(brain.Flee<=0&&Flat(goal-position).magnitude<2.5f)goal+=brain.Fidget;
    } else if(target==null)goal=position;
    else {
     var prey3=V(target.Position);goal=prey3;var toward=Flat(prey3-position);float d=toward.magnitude;var dir=d>.01f?toward/d:Vector3.forward;var side=Vector3.Cross(Vector3.up,dir)*(player.Slot%2==0?1:-1);
     // Several animals on one human close in from different sides rather than queueing behind each other.
     bool packed=hunted.TryGetValue(target.Slot,out var pack)&&pack.Count>1;
     // From far off each pack member already heads for its own side of the human (straight on, left, right,
     // behind), so the pack arrives as a pincer instead of a queue. The side is fixed when the hunt starts.
     if(packed&&d>=7&&d<36) {
      if(brain.ApproachPrey!=target.Slot){brain.ApproachPrey=target.Slot;var from=d>.01f?-dir:Vector3.forward;brain.ApproachDir=Quaternion.Euler(0,Sectors[pack.IndexOf(player.Slot)%Sectors.Length],0)*from;}
      var flank=prey3+brain.ApproachDir*Mathf.Min(d*.55f,7);
      if(HasFloor(flank,prey3.y))goal=flank;
     } else if(!packed||d<7)brain.ApproachPrey=-1;
     if(packed&&d>1.8f&&d<7){float a=(pack.IndexOf(player.Slot)*Mathf.PI*2/pack.Count)+target.Slot;goal=prey3+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*1.3f;}
     // The wolf circles in from the side.
     else if(player.CharacterId=="animal-wolf"&&d>4&&d<14)goal=prey3+side*3.5f-dir*1;
    }
    if(brain.Replan<=0 && brain.Stuck>2){routes.Invalidate();brain.Stuck=.8f;}
    // An animal held at a choke point (bodies, shots) marks the spot just ahead and plans round it for a while:
    // a blocked staircase sends it to the far ramp instead of pushing into the same wall of humans.
    bool crowded=state.Players.Any(o=>o.Slot!=player.Slot&&Vector3.Distance(V(o.Position),position)<2.5f);
    if(player.Faction==Faction.Animal&&crowded&&brain.Stuck>1.2f&&brain.AvoidTime<=0&&brain.Cursor<brain.Path.Count){brain.Avoid=Vector3.Lerp(position,brain.Path[brain.Cursor],.5f);brain.AvoidTime=AvoidSeconds;brain.Replan=0;brain.Stuck=0;}
    // A human chased on the deck may jump down through a railing gap when that is the shorter way out.
    bool jumpDown=player.Faction==Faction.Human&&brain.Flee>0&&position.y>1.5f;
    if(brain.Replan<=0) {
     // Teammates' routes cost extra: the next bot takes a parallel street or the other stair when that is not
     // much longer, instead of walking in the first one's footsteps. A single bridge is still used by all.
     routes.Share(brains.Where((b,i)=>i!=player.Slot&&b.Round==state.Round&&b.Faction==player.Faction).Select(b=>b.Ids));
     brain.Path=routes.Find(position,goal,out bool reachable,jumpDown,brain.AvoidTime>0?brain.Avoid:(Vector3?)null,true);
     if(!reachable) {
      routes.Invalidate();
      if(player.Faction==Faction.Human) {
       for(int offset=1;offset<Shelters.Count;offset++) {
        int alternate=(brain.Shelter+offset)%Shelters.Count;
        var alternative=Shelters[alternate]+new Vector3((player.Slot/3-1.5f)*.75f,0,0);
        var path=routes.Find(position,alternative,out bool success,jumpDown);
        if(success){brain.Shelter=alternate;brain.Path=path;goal=alternative;break;}
       }
      } else {
       foreach(var human in state.Players.Where(p=>p.Faction==Faction.Human).OrderBy(p=>(V(p.Position)-position).sqrMagnitude)) {
        var path=routes.Find(position,V(human.Position),out bool success);
        if(success){target=human;goal=V(human.Position);brain.Path=path;break;}
       }
      }
     }
     brain.Ids.Clear();brain.Ids.AddRange(routes.LastIds);
     brain.Path=routes.Smooth(position,brain.Path);
     brain.Cursor=0;brain.Replan=.8f+player.Slot*.013f;
    }
    while(brain.Cursor<brain.Path.Count && Flat(brain.Path[brain.Cursor]-position).magnitude<.32f)brain.Cursor++;
    var move=brain.Cursor<brain.Path.Count?Flat(brain.Path[brain.Cursor]-position).normalized:Vector3.zero;
    // Round corners: close to a waypoint, start turning toward the one after it.
    if(brain.Cursor+1<brain.Path.Count){float near=Flat(brain.Path[brain.Cursor]-position).magnitude;
     // A waypoint pair that doubles back cancels out; then keep heading for the current waypoint.
     if(near<CornerRadius){var rounded=Vector3.Lerp(Flat(brain.Path[brain.Cursor+1]-position).normalized,move,near/CornerRadius);if(rounded.magnitude>.3f)move=rounded.normalized;}}
    float remaining=Flat(goal-position).magnitude;
    if(remaining<.4f)move=Vector3.zero;
    // Ease into a fixed destination (shelter) like a person; CharacterMotion adds the body's own inertia. Animals keep charging.
    else if(player.Faction==Faction.Human)move*=Mathf.Clamp(remaining/ArrivalRadius,MinArrivalInput,1);
    brain.ProgressTime+=seconds;
    if(brain.ProgressTime>=.75f) {
     float goalDistance=Vector3.Distance(position,goal);
     if(goalDistance>.6f && brain.LastDistance-goalDistance<.15f)brain.Stuck+=brain.ProgressTime;
     else brain.Stuck=Mathf.Max(0,brain.Stuck-brain.ProgressTime);
     brain.LastDistance=goalDistance;brain.Last=position;brain.ProgressTime=0;
    }
    foreach(var other in state.Players) {
     if(other.Slot==player.Slot)continue;var away=Flat(position-V(other.Position));
     if(away.magnitude<1.05f && away.magnitude>.02f && Mathf.Abs(position.y-other.Position.Y)<1)
      move+=away.normalized*(1.05f-away.magnitude)*1.5f;
    }
    if(brain.Stuck>.8f)move+=Vector3.Cross(move,Vector3.up)*(player.Slot%2==0?1:-1);
    bool hop=false;
    if(target!=null) {
     var toward=Flat(V(target.Position)-position);var across=Vector3.Cross(Vector3.up,toward.normalized);
     if(player.Faction==Faction.Human) {
      // Shooting from a shelter: drift side to side instead of standing still.
      if(brain.Flee<=0&&distance>4&&distance<18&&Flat(goal-position).magnitude<2.5f)move+=across*Mathf.Sin((float)clock*1.6f+player.Slot)*.5f;
     } else {
      brain.Charge-=seconds;brain.Hop-=seconds;
      switch(player.CharacterId) {
       case "animal-boar":
        if(player.Ability==AbilityKind.Charge&&rules.Abilities)break;
        // A boar lowers its head and charges in a straight line, often past where the human was.
        if(brain.Charge<=-2.5f&&distance>2.5f&&distance<9){brain.Charge=1.1f;brain.ChargeDir=toward.normalized;}
        if(brain.Charge>0)move=brain.ChargeDir;
        break;
       case "animal-rabbit_brown":
        if(move.sqrMagnitude>.2f&&distance>2.2f&&brain.Hop<=0){hop=true;brain.Hop=.9f+(player.Slot%3)*.15f;}
        break;
       case "animal-fox":
        if(distance>3)move+=across*Mathf.Sin((float)clock*5+player.Slot)*.4f;
        break;
      }
     }
    }
    move=Vector3.ClampMagnitude(move,1);
    // Crowd spacing and stuck side-steps must not carry a bot over a bottomless edge (sky bridges, the chasm, the map rim).
    // A route that plans a jump down keeps it; otherwise the bot heads straight for its waypoint, or stops.
    // Judged from the floor under the bot, not its body: a bot in mid-jump has not planned a drop.
    float floor=FloorBelow(position);bool plannedDrop=false;for(int k=brain.Cursor;k<Mathf.Min(brain.Path.Count,brain.Cursor+3);k++)plannedDrop|=floor-brain.Path[k].y>.6f;
    if(!plannedDrop&&!FloorAhead(position,move)) {
     var heading=brain.Cursor<brain.Path.Count?Flat(brain.Path[brain.Cursor]-position):Vector3.zero;
     move=heading.sqrMagnitude>.01f&&FloorAhead(position,heading.normalized*move.magnitude)?heading.normalized*move.magnitude:Vector3.zero;
    }
    Vector3 aim=target==null?move:V(target.Position)-position;
    float yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg,pitch=-Mathf.Atan2(aim.y,Flat(aim).magnitude)*Mathf.Rad2Deg;
    bool scanning=player.Faction==Faction.Human&&(target==null||distance>Tools.Range(player.Tool,rules)+6)&&Flat(goal-position).magnitude<2.5f;
    bool visible=target!=null;
    // Linecast normally ends at the target controller; accept that nearest hit.
    if(target!=null && target.Slot>=0 && Physics.Linecast(position+Vector3.up*.9f,V(target.Position)+Vector3.up*.9f,out var sight,Layers.World,QueryTriggerInteraction.Ignore)) visible=sight.collider.transform==game.PlayerTransform(target.Slot);
    // Human-like aim: lags the seen heading and wanders slightly; zero settings return the exact aim above.
    bool canFire=visible;
    if(target!=null){var aimed=brain.Aim.Step(clock,target.Slot,visible,yaw,pitch,rules.BotAimDelaySeconds,rules.BotAimErrorDegrees);yaw=aimed.Yaw;pitch=aimed.Pitch;canFire=aimed.CanFire;}
    // Nothing in reach: the human looks around the approaches instead of staring at one spot.
    if(scanning){yaw+=Mathf.Sin((float)clock*.45f+player.Slot*1.3f)*65;pitch=-4;canFire=false;}
    // A shaken human (acorn, slam) cannot hold their aim.
    if(player.ShakeRemaining>0){yaw+=Mathf.Sin((float)clock*23+player.Slot)*14;pitch+=Mathf.Cos((float)clock*19)*8;}
    // Smoke: the animal knows roughly where, not exactly; it weaves about and cannot strike until it is right there.
    if(player.Faction==Faction.Animal&&target!=null&&target.Slot>=0&&PlaytestSession.Smoked(state,target,player)){canFire=false;move=Quaternion.Euler(0,Mathf.Sin((float)clock*1.7f+player.Slot*2)*80,0)*move*.7f;}
    // A fox in its box holds still; that is the whole trick.
    if(Disguised(player)){move=Vector3.zero;canFire=false;hop=false;}
    bool ability=false;brain.AbilityWait-=seconds;
    if(rules.Abilities&&state.Phase==RoundPhase.Chase&&target!=null&&player.Ability!=AbilityKind.None&&player.AbilityCooldownRemaining<=0&&player.AbilityActiveRemaining<=0&&brain.AbilityWait<=0) {
     var away=move.sqrMagnitude>.04f?move.normalized:-Flat(V(target.Position)-position).normalized;
     ability=WantsAbility(state,player,target,distance,visible,brain.Flee>0,position);
     // People react a moment late, and not on the same frame as each other.
     if(ability){brain.AbilityWait=.6f+Hash(player.Slot+3,clock)*.8f;
      // A kickboard or a hook goes where the body is heading, not where the eyes are (on the animal).
      if(player.Ability==AbilityKind.Kickboard||player.Ability==AbilityKind.Hook){yaw=Mathf.Atan2(away.x,away.z)*Mathf.Rad2Deg;pitch=-25;}}
    }
    // Movement input is relative to the submitted aim, so aim lag never bends the path.
    var local=Quaternion.Euler(0,-yaw,0)*move;
    game.SubmitInput(player.Slot,new PlayerInput {Right=local.x,Forward=local.z,Yaw=yaw,
     Pitch=pitch,
     Jump=brain.Stuck>1.5f||hop,Attack=target!=null&&canFire&&distance<(player.Faction==Faction.Human?Tools.Range(player.Tool,rules):CombatRules.MeleeDistance-.15f),
     Reload=player.Faction==Faction.Human&&Tools.UsesAmmo(player.Tool)&&player.Ammo==0,Ability=ability,RoundId=state.Round});
   }
  }
  static float FloorBelow(Vector3 position) {
   float best=float.NegativeInfinity;
   foreach(var hit in Physics.RaycastAll(position+Vector3.up*.3f,Vector3.down,6,Layers.World,QueryTriggerInteraction.Ignore))
    if(!(hit.collider is CharacterController)&&hit.collider.GetComponentInParent<CharacterController>()==null)best=Mathf.Max(best,hit.point.y);
   return float.IsNegativeInfinity(best)?position.y:best;
  }
  /// <summary>How much farther (m) an animal will go for a human nobody else is chasing.</summary>
  public const float SpreadPenalty=4;
  /// <summary>Approach sides (degrees from the line the animal starts on) handed out to a pack in order.</summary>
  static readonly float[] Sectors={0,75,-75,145,-145};
  /// <summary>
  /// Extra cost per metre, for each teammate route on or next to a cell (counted up to 2). 0 turns route sharing
  /// off; tests compare against it.
  /// </summary>
  public static float SharePenalty=.8f;
  /// <summary>True when there is ground near this height under a point (a flank point is not over a void).</summary>
  static bool HasFloor(Vector3 point,float height) {
   foreach(var hit in Physics.RaycastAll(new Vector3(point.x,height+3,point.z),Vector3.down,6,Layers.World,QueryTriggerInteraction.Ignore))
    if(!(hit.collider is CharacterController)&&hit.collider.GetComponentInParent<CharacterController>()==null&&Mathf.Abs(hit.point.y-height)<2.5f)return true;
   return false;
  }
  /// <summary>A repeatable 0..1 number for this slot and moment (bots stay deterministic for tests).</summary>
  static float Hash(int slot,double time){var x=Mathf.Sin((float)(slot*12.9898+Math.Floor(time*10)*78.233))*43758.547f;return x-Mathf.Floor(x);}
  /// <summary>True when a body taking this step still lands on a floor (a ledge up to 6 m is fine; a void is not).</summary>
  static bool FloorAhead(Vector3 position,Vector3 move) {
   if(move.sqrMagnitude<.0025f)return true;
   var probe=position+Vector3.up*.6f+Flat(move).normalized*.75f;
   foreach(var hit in Physics.RaycastAll(probe,Vector3.down,6.6f,Layers.World,QueryTriggerInteraction.Ignore))
    if(!(hit.collider is CharacterController)&&hit.collider.GetComponentInParent<CharacterController>()==null)return true;
   return false;
  }
  const float ArrivalRadius=1.6f,MinArrivalInput=.25f,CornerRadius=.6f,AvoidSeconds=6;
  /// <summary>Extra route cost (metres) for each cell inside a bot's avoided choke point.</summary>
  public const float AvoidRadius=3,AvoidPenalty=40;
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  static Vector3 Flat(Vector3 p)=>new Vector3(p.x,0,p.z);
 }
 /// <summary>
 /// Where the bot route grid lies for one map: corner (OriginX, OriginZ), cell size, cell counts, and how many
 /// floors one cell may hold (2 lets a bridge and the courtyard under it both be walkable).
 /// </summary>
 public readonly struct RouteGrid {
  public readonly float OriginX,OriginZ,Cell,Top;public readonly int SizeX,SizeZ,Layers;
  /// <param name="top">Height the grid rays start from: above every floor and roof of the map.</param>
  public RouteGrid(float originX,float originZ,float cell,int sizeX,int sizeZ,int layers,float top=12){OriginX=originX;OriginZ=originZ;Cell=cell;SizeX=sizeX;SizeZ=sizeZ;Layers=layers;Top=top;}
  /// <summary>The village and the warehouse: 90 m square round the centre, one floor per cell.</summary>
  public static readonly RouteGrid Default=new RouteGrid(-45,-45,.75f,121,121,1);
 }
 // A small terrain-derived grid supports the roof staircase and both ground shelters.
 sealed class VillageRoutes {
  RouteGrid grid=RouteGrid.Default;int Cells,Nodes;
  Vector3[] points;bool[] walkable;float[] distance;int[] parent;bool[] closed;byte[] share;bool[] edge;long planned,sharedCells;
  public VillageRoutes(){Allocate();}
  void Allocate(){Cells=grid.SizeX*grid.SizeZ;Nodes=Cells*grid.Layers;points=new Vector3[Nodes];walkable=new bool[Nodes];distance=new float[Nodes];parent=new int[Nodes];closed=new bool[Nodes];share=new byte[Nodes];edge=new bool[Nodes];}
  bool built,rebuildRequested,rebuiltThisStep;float sinceBuild=1;int builds,thisStep,maxPerStep;
  public BotNavigationDiagnostics Observe()=>new BotNavigationDiagnostics{GridBuilds=builds,MaxGridBuildsPerStep=maxPerStep,PlannedCells=planned,SharedCells=sharedCells};
  /// <summary>Grid nodes of the last <see cref="Find"/> result, start to goal.</summary>
  public readonly List<int> LastIds=new List<int>();
  const int ShareCells=2;const float EdgePenalty=2.5f;
  /// <summary>Marks the cells on and within 1.5 m of these routes (same floor only) for the next shared <see cref="Find"/>.</summary>
  public void Share(IEnumerable<List<int>> routes) {
   Array.Clear(share,0,share.Length);
   foreach(var route in routes) {
    if(route.Count==0)continue;
    var mark=new HashSet<int>();
    foreach(int id in route) {
     if(id<0||id>=Nodes||!walkable[id])continue;int cell=id%Cells,x=cell%grid.SizeX,z=cell/grid.SizeX;float y=points[id].y;
     for(int dz=-ShareCells;dz<=ShareCells;dz++)for(int dx=-ShareCells;dx<=ShareCells;dx++){int nx=x+dx,nz=z+dz;if(nx<0||nx>=grid.SizeX||nz<0||nz>=grid.SizeZ)continue;
      for(int l=0;l<grid.Layers;l++){int n=nz*grid.SizeX+nx+l*Cells;if(walkable[n]&&Mathf.Abs(points[n].y-y)<1)mark.Add(n);}}
    }
    foreach(int n in mark)if(share[n]<255)share[n]++;
   }
  }
  readonly SortedSet<(float score,int id)> open=new SortedSet<(float,int)>();
  public void BeginStep(float seconds){sinceBuild+=seconds;rebuiltThisStep=false;thisStep=0;}
  public void Invalidate(){rebuildRequested=true;}
  public void Reset(RouteGrid next){if(!next.Equals(grid)){grid=next;Allocate();}built=false;rebuildRequested=false;}
  static bool Terrain(Collider c)=>!(c is CharacterController)&&c.GetComponentInParent<CharacterController>()==null;
  void Build() {
   builds++;maxPerStep=Math.Max(maxPerStep,++thisStep);
   Physics.SyncTransforms();Array.Clear(walkable,0,walkable.Length);
   for(int z=0;z<grid.SizeZ;z++)for(int x=0;x<grid.SizeX;x++) {
    int id=z*grid.SizeX+x,layer=0;var top=new Vector3(grid.OriginX+x*grid.Cell,grid.Top,grid.OriginZ+z*grid.Cell);
    var hits=Physics.RaycastAll(top,Vector3.down,grid.Top+10,Layers.World,QueryTriggerInteraction.Ignore).Where(h=>Terrain(h.collider)&&h.normal.y>.7f).OrderByDescending(h=>h.distance).ToArray();
    foreach(var hit in hits) {
     var p=hit.point;
     // Reject a floor hidden under a low stair. The standing capsule alone leaves
     // foot clearance and can otherwise connect ground directly to a .5m step.
     if(Physics.OverlapSphere(p+Vector3.up*.05f,.015f,Layers.World,QueryTriggerInteraction.Ignore).Any(Terrain))continue;
     if(Physics.OverlapCapsule(p+Vector3.up*.75f,p+Vector3.up*1.47f,.32f,Layers.World,QueryTriggerInteraction.Ignore).Any(Terrain))continue;
     // A second floor counts only well above the first (a bridge over a street), never a step or a crate top.
     if(layer>0&&p.y-points[id+(layer-1)*Cells].y<2)continue;
     points[id+layer*Cells]=p;walkable[id+layer*Cells]=true;if(++layer>=grid.Layers)break;
    }
   }
   // Edge cells: a neighbour with no floor within 6 m below (a sky bridge rim, the chasm). Spreading out never
   // pushes a bot onto them, so making room for a teammate does not walk anyone off into the void.
   for(int n=0;n<Nodes;n++) {
    edge[n]=false;if(!walkable[n])continue;int cell=n%Cells,x=cell%grid.SizeX,z=cell/grid.SizeX;
    for(int dz=-1;dz<=1&&!edge[n];dz++)for(int dx=-1;dx<=1;dx++){int nx=x+dx,nz=z+dz;if(nx<0||nx>=grid.SizeX||nz<0||nz>=grid.SizeZ||!Landing(nz*grid.SizeX+nx,points[n].y)){edge[n]=true;break;}}
   }
   built=true;rebuildRequested=false;rebuiltThisStep=true;sinceBuild=0;
  }
  // A diagonal step must not cut a corner: both side cells need a floor near this height.
  bool Floor(int cell,float y) {
   if(grid.Layers==1)return walkable[cell];
   for(int l=0;l<grid.Layers;l++){int n=cell+l*Cells;if(walkable[n]&&Mathf.Abs(points[n].y-y)<1)return true;}
   return false;
  }
  bool Landing(int cell,float y) {
   for(int l=0;l<grid.Layers;l++){int n=cell+l*Cells;if(walkable[n]&&points[n].y<y+1&&points[n].y>y-6.5f)return true;}
   return false;
  }
  bool Covered(int cell,float low,float high) {
   for(int l=0;l<grid.Layers;l++){int n=cell+l*Cells;if(walkable[n]&&points[n].y>low+.4f&&points[n].y<high+.5f)return true;}
   return false;
  }
  int Nearest(Vector3 p) {
   int best=-1;float score=float.MaxValue;
   for(int i=0;i<points.Length;i++)if(walkable[i]){float d=(points[i]-p).sqrMagnitude;if(d<score){score=d;best=i;}}
   return best;
  }
  static bool ClearDrop(Vector3 from,Vector3 to) {
   var delta=to-from;delta.y=0;
   return !Physics.CapsuleCastAll(from+Vector3.up*.42f,from+Vector3.up*1.42f,.36f,
    delta.normalized,delta.magnitude,Layers.World,QueryTriggerInteraction.Ignore).Any(hit=>Terrain(hit.collider));
  }
  // String pulling: replace runs of grid cells with straight segments the full body can walk, so bots stop
  // zigzagging between 8-direction cells. Only flat stretches are pulled; stairs and roof drops keep their cells.
  // Only the next stretch is smoothed because routes are re-planned every 0.8 s.
  const int SmoothedCells=18,LookAhead=12;
  public List<Vector3> Smooth(Vector3 from,List<Vector3> path) {
   if(path.Count<2)return path;
   var result=new List<Vector3>();var anchor=from;int i=0,limit=Math.Min(path.Count,SmoothedCells);
   while(i<limit) {
    int reach=i;
    for(int k=i+1;k<limit&&k<=i+LookAhead;k++){if(!Straight(anchor,path,i,k))break;reach=k;}
    result.Add(path[reach]);anchor=path[reach];i=reach+1;
   }
   for(;i<path.Count;i++)result.Add(path[i]);
   return result;
  }
  bool Straight(Vector3 anchor,List<Vector3> path,int first,int last) {
   var end=path[last];var flat=end-anchor;flat.y=0;float length=flat.magnitude;if(length<.01f)return true;var heading=flat/length;
   bool sloped=Mathf.Abs(end.y-anchor.y)>.15f;
   for(int m=first;m<=last;m++){
    var previous=m==first?anchor:path[m-1];if(Mathf.Abs(path[m].y-previous.y)>.36f)return false;
    var offset=path[m]-anchor;offset.y=0;float along=Mathf.Clamp(Vector3.Dot(offset,heading),0,length);
    // Stay on an even floor or a steady stair slope, and inside the corridor of walkable cells.
    if(Mathf.Abs(path[m].y-Mathf.Lerp(anchor.y,end.y,along/length))>.2f)return false;
    if(m<last&&(offset-heading*along).magnitude>.55f)return false;
   }
   // The body sweep uses the grid's own clearance radius; on a stair slope it rides above the step edges.
   var direction=(end-anchor).normalized;float lift=sloped?.75f:.42f;
   return !Physics.CapsuleCastAll(anchor+Vector3.up*lift,anchor+Vector3.up*1.42f,.33f,direction,Vector3.Distance(anchor,end),Layers.World,QueryTriggerInteraction.Ignore).Any(hit=>Terrain(hit.collider));
  }
  public List<Vector3> Find(Vector3 from,Vector3 to,out bool reachable,bool allowDrop=false,Vector3? avoid=null,bool shared=false) {
   LastIds.Clear();
   if(!rebuiltThisStep && (!built || rebuildRequested&&sinceBuild>=1))Build();
   int start=Nearest(from),goal=Nearest(to);var result=new List<Vector3>();reachable=false;if(start<0||goal<0)return result;
   for(int i=0;i<distance.Length;i++){distance[i]=float.MaxValue;parent[i]=-1;closed[i]=false;}open.Clear();
   distance[start]=0;open.Add((0,start));int nearestReachable=start;
   while(open.Count>0) {
    var current=open.Min;open.Remove(current);int id=current.id;if(closed[id])continue;closed[id]=true;if((points[id]-to).sqrMagnitude<(points[nearestReachable]-to).sqrMagnitude)nearestReachable=id;if(id==goal)break;
    int cell=id%Cells,x=cell%grid.SizeX,z=cell/grid.SizeX;
    for(int span=1;span<=(allowDrop?4:1);span++)for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)for(int layer=0;layer<grid.Layers;layer++) {
     if(dx==0&&dz==0)continue;int nx=x+dx*span,nz=z+dz*span;if(nx<0||nx>=grid.SizeX||nz<0||nz>=grid.SizeZ)continue;int next=nz*grid.SizeX+nx+layer*Cells;
     if(!walkable[next]||closed[next]||points[next].y-points[id].y>.36f||points[id].y-points[next].y>4.2f)continue;
     if(span>1) {
      // A roof can be an isolated grid island: its steep eave has no standable cells.
      // Permit a one-way walk-off only when the full body can clear the edge at roof height.
      if(points[id].y-points[next].y<.4f||!ClearDrop(points[id],points[next]))continue;
     } else if(dx!=0&&dz!=0&&(!Floor(z*grid.SizeX+nx,points[id].y)||!Floor(nz*grid.SizeX+x,points[id].y)))continue;
     // On a layered grid a drop must really fall: nothing to land on first (the bridge deck itself) and no parapet in the way.
     if(grid.Layers>1&&points[id].y-points[next].y>.4f&&(Covered(nz*grid.SizeX+nx,points[next].y,points[id].y)||span==1&&!ClearDrop(points[id],points[next])))continue;
     float step=Vector3.Distance(points[id],points[next]);
     float cost=distance[id]+step*(shared&&BotDirector.SharePenalty>0?1+BotDirector.SharePenalty*Mathf.Min(share[next],2)+(edge[next]?EdgePenalty:0):1);
     if(avoid.HasValue&&(points[next]-avoid.Value).sqrMagnitude<BotDirector.AvoidRadius*BotDirector.AvoidRadius)cost+=BotDirector.AvoidPenalty;
     if(cost>=distance[next])continue;
     distance[next]=cost;parent[next]=id;open.Add((cost+Vector3.Distance(points[next],points[goal]),next));
    }
   }
   reachable=start==goal||parent[goal]>=0;
   if(!reachable&&!allowDrop)return Find(from,to,out reachable,true,avoid,shared);
   if(!reachable)goal=nearestReachable;
   for(int id=goal;id!=start;id=parent[id]){if(id<0){LastIds.Clear();return new List<Vector3>();}result.Add(points[id]);LastIds.Add(id);}result.Reverse();LastIds.Reverse();
   if(shared){planned+=LastIds.Count;foreach(int id in LastIds)if(share[id]>0)sharedCells++;}
   return result;
  }
 }
}
