using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AvH {
 // Generates normal player inputs. No teleport, hit claim, or faction mutation exists here.
 public sealed class BotDirector {
  public static readonly Vector3[] Shelters={new Vector3(-12,4,10),new Vector3(14,0,15),new Vector3(9,0,-14)};
  sealed class Brain {public int Shelter,Round;public Faction Faction;public float Replan,Flee,Stuck;public Vector3 Last;public List<Vector3> Path=new List<Vector3>();public int Cursor;}
  readonly Brain[] brains=Enumerable.Range(0,12).Select(i=>new Brain{Shelter=i%3}).ToArray();
  readonly VillageRoutes routes=new VillageRoutes();
  float time;
  public void Step(UnityPlaytestSession game,float seconds) {
   var state=game.Observe();var rules=game.Session.ObserveSettings().Current;time+=seconds;
   foreach(var player in state.Players) {
    if(!player.IsBot)continue;
    var brain=brains[player.Slot];var position=V(player.Position);
    if(brain.Round!=state.Round||brain.Faction!=player.Faction) {brain.Round=state.Round;brain.Faction=player.Faction;brain.Replan=0;brain.Path.Clear();brain.Stuck=0;brain.Shelter=player.Slot%3;brain.Flee=0;}
    if(state.Phase==RoundPhase.Results){game.SubmitInput(player.Slot,new PlayerInput{RoundId=state.Round});continue;}
    var target=state.Players.Where(p=>p.Faction!=player.Faction).OrderBy(p=>(V(p.Position)-position).sqrMagnitude).FirstOrDefault();
    float distance=target==null?float.MaxValue:Vector3.Distance(position,V(target.Position));
    brain.Flee-=seconds;brain.Replan-=seconds;
    Vector3 goal;
    if(player.Faction==Faction.Human) {
     if(distance<4 && brain.Flee<=0) {brain.Shelter=Enumerable.Range(0,3).Where(i=>i!=brain.Shelter).OrderByDescending(i=>(Shelters[i]-V(target.Position)).sqrMagnitude).First();brain.Flee=5;brain.Replan=0;}
     goal=Shelters[brain.Shelter]+new Vector3(((player.Slot/3)%3-1)*.85f,0,0);
    } else goal=target==null?position:V(target.Position);
    if(brain.Replan<=0 && brain.Stuck>2){routes.Invalidate();brain.Stuck=.8f;}
    if(brain.Replan<=0) {brain.Path=routes.Find(position,goal);brain.Cursor=0;brain.Replan=.8f+player.Slot*.013f;}
    while(brain.Cursor<brain.Path.Count && Flat(brain.Path[brain.Cursor]-position).magnitude<.32f)brain.Cursor++;
    var move=brain.Cursor<brain.Path.Count?Flat(brain.Path[brain.Cursor]-position).normalized:Vector3.zero;
    if(Flat(goal-position).magnitude<.4f)move=Vector3.zero;
    if(move.sqrMagnitude>.1f && Flat(position-brain.Last).magnitude<seconds*.2f)brain.Stuck+=seconds;else brain.Stuck=Mathf.Max(0,brain.Stuck-seconds*2);
    brain.Last=position;
    foreach(var other in state.Players) {
     if(other.Slot==player.Slot)continue;var away=Flat(position-V(other.Position));
     if(away.magnitude<1.05f && away.magnitude>.02f && Mathf.Abs(position.y-other.Position.Y)<1)
      move+=away.normalized*(1.05f-away.magnitude)*1.5f;
    }
    if(brain.Stuck>.8f)move+=Vector3.Cross(move,Vector3.up)*(player.Slot%2==0?1:-1);
    move=Vector3.ClampMagnitude(move,1);
    Vector3 aim=target==null?move:V(target.Position)-position;
    float yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;
    var local=Quaternion.Euler(0,-yaw,0)*move;
    bool visible=target!=null && !Physics.Linecast(position+Vector3.up*.9f,V(target.Position)+Vector3.up*.9f,out var hit,~0,QueryTriggerInteraction.Ignore);
    // Linecast normally ends at the target controller; accept that nearest hit.
    if(target!=null && Physics.Linecast(position+Vector3.up*.9f,V(target.Position)+Vector3.up*.9f,out var sight,~0,QueryTriggerInteraction.Ignore)) visible=sight.collider.transform==game.PlayerTransform(target.Slot);
    game.SubmitInput(player.Slot,new PlayerInput {Right=local.x,Forward=local.z,Yaw=yaw,
     Pitch=-Mathf.Atan2(aim.y,Flat(aim).magnitude)*Mathf.Rad2Deg,
     Jump=brain.Stuck>1.5f,Attack=target!=null&&visible&&distance<(player.Faction==Faction.Human?rules.BubbleRange:1.65f),
     Reload=player.Faction==Faction.Human&&player.Ammo==0,RoundId=state.Round});
   }
  }
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  static Vector3 Flat(Vector3 p)=>new Vector3(p.x,0,p.z);
 }
 // A small terrain-derived grid supports the roof staircase and both ground shelters.
 sealed class VillageRoutes {
  const int Size=89;const float Cell=.5f,Origin=-22;
  readonly Vector3[] points=new Vector3[Size*Size];readonly bool[] walkable=new bool[Size*Size];
  bool built;
  public void Invalidate(){built=false;Array.Clear(walkable,0,walkable.Length); }
  static bool Terrain(Collider c)=>!(c is CharacterController)&&c.GetComponentInParent<CharacterController>()==null;
  void Build() {
   Physics.SyncTransforms();
   for(int z=0;z<Size;z++)for(int x=0;x<Size;x++) {
    int id=z*Size+x;var top=new Vector3(Origin+x*Cell,12,Origin+z*Cell);
    var hits=Physics.RaycastAll(top,Vector3.down,15,~0,QueryTriggerInteraction.Ignore).Where(h=>Terrain(h.collider)&&h.normal.y>.7f).OrderByDescending(h=>h.distance).ToArray();
    foreach(var hit in hits) {
     var p=hit.point;
     if(Physics.OverlapCapsule(p+Vector3.up*.75f,p+Vector3.up*1.47f,.32f,~0,QueryTriggerInteraction.Ignore).Any(Terrain))continue;
     points[id]=p;walkable[id]=true;break;
    }
   }
   built=true;
  }
  int Nearest(Vector3 p) {
   int best=-1;float score=float.MaxValue;
   for(int i=0;i<points.Length;i++)if(walkable[i]){float d=(points[i]-p).sqrMagnitude;if(d<score){score=d;best=i;}}
   return best;
  }
  public List<Vector3> Find(Vector3 from,Vector3 to) {
   if(!built)Build();int start=Nearest(from),goal=Nearest(to);var result=new List<Vector3>();if(start<0||goal<0)return result;
   var distance=Enumerable.Repeat(float.MaxValue,points.Length).ToArray();var parent=Enumerable.Repeat(-1,points.Length).ToArray();var closed=new bool[points.Length];
   var open=new SortedSet<(float score,int id)>();distance[start]=0;open.Add((0,start));
   while(open.Count>0) {
    var current=open.Min;open.Remove(current);int id=current.id;if(closed[id])continue;closed[id]=true;if(id==goal)break;
    int x=id%Size,z=id/Size;
    for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++) {
     if(dx==0&&dz==0)continue;int nx=x+dx,nz=z+dz;if(nx<0||nx>=Size||nz<0||nz>=Size)continue;int next=nz*Size+nx;
     if(!walkable[next]||closed[next]||points[next].y-points[id].y>.36f||points[id].y-points[next].y>4.2f)continue;
     if(dx!=0&&dz!=0&&(!walkable[z*Size+nx]||!walkable[nz*Size+x]))continue;
     float cost=distance[id]+Vector3.Distance(points[id],points[next]);if(cost>=distance[next])continue;
     distance[next]=cost;parent[next]=id;open.Add((cost+Vector3.Distance(points[next],points[goal]),next));
    }
   }
   if(start!=goal&&parent[goal]<0)return result;
   for(int id=goal;id!=start;id=parent[id]){if(id<0)return new List<Vector3>();result.Add(points[id]);}result.Reverse();return result;
  }
 }
}
