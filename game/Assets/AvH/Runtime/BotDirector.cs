using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AvH {
 // Generates normal player inputs. No teleport, hit claim, or faction mutation exists here.
 public struct BotNavigationDiagnostics {public int GridBuilds,MaxGridBuildsPerStep;}
 public sealed class BotDirector {
  public BotNavigationDiagnostics ObserveNavigation()=>routes.Observe();
  sealed class Brain {public int Shelter,Round;public Faction Faction;public float Replan,Flee,Stuck,ProgressTime,LastDistance;public Vector3 Last;public List<Vector3> Path=new List<Vector3>();public int Cursor;public BotAim Aim;}
  readonly Brain[] brains=Enumerable.Range(0,12).Select(i=>new Brain{Shelter=i%3,Aim=new BotAim(i+1)}).ToArray();
  double clock;
  readonly VillageRoutes routes=new VillageRoutes();
  
  public void Step(UnityPlaytestSession game,float seconds) {
   var Shelters=game.ShelterPoints;var state=game.Observe();var rules=game.Session.ObserveSettings().Current;routes.BeginStep(seconds);clock+=seconds;
   foreach(var player in state.Players) {
    if(!player.IsBot)continue;
    var brain=brains[player.Slot];var position=V(player.Position);
    if(brain.Round!=state.Round||brain.Faction!=player.Faction) {brain.Round=state.Round;brain.Faction=player.Faction;brain.Replan=0;brain.Path.Clear();brain.Stuck=0;brain.Shelter=player.Slot%3;brain.Flee=0;brain.ProgressTime=0;brain.LastDistance=float.MaxValue;brain.Aim.Reset();}
    if(state.Phase==RoundPhase.Results){game.SubmitInput(player.Slot,new PlayerInput{RoundId=state.Round});continue;}
    var target=state.Players.Where(p=>p.Faction!=player.Faction).OrderBy(p=>(V(p.Position)-position).sqrMagnitude).FirstOrDefault();
    float distance=target==null?float.MaxValue:Vector3.Distance(position,V(target.Position));
    brain.Flee-=seconds;brain.Replan-=seconds;
    Vector3 goal;
    if(player.Faction==Faction.Human) {
     if(distance<4 && brain.Flee<=0) {brain.Shelter=Enumerable.Range(0,3).Where(i=>i!=brain.Shelter).OrderByDescending(i=>(Shelters[i]-V(target.Position)).sqrMagnitude).First();brain.Flee=5;brain.Replan=0;}
     goal=Shelters[brain.Shelter]+new Vector3((player.Slot/3-1.5f)*.75f,0,0);
    } else goal=target==null?position:V(target.Position);
    if(brain.Replan<=0 && brain.Stuck>2){routes.Invalidate();brain.Stuck=.8f;}
    if(brain.Replan<=0) {
     brain.Path=routes.Find(position,goal,out bool reachable);
     if(!reachable) {
      routes.Invalidate();
      if(player.Faction==Faction.Human) {
       for(int offset=1;offset<Shelters.Count;offset++) {
        int alternate=(brain.Shelter+offset)%Shelters.Count;
        var alternative=Shelters[alternate]+new Vector3((player.Slot/3-1.5f)*.75f,0,0);
        var path=routes.Find(position,alternative,out bool success);
        if(success){brain.Shelter=alternate;brain.Path=path;goal=alternative;break;}
       }
      } else {
       foreach(var human in state.Players.Where(p=>p.Faction==Faction.Human).OrderBy(p=>(V(p.Position)-position).sqrMagnitude)) {
        var path=routes.Find(position,V(human.Position),out bool success);
        if(success){target=human;goal=V(human.Position);brain.Path=path;break;}
       }
      }
     }
     brain.Path=routes.Smooth(position,brain.Path);
     brain.Cursor=0;brain.Replan=.8f+player.Slot*.013f;
    }
    while(brain.Cursor<brain.Path.Count && Flat(brain.Path[brain.Cursor]-position).magnitude<.32f)brain.Cursor++;
    var move=brain.Cursor<brain.Path.Count?Flat(brain.Path[brain.Cursor]-position).normalized:Vector3.zero;
    // Round corners: close to a waypoint, start turning toward the one after it.
    if(brain.Cursor+1<brain.Path.Count){float near=Flat(brain.Path[brain.Cursor]-position).magnitude;
     if(near<CornerRadius)move=Vector3.Lerp(Flat(brain.Path[brain.Cursor+1]-position).normalized,move,near/CornerRadius).normalized;}
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
    move=Vector3.ClampMagnitude(move,1);
    Vector3 aim=target==null?move:V(target.Position)-position;
    float yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg,pitch=-Mathf.Atan2(aim.y,Flat(aim).magnitude)*Mathf.Rad2Deg;
    bool visible=target!=null;
    // Linecast normally ends at the target controller; accept that nearest hit.
    if(target!=null && Physics.Linecast(position+Vector3.up*.9f,V(target.Position)+Vector3.up*.9f,out var sight,~0,QueryTriggerInteraction.Ignore)) visible=sight.collider.transform==game.PlayerTransform(target.Slot);
    // Human-like aim: lags the seen heading and wanders slightly; zero settings return the exact aim above.
    bool canFire=visible;
    if(target!=null){var aimed=brain.Aim.Step(clock,target.Slot,visible,yaw,pitch,rules.BotAimDelaySeconds,rules.BotAimErrorDegrees);yaw=aimed.Yaw;pitch=aimed.Pitch;canFire=aimed.CanFire;}
    // Movement input is relative to the submitted aim, so aim lag never bends the path.
    var local=Quaternion.Euler(0,-yaw,0)*move;
    game.SubmitInput(player.Slot,new PlayerInput {Right=local.x,Forward=local.z,Yaw=yaw,
     Pitch=pitch,
     Jump=brain.Stuck>1.5f,Attack=target!=null&&canFire&&distance<(player.Faction==Faction.Human?rules.BubbleRange:CombatRules.MeleeDistance-.15f),
     Reload=player.Faction==Faction.Human&&player.Ammo==0,RoundId=state.Round});
   }
  }
  const float ArrivalRadius=1.6f,MinArrivalInput=.25f,CornerRadius=.6f;
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  static Vector3 Flat(Vector3 p)=>new Vector3(p.x,0,p.z);
 }
 // A small terrain-derived grid supports the roof staircase and both ground shelters.
 sealed class VillageRoutes {
  const int Size=121;const float Cell=.75f,Origin=-45;
  readonly Vector3[] points=new Vector3[Size*Size];readonly bool[] walkable=new bool[Size*Size];
  bool built,rebuildRequested,rebuiltThisStep;float sinceBuild=1;int builds,thisStep,maxPerStep;
  public BotNavigationDiagnostics Observe()=>new BotNavigationDiagnostics{GridBuilds=builds,MaxGridBuildsPerStep=maxPerStep};
  readonly float[] distance=new float[Size*Size];readonly int[] parent=new int[Size*Size];readonly bool[] closed=new bool[Size*Size];
  readonly SortedSet<(float score,int id)> open=new SortedSet<(float,int)>();
  public void BeginStep(float seconds){sinceBuild+=seconds;rebuiltThisStep=false;thisStep=0;}
  public void Invalidate(){rebuildRequested=true;}
  static bool Terrain(Collider c)=>!(c is CharacterController)&&c.GetComponentInParent<CharacterController>()==null;
  void Build() {
   builds++;maxPerStep=Math.Max(maxPerStep,++thisStep);
   Physics.SyncTransforms();Array.Clear(walkable,0,walkable.Length);
   for(int z=0;z<Size;z++)for(int x=0;x<Size;x++) {
    int id=z*Size+x;var top=new Vector3(Origin+x*Cell,12,Origin+z*Cell);
    var hits=Physics.RaycastAll(top,Vector3.down,15,~0,QueryTriggerInteraction.Ignore).Where(h=>Terrain(h.collider)&&h.normal.y>.7f).OrderByDescending(h=>h.distance).ToArray();
    foreach(var hit in hits) {
     var p=hit.point;
     // Reject a floor hidden under a low stair. The standing capsule alone leaves
     // foot clearance and can otherwise connect ground directly to a .5m step.
     if(Physics.OverlapSphere(p+Vector3.up*.05f,.015f,~0,QueryTriggerInteraction.Ignore).Any(Terrain))continue;
     if(Physics.OverlapCapsule(p+Vector3.up*.75f,p+Vector3.up*1.47f,.32f,~0,QueryTriggerInteraction.Ignore).Any(Terrain))continue;
     points[id]=p;walkable[id]=true;break;
    }
   }
   built=true;rebuildRequested=false;rebuiltThisStep=true;sinceBuild=0;
  }
  int Nearest(Vector3 p) {
   int best=-1;float score=float.MaxValue;
   for(int i=0;i<points.Length;i++)if(walkable[i]){float d=(points[i]-p).sqrMagnitude;if(d<score){score=d;best=i;}}
   return best;
  }
  static bool ClearDrop(Vector3 from,Vector3 to) {
   var delta=to-from;delta.y=0;
   return !Physics.CapsuleCastAll(from+Vector3.up*.42f,from+Vector3.up*1.42f,.36f,
    delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(hit=>Terrain(hit.collider));
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
   return !Physics.CapsuleCastAll(anchor+Vector3.up*lift,anchor+Vector3.up*1.42f,.33f,direction,Vector3.Distance(anchor,end),~0,QueryTriggerInteraction.Ignore).Any(hit=>Terrain(hit.collider));
  }
  public List<Vector3> Find(Vector3 from,Vector3 to,out bool reachable,bool allowDrop=false) {
   if(!rebuiltThisStep && (!built || rebuildRequested&&sinceBuild>=1))Build();
   int start=Nearest(from),goal=Nearest(to);var result=new List<Vector3>();reachable=false;if(start<0||goal<0)return result;
   for(int i=0;i<distance.Length;i++){distance[i]=float.MaxValue;parent[i]=-1;closed[i]=false;}open.Clear();
   distance[start]=0;open.Add((0,start));int nearestReachable=start;
   while(open.Count>0) {
    var current=open.Min;open.Remove(current);int id=current.id;if(closed[id])continue;closed[id]=true;if((points[id]-to).sqrMagnitude<(points[nearestReachable]-to).sqrMagnitude)nearestReachable=id;if(id==goal)break;
    int x=id%Size,z=id/Size;
    for(int span=1;span<=(allowDrop?4:1);span++)for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++) {
     if(dx==0&&dz==0)continue;int nx=x+dx*span,nz=z+dz*span;if(nx<0||nx>=Size||nz<0||nz>=Size)continue;int next=nz*Size+nx;
     if(!walkable[next]||closed[next]||points[next].y-points[id].y>.36f||points[id].y-points[next].y>4.2f)continue;
     if(span>1) {
      // A roof can be an isolated grid island: its steep eave has no standable cells.
      // Permit a one-way walk-off only when the full body can clear the edge at roof height.
      if(points[id].y-points[next].y<.4f||!ClearDrop(points[id],points[next]))continue;
     } else if(dx!=0&&dz!=0&&(!walkable[z*Size+nx]||!walkable[nz*Size+x]))continue;
     float cost=distance[id]+Vector3.Distance(points[id],points[next]);if(cost>=distance[next])continue;
     distance[next]=cost;parent[next]=id;open.Add((cost+Vector3.Distance(points[next],points[goal]),next));
    }
   }
   reachable=start==goal||parent[goal]>=0;
   if(!reachable&&!allowDrop)return Find(from,to,out reachable,true);
   if(!reachable)goal=nearestReachable;
   for(int id=goal;id!=start;id=parent[id]){if(id<0)return new List<Vector3>();result.Add(points[id]);}result.Reverse();return result;
  }
 }
}
