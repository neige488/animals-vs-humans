using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
namespace AvH {
 /// <summary>
 /// Development-only playthrough of one warehouse round in the real app, driven through the same input path as a player.
 /// <c>-avhScenario host|guest &lt;folder&gt;</c> [<c>-avhRecord</c>] [<c>-avhScenarioChase seconds</c>] [<c>-avhScenarioSeconds cap</c>].
 /// The host opens a warehouse room (11 bots), holds the stair head and shoots down it, jumps through a railing gap
 /// when an animal reaches the deck, runs out to the yard, and chases humans once caught. A guest joins with the
 /// host's room code (folder/room.txt), moves, jumps and shoots. Each writes what it observed to scenario-&lt;role&gt;.txt.
 /// </summary>
 [DefaultExecutionOrder(200)] public sealed class WarehouseScenario : MonoBehaviour {
  NetworkPresentation network;UnityPlaytestSession world;GamePresentation presentation;SessionRecorder recorder;
  string folder,role;double started,cap=330,resultsSeen=-1;float chase=-1;
  readonly StringBuilder log=new StringBuilder();
  public static bool TryStart(GamePresentation presentation,NetworkPresentation network,UnityPlaytestSession world,string[] args) {
   int at=Array.IndexOf(args,"-avhScenario");if(!Debug.isDebugBuild||at<0||at+2>=args.Length)return false;
   var s=presentation.gameObject.AddComponent<WarehouseScenario>();s.presentation=presentation;s.network=network;s.world=world;s.role=args[at+1];s.folder=args[at+2];
   Directory.CreateDirectory(s.folder);presentation.AutomatedProbeInput=true;s.started=Time.realtimeSinceStartupAsDouble;
   presentation.UseDisplaySettings(new LocalDisplaySettings(Path.Combine(s.folder,s.role+"-display.xml")));
   int c=Array.IndexOf(args,"-avhScenarioChase");if(c>=0&&c+1<args.Length)s.chase=float.Parse(args[c+1],CultureInfo.InvariantCulture);
   int m=Array.IndexOf(args,"-avhScenarioSeconds");if(m>=0&&m+1<args.Length)s.cap=double.Parse(args[m+1],CultureInfo.InvariantCulture);
   if(s.role=="host") {
    network.HostMap=Array.IndexOf(args,"-avhMap")>=0?NetworkPresentation.StartupMap():GameMaps.Warehouse;network.HostSettingsPath=Path.Combine(s.folder,"host-settings.xml");if(File.Exists(network.HostSettingsPath))File.Delete(network.HostSettingsPath);
    network.StartHost("민지","127.0.0.1");File.WriteAllText(Path.Combine(s.folder,"room.txt"),network.RoomCode);
    if(s.chase>0){world.Session.BeginSettingsEdit(0);var v=world.Session.ObserveSettings().Edit;v.RoundSeconds=s.chase;v.PreparationSeconds=12;world.Session.UpdateSettingsEdit(0,v);world.Session.ApplySettingsNow(0);}
   } else {
    var room=Path.Combine(s.folder,"room.txt");for(int i=0;i<200&&!File.Exists(room);i++)System.Threading.Thread.Sleep(50);
    network.JoinRoom(File.ReadAllText(room).Trim(),"준호");
   }
   if(Array.IndexOf(args,"-avhRecord")>=0)s.recorder=SessionRecorder.Begin(presentation.gameObject,Path.Combine(s.folder,s.role+"-capture"),20);
   s.Note($"시작 role={s.role}");return true;
  }
  double Now=>Time.realtimeSinceStartupAsDouble-started;
  int frames;double frameTime;float worstFrame;
  void Note(string text){var line=$"[{Now,6:F1}s] {text}";log.AppendLine(line);Debug.Log("[scenario] "+line);}

  // ---------- what happened (read from the public session state every viewer has) ----------
  int falls,shovedFalls;
  int lastEvent,round,shoves,localShoved,localShovedOthers,staggers,transforms,drops,dropChases,jumps,shots,joinedSlot=-1;
  readonly HashSet<int> onStairs=new HashSet<int>(),onRamp=new HashSet<int>(),detours=new HashSet<int>(),reachedDeck=new HashSet<int>();
  readonly Dictionary<int,(double at,Vector3 where)> lastHigh=new Dictionary<int,(double,Vector3)>();
  readonly Dictionary<int,double> dropped=new Dictionary<int,double>();readonly HashSet<int> chasedAfterDrop=new HashSet<int>();
  readonly List<string> shoveLines=new List<string>();readonly Dictionary<string,int> toolHits=new Dictionary<string,int>();
  float nextMoveNote=25;bool grounded=true;int jumpsSeen;readonly Queue<(double t,float y)> recentY=new Queue<(double,float)>();
  Faction localFaction;float moved;Vector3 lastLocal;bool haveLocal;float lastY;int lastAmmo=-1;
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  // The map the round is played on decides what "stairs", "ramp" and "deck" mean.
  bool Crossing=>world.Observe().Map==GameMaps.Crossing;
  bool OnStairs(Vector3 p)=>Crossing?CrossingLayout.OnClimb(p)&&Mathf.Abs(p.z-26)>1.8f:Mathf.Abs(p.x-WarehouseLayout.StairX)<1.3f&&p.y>.5f&&p.y<3.2f&&p.z<WarehouseLayout.DeckFront;
  bool OnRamp(Vector3 p)=>Crossing?CrossingLayout.OnClimb(p)&&Mathf.Abs(p.z-26)<=1.8f&&Mathf.Abs(p.x)>14:Mathf.Abs(p.x-WarehouseLayout.RampX)<1.8f&&p.y>.5f&&p.y<3.2f&&p.z<WarehouseLayout.DeckFront;
  bool OnDeck(Vector3 p)=>Crossing?CrossingLayout.OnDeck(p):p.y>WarehouseLayout.Upper-.4f&&p.z>WarehouseLayout.DeckFront-.3f;
  readonly Dictionary<int,List<string>> lanes=new Dictionary<int,List<string>>();int laneDetours,bridgeShoves;
  void Observe(SessionState s) {
   if(s.Round!=round){if(round!=0)Note($"라운드 {round} 끝");round=s.Round;Note($"라운드 {round} 시작 · 맵 {GameMaps.Name(s.Map)} · 도구 배정: "+string.Join(", ",s.Players.GroupBy(p=>p.Tool).OrderBy(g=>g.Key).Select(g=>$"{Tools.Short(g.Key)} {g.Count()}")));}
   foreach(var e in s.Events??new FeelEvent[0]) {
    if(e.Id<=lastEvent)continue;lastEvent=e.Id;
    string Name(int slot)=>slot>=0&&slot<s.Players.Length?s.Players[slot].Nickname:"?";
    if((e.Kind==FeelEventKind.Shoved||e.Kind==FeelEventKind.Stagger)&&e.Actor>=0&&e.Actor<s.Players.Length){var t=Tools.Short(s.Players[e.Actor].Tool);toolHits[t]=(toolHits.TryGetValue(t,out var n)?n:0)+1;if(s.Players[e.Actor].Tool==ToolKind.Glove&&e.Kind==FeelEventKind.Shoved)Note($"글러브로 아군 날림: {Name(e.Actor)} → {Name(e.Target)}");}
    if(e.Kind==FeelEventKind.Shoved){shoves++;var where=V(s.Players[e.Target].Position);var line=$"아군 밀침: {Name(e.Actor)} → {Name(e.Target)} @({where.x:F1},{where.y:F1},{where.z:F1}){(Crossing&&CrossingLayout.OnBridge(where)?" [다리 위]":OnDeck(where)||OnStairs(where)?" [계단/2층]":"")}";if(Crossing&&CrossingLayout.OnBridge(where))bridgeShoves++;shoveLines.Add(line);Note(line);
     if(e.Target==network.LocalSlot)localShoved++;if(e.Actor==network.LocalSlot)localShovedOthers++;}
    else if(e.Kind==FeelEventKind.Stagger)staggers++;
    else if(e.Kind==FeelEventKind.FellOff){falls++;if(e.Actor>=0)shovedFalls++;Note($"추락 변신: {Name(e.Target)}"+(e.Actor>=0?$" ← {Name(e.Actor)}의 {Tools.Short(s.Players[e.Actor].Tool)}":""));}
    else if(e.Kind==FeelEventKind.Transform&&e.Actor>=0){transforms++;Note($"변신: {Name(e.Target)} ← {Name(e.Actor)} @({e.Position.X:F1},{e.Position.Y:F1},{e.Position.Z:F1})");}
   }
   if(s.Phase!=RoundPhase.Chase)return;
   foreach(var p in s.Players) {
    var at=V(p.Position);
    if(p.Faction==Faction.Animal) {
     if(OnStairs(at)&&onStairs.Add(p.Slot))Note($"동물 계단 진입: {p.Nickname} ({p.CharacterName})");
     if(OnRamp(at)&&onRamp.Add(p.Slot)){Note($"동물 경사로 진입: {p.Nickname}");if(onStairs.Contains(p.Slot)&&detours.Add(p.Slot))Note($"  → 계단에서 경사로로 우회: {p.Nickname}");}
     var lane=Crossing?CrossingLayout.Lane(at):null;
     if(lane!=null){if(!lanes.TryGetValue(p.Slot,out var seen))lanes[p.Slot]=seen=new List<string>();
      if(!seen.Contains(lane)){seen.Add(lane);Note($"동물 {lane} 진입: {p.Nickname}"+(seen.Count>1?$" (우회: {string.Join(" → ",seen)})":""));if(seen.Count==2)laneDetours++;}}
     if(OnDeck(at)&&reachedDeck.Add(p.Slot))Note($"동물 2층 도달: {p.Nickname} (경로: {(onRamp.Contains(p.Slot)?"경사로":onStairs.Contains(p.Slot)?"계단":"?")})");
    } else {
     if(at.y>WarehouseLayout.Upper-.4f)lastHigh[p.Slot]=(Now,at);
     else if(at.y<.4f&&lastHigh.TryGetValue(p.Slot,out var high)&&Now-high.at<1.5&&!OnStairs(high.where)&&!OnRamp(high.where)){
      lastHigh.Remove(p.Slot);drops++;dropped[p.Slot]=Now;Note($"인간 낙하 탈출: {p.Nickname} ({high.where.x:F1},{high.where.z:F1}) → 1층");}
     if(dropped.TryGetValue(p.Slot,out var when)&&Now-when<10&&!chasedAfterDrop.Contains(p.Slot)&&s.Players.Any(a=>a.Faction==Faction.Animal&&Vector3.Distance(V(a.Position),at)<3.5f&&a.Position.Y<1)){chasedAfterDrop.Add(p.Slot);dropChases++;Note($"  → 1층에서 추격 이어짐: {p.Nickname}");}
    }
   }
  }

  // ---------- the local player's hands ----------
  List<Vector3> path=new List<Vector3>();int cursor;double replan;Vector3 goal;bool fleeing;float stuck;Vector3 stuckFrom;double stuckCheck;
  static readonly Vector3 StairPost=new Vector3(WarehouseLayout.StairX,WarehouseLayout.Upper,WarehouseLayout.DeckFront+.7f);
  void Update() {
   if(Now>cap){Finish("시간 상한");return;}
   if(Now>5){frames++;frameTime+=Time.unscaledDeltaTime;worstFrame=Mathf.Max(worstFrame,Time.unscaledDeltaTime);}
   // A guest whose host left (round over, host quit) ends here too instead of waiting for the cap.
   if(joinedSlot>=0&&network.IsClient&&!network.CanPlay){Finish("호스트 연결 종료");return;}
   if(world.Session==null||!network.CanPlay||network.LocalSlot<0)return;
   var s=world.Observe();int slot=network.LocalSlot;var me=s.Players[slot];var at=world.PlayerTransform(slot).position;
   if(joinedSlot<0){joinedSlot=slot;Note($"참가 완료: 슬롯 {slot} · {me.Nickname} · {me.Faction} · 맵 {GameMaps.Name(s.Map)}");localFaction=me.Faction;}
   Observe(s);
   if(me.Faction!=localFaction){Note($"내 캐릭터 변신: {localFaction} → {me.Faction} ({me.CharacterName})");localFaction=me.Faction;fleeing=false;path.Clear();}
   if(haveLocal){var d=at-lastLocal;d.y=0;moved+=d.magnitude;}
   if(moved>=nextMoveNote){Note($"이동 누적 {moved:F0} m · 위치 ({at.x:F1},{at.y:F1},{at.z:F1})");nextMoveNote+=25;}
   // A jump the guest itself sees: its predicted body rises clearly off flat ground.
   // A jump the guest itself sees: its own body rises clearly above where it stood a moment ago (not on a slope).
   recentY.Enqueue((Now,at.y));while(recentY.Count>0&&Now-recentY.Peek().t>.6)recentY.Dequeue();
   float floor=recentY.Min(r=>r.y);
   if(grounded&&at.y>floor+.6f&&!OnStairs(at)&&!OnRamp(at)){jumpsSeen++;grounded=false;Note($"점프 확인 #{jumpsSeen}: 높이 +{at.y-floor:F2} m");}
   if(!grounded&&at.y-floor<.05f)grounded=true;
   lastLocal=at;haveLocal=true;
   if(lastAmmo>=0&&me.Ammo<lastAmmo)shots+=lastAmmo-me.Ammo;lastAmmo=me.Ammo;
   if(s.Phase==RoundPhase.Results){if(resultsSeen<0){resultsSeen=Now;Note($"결과: {(s.Winner==Faction.Human?"인간 승리":s.Winner==Faction.Animal?"동물 승리":"없음")} · 남은 인간 {s.Players.Count(p=>p.Faction==Faction.Human)}");}if(Now-resultsSeen>4)Finish("라운드 종료");network.SubmitInput(new PlayerInput());return;}
   var input=role=="host"?Host(s,me,at):Guest(s,me,at);
   if(input.Jump&&me.Position.Y<.3f)jumps++;
   network.SubmitInput(input);
   lastY=at.y;
  }
  PlayerInput Host(SessionState s,PlayerState me,Vector3 at) {
   if(me.Faction==Faction.Animal)return Hunt(s,me,at);
   var animals=s.Players.Where(p=>p.Faction==Faction.Animal).ToArray();
   var nearest=animals.OrderBy(a=>(V(a.Position)-at).sqrMagnitude).FirstOrDefault();float distance=nearest==null?999:Vector3.Distance(V(nearest.Position),at);
   if(!fleeing&&s.Phase==RoundPhase.Chase&&at.y>3&&(animals.Any(a=>OnDeck(V(a.Position))&&Vector3.Distance(V(a.Position),at)<7)||distance<2.6f)){fleeing=true;path.Clear();replan=0;Note("2층이 뚫림 → 난간 틈으로 뛰어내림");}
   if(Crossing)return HostCrossing(s,me,at,nearest,distance);
   if(MapPlans.For(s.Map)!=null)return HostPlanned(s,me,at,nearest,distance,MapPlans.For(s.Map));
   if(fleeing) {
    // Through the nearest railing gap, then out to the yard and keep away from the closest animal.
    if(at.y>2.5f)goal=new Vector3(WarehouseLayout.DropGaps.OrderBy(g=>Mathf.Abs(g-at.x)).First(),0,WarehouseLayout.DeckFront-3);
    else if(at.z>-WarehouseLayout.HalfZ-2)goal=WarehouseLayout.YardShelter;
    else goal=nearest==null?WarehouseLayout.YardShelter:new Vector3(nearest.Position.X>0?-26:26,0,-20);
    if(at.y<1&&nearest!=null&&distance<9&&distance>2.5f&&Sight(at,V(nearest.Position)))return Fire(at,V(nearest.Position),true);
    return Walk(at,goal,at.y>2.5f);
   }
   if(Vector2.Distance(new Vector2(at.x,at.z),new Vector2(StairPost.x,StairPost.z))>.5f||at.y<3)return Walk(at,StairPost,false);
   // Hold the stair head and shoot down at whatever climbs.
   var target=animals.Where(a=>Vector3.Distance(V(a.Position),at)<20&&Sight(at,V(a.Position))).OrderBy(a=>(V(a.Position)-at).sqrMagnitude).FirstOrDefault();
   if(target!=null){var aim=Fire(at,V(target.Position),false);aim.Attack=Vector3.Distance(V(target.Position),at)<Tools.Range(me.Tool,world.ObserveActiveSettings().Current)+.3f;return aim;}
   presentation.SetLookAngles(180,22);return new PlayerInput{Yaw=180,Pitch=12};
  }
  // Crossing: hold the tower stair head and shoot down it; when the deck is breached, drop through the nearest
  // parapet gap and run for the lane farthest from the closest animal.
  static readonly Vector3 TowerPost=new Vector3(3.2f,CrossingLayout.Upper,26);
  static readonly Vector3[] CrossingDrops={new Vector3(-2,0,22),new Vector3(-16,0,28.3f),new Vector3(16,0,28.3f),new Vector3(-22,0,34.7f),new Vector3(22,0,34.7f),new Vector3(-34,0,22),new Vector3(34,0,22)};
  PlayerInput HostCrossing(SessionState s,PlayerState me,Vector3 at,PlayerState nearest,float distance) {
   var animals=s.Players.Where(p=>p.Faction==Faction.Animal).ToArray();
   if(fleeing) {
    if(at.y>2.5f)goal=CrossingDrops.OrderBy(g=>Vector2.Distance(new Vector2(g.x,g.z),new Vector2(at.x,at.z))).First();
    else goal=nearest==null?CrossingLayout.Recovery[0]:CrossingLayout.Recovery.OrderByDescending(r=>(r-V(nearest.Position)).sqrMagnitude).First();
    if(at.y<1&&nearest!=null&&distance<9&&distance>2.5f&&Sight(at,V(nearest.Position)))return Fire(at,V(nearest.Position),true);
    return Walk(at,goal,at.y>2.5f);
   }
   if(Vector2.Distance(new Vector2(at.x,at.z),new Vector2(TowerPost.x,TowerPost.z))>.5f||at.y<3)return Walk(at,TowerPost,false);
   var target=animals.Where(a=>Vector3.Distance(V(a.Position),at)<20&&Sight(at,V(a.Position))).OrderBy(a=>(V(a.Position)-at).sqrMagnitude).FirstOrDefault();
   if(target!=null){var aim=Fire(at,V(target.Position),false);aim.Attack=Vector3.Distance(V(target.Position),at)<Tools.Range(me.Tool,world.ObserveActiveSettings().Current)+.3f;return aim;}
   presentation.SetLookAngles(90,22);return new PlayerInput{Yaw=90,Pitch=12};
  }
  // Planned maps: hold the first shelter and shoot at what comes; when it is breached, run to the shelter farthest from the nearest animal.
  PlayerInput HostPlanned(SessionState s,PlayerState me,Vector3 at,PlayerState nearest,float distance,MapPlan plan) {
   var animals=s.Players.Where(p=>p.Faction==Faction.Animal).ToArray();
   if(fleeing) {
    goal=nearest==null?plan.Shelters[0]:plan.Shelters.OrderByDescending(r=>(r-V(nearest.Position)).sqrMagnitude).First();
    if(Vector3.Distance(at,goal)<1.5f){fleeing=false;Note("다른 거점으로 피신 완료");}
    if(nearest!=null&&distance<9&&distance>2.5f&&Sight(at,V(nearest.Position)))return Fire(at,V(nearest.Position),true);
    return Walk(at,goal,true);
   }
   var post=plan.Shelters[0];
   if(Vector2.Distance(new Vector2(at.x,at.z),new Vector2(post.x,post.z))>.6f||at.y<post.y-.5f)return Walk(at,post,false);
   var target=animals.Where(a=>Vector3.Distance(V(a.Position),at)<20&&Sight(at,V(a.Position))).OrderBy(a=>(V(a.Position)-at).sqrMagnitude).FirstOrDefault();
   if(target!=null){var aim=Fire(at,V(target.Position),false);aim.Attack=Vector3.Distance(V(target.Position),at)<Tools.Range(me.Tool,world.ObserveActiveSettings().Current)+.3f;return aim;}
   presentation.SetLookAngles(0,22);return new PlayerInput{Yaw=0,Pitch=12};
  }
  PlayerInput Guest(SessionState s,PlayerState me,Vector3 at) {
   if(me.Faction==Faction.Animal)return Hunt(s,me,at);
   var target=s.Players.Where(p=>p.Faction==Faction.Animal&&Vector3.Distance(V(p.Position),at)<14&&Sight(at,V(p.Position))).OrderBy(p=>(V(p.Position)-at).sqrMagnitude).FirstOrDefault();
   if(target!=null&&s.Phase==RoundPhase.Chase){var aim=Fire(at,V(target.Position),false);aim.Attack=Vector3.Distance(V(target.Position),at)<Tools.Range(me.Tool,world.ObserveActiveSettings().Current)+.3f;return aim;}
   // Wander between the ramp foot, the deck and the yard, hopping now and then.
   var stops=Crossing?new[]{CrossingLayout.RampBFoot,CrossingLayout.ShelterB,CrossingLayout.ShelterTower,new Vector3(-12,CrossingLayout.Upper,CrossingLayout.BridgeZ),new Vector3(8,0,0)}:new[]{WarehouseLayout.RampFoot,WarehouseLayout.RampShelter,new Vector3(2,WarehouseLayout.Upper,9),WarehouseLayout.YardShelter,new Vector3(-6,0,-6)};
   if(path.Count==0||cursor>=path.Count||Vector3.Distance(at,goal)<1)goal=stops[(int)(Now/12)%stops.Length];
   var input=Walk(at,goal,false);input.Jump|=((int)(Now*10))%35==0;return input;
  }
  PlayerInput Hunt(SessionState s,PlayerState me,Vector3 at) {
   var prey=s.Players.Where(p=>p.Faction==Faction.Human).OrderBy(p=>(V(p.Position)-at).sqrMagnitude).FirstOrDefault();
   if(prey==null)return new PlayerInput();var where=V(prey.Position);
   if(Vector3.Distance(where,at)<CombatRules.MeleeDistance-.2f){var aim=Fire(at,where,false);return aim;}
   return Walk(at,where,false);
  }
  PlayerInput Fire(Vector3 at,Vector3 target,bool keepMoving) {
   var aim=target+Vector3.up*.9f-(at+Vector3.up*1.3f);float yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg,pitch=-Mathf.Atan2(aim.y,new Vector2(aim.x,aim.z).magnitude)*Mathf.Rad2Deg;
   presentation.SetLookAngles(yaw,Mathf.Clamp(pitch+12,-20,65));
   var input=new PlayerInput{Yaw=yaw,Pitch=pitch,Attack=true,Reload=world.Observe().Players[network.LocalSlot].Ammo==0};
   if(keepMoving){var away=(at-target);away.y=0;var local=Quaternion.Euler(0,-yaw,0)*away.normalized;input.Right=local.x;input.Forward=local.z;}
   return input;
  }
  PlayerInput Walk(Vector3 at,Vector3 to,bool allowDrop) {
   replan-=Time.deltaTime;
   if(replan<=0||(goal-to).sqrMagnitude>1||cursor>=path.Count){goal=to;path=world.PlanBotRoute(at,to,out _,allowDrop);cursor=0;replan=.6;}
   while(cursor<path.Count&&Flat(path[cursor]-at).magnitude<.45f)cursor++;
   var heading=cursor<path.Count?Flat(path[cursor]-at):Flat(to-at);
   if(Now-stuckCheck>.8){stuck=Vector3.Distance(at,stuckFrom)<.3f&&heading.magnitude>.5f?stuck+.8f:0;stuckFrom=at;stuckCheck=Now;}
   if(heading.magnitude<.2f){presentation.SetLookAngles(lookYaw,18);return new PlayerInput{Yaw=lookYaw};}
   float yaw=Mathf.Atan2(heading.x,heading.z)*Mathf.Rad2Deg;lookYaw=Mathf.LerpAngle(lookYaw,yaw,Time.deltaTime*5);presentation.SetLookAngles(lookYaw,18);
   var local=Quaternion.Euler(0,-lookYaw,0)*heading.normalized;
   return new PlayerInput{Yaw=lookYaw,Right=local.x,Forward=local.z,Jump=stuck>1.5f};
  }
  float lookYaw;
  static Vector3 Flat(Vector3 v)=>new Vector3(v.x,0,v.z);
  static bool Sight(Vector3 from,Vector3 to) {
   var a=from+Vector3.up*1.3f;var b=to+Vector3.up*.9f;
   return !Physics.RaycastAll(a,(b-a).normalized,Vector3.Distance(a,b),Layers.World,QueryTriggerInteraction.Ignore).Any(h=>h.collider.GetComponentInParent<CharacterController>()==null);
  }

  // ---------- camera: an overview while the stair is held, the player's own view otherwise ----------
  void LateUpdate() {
   if(role!="host"||world.Session==null||!network.CanPlay)return;
   var s=world.Observe();var me=s.Players[network.LocalSlot];var at=world.PlayerTransform(network.LocalSlot).position;
   bool overview=s.Phase==RoundPhase.Chase&&me.Faction==Faction.Human&&!fleeing&&at.y>3;
   if(!overview)return;
   var view=Camera.main;if(view==null)return;
   if(s.Map==GameMaps.Crossing){view.transform.position=new Vector3(16,17,8);view.transform.rotation=Quaternion.LookRotation(new Vector3(-2,2,28)-view.transform.position);return;}
   view.transform.position=new Vector3(3,5,-11);view.transform.rotation=Quaternion.LookRotation(new Vector3(-9,2.5f,7)-view.transform.position);
  }

  bool finished;
  void Finish(string why) {
   if(finished)return;finished=true;Note("종료: "+why);
   var r=new StringBuilder();
   r.AppendLine($"# 시나리오 결과 ({role}) · 맵 {GameMaps.Name(world.Session==null?network.HostMap:world.Observe().Map)}");
   r.AppendLine($"화면 갱신: 평균 {(frames>0?frames/frameTime:0):F0} FPS · 가장 긴 프레임 {worstFrame*1000:F0} ms{(recorder!=null?" (녹화 중이라 낮게 나옴)":"")}");
   r.AppendLine($"참가 슬롯: {joinedSlot} · 이동 거리: {moved:F0} m · 점프 입력: {jumps} · 점프 확인: {jumpsSeen} · 발사: {shots}");
   r.AppendLine($"아군 밀침 전체: {shoves} (내가 밀림 {localShoved}, 내가 밈 {localShovedOthers}) · 동물 경직: {staggers} · 공격 변신: {transforms}");
   r.AppendLine($"동물 계단 진입: {onStairs.Count} · 경사로 진입: {onRamp.Count} · 계단→경사로 우회: {detours.Count} · 2층 도달: {reachedDeck.Count}");
   r.AppendLine($"인간 낙하 탈출: {drops} · 낙하 후 1층 추격: {dropChases}");
   if(falls>0)r.AppendLine($"추락 변신: {falls} (밀려서 떨어짐 {shovedFalls})");
   if(lanes.Count>0)r.AppendLine($"동물 갈래 사용: "+string.Join(", ",lanes.Values.SelectMany(l=>l).GroupBy(l=>l).Select(g=>$"{g.Key} {g.Count()}"))+$" · 갈래 우회: {laneDetours} · 다리 위 밀침: {bridgeShoves}");
   r.AppendLine("도구별 명중(밀침+경직): "+string.Join(", ",toolHits.OrderByDescending(p=>p.Value).Select(p=>$"{p.Key} {p.Value}")));
   foreach(var line in shoveLines.Take(20))r.AppendLine("- "+line);
   r.AppendLine();r.AppendLine("## 기록");r.Append(log);
   File.WriteAllText(Path.Combine(folder,$"scenario-{role}.txt"),r.ToString());
   recorder?.Finish();
   Application.Quit();
  }
 }
}
