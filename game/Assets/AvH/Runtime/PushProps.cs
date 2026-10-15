using System.Collections.Generic;
using UnityEngine;
namespace AvH {
 /// <summary>One light market prop as every viewer sees it.</summary>
 public struct PropView {
  public int Index;
  /// <summary>crate, barrel or pot.</summary>
  public string Kind;
  /// <summary>Footprint centre at floor height, and facing.</summary>
  public Vector3 Position,Home;
  public float Yaw,HomeYaw,Radius,Height;
  public bool Moving;
 }
 /// <summary>
 /// Light crates, barrels and pots that bodies and bubbles shove. The host judges them with a small deterministic
 /// slide: an upright footprint circle that bodies push ahead of themselves, bubbles knock along, other props
 /// bump, and static scenery stops. Props have no colliders, so no body, bubble line of sight, camera or bot
 /// navigation ever treats them as an obstacle, and only this world's own props take part (tests and the remote
 /// mirror share one scene). Remote viewers only show the poses the host sends.
 /// </summary>
 public sealed class PushProps {
  /// <summary>Village child holding the pushable props, in a fixed order every build shares.</summary>
  public const string Container="Pushable props";
  /// <summary>Moving props, and props that stopped this recently, ride every host frame.</summary>
  public const double SettleWindow=.5;
  const float BodyRadius=.38f,BodyHeight=1.8f,Friction=7,SpinFriction=540,MaxSpeed=9,Gravity=22,Skin=.01f,StepUp=.32f,LostDepth=-20;
  /// <summary>Share of a bubble's push force a prop receives.</summary>
  public const float BubbleShare=.6f;
  sealed class Prop {
   public Transform Visual;public string Kind;
   public Vector3 Home,Position,PivotOffset;public Quaternion HomeRotation;public float HomeYaw,Yaw,Radius,Height;
   public Vector2 Velocity;public float Spin,Fall;public bool Moving,Lost;public double SettledAt=double.NegativeInfinity;
  }
  readonly List<Prop> props=new List<Prop>();
  readonly List<(int prop,int actor,float strength)> knocks=new List<(int,int,float)>();
  /// <summary>Speed (m/s) at which a body or bubble knocking a prop from rest is a public knock.</summary>
  public const float KnockSpeed=1;
  /// <summary>Knocks since the last call: which prop, by which slot, and a 0..1 strength.</summary>
  public (int prop,int actor,float strength)[] TakeKnocks(){var result=knocks.ToArray();knocks.Clear();return result;}
  void Knock(int index,int actor,float before){var p=props[index];float speed=p.Velocity.magnitude;if(before<KnockSpeed*.5f&&speed>=KnockSpeed)knocks.Add((index,actor,Mathf.Clamp01(speed/MaxSpeed)));}
  readonly List<Vector3> lastBodies=new List<Vector3>();
  public int Count=>props.Count;

  /// <summary>Takes over the village's pushable props (none when the village has no container).</summary>
  public static PushProps Adopt(Transform village) {
   var result=new PushProps();var container=village==null?null:Find(village,Container);if(container==null)return result;
   for(int i=0;i<container.childCount;i++) {
    var visual=container.GetChild(i);var renderers=visual.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
    var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
    var home=new Vector3(b.center.x,b.min.y,b.center.z);float yaw=visual.eulerAngles.y;
    result.props.Add(new Prop{Visual=visual,Kind=KindOf(visual.name),Home=home,Position=home,PivotOffset=visual.position-home,HomeRotation=visual.rotation,HomeYaw=yaw,Yaw=yaw,
     Radius=Mathf.Min(b.size.x,b.size.z)*.5f,Height=b.size.y});
   }
   return result;
  }
  static Transform Find(Transform parent,string name){if(parent.name==name)return parent;foreach(Transform child in parent){var found=Find(child,name);if(found!=null)return found;}return null;}
  static string KindOf(string name){name=name.ToLowerInvariant();return name.Contains("barrel")?"barrel":name.Contains("pot")?"pot":"crate";}

  public PropView[] Observe() {
   var result=new PropView[props.Count];
   for(int i=0;i<props.Count;i++){var p=props[i];result[i]=new PropView{Index=i,Kind=p.Kind,Position=p.Position,Home=p.Home,Yaw=p.Yaw,HomeYaw=p.HomeYaw,Radius=p.Radius,Height=p.Height,Moving=p.Moving};}
   return result;
  }

  /// <summary>Round start (and joining): every prop back at its own spot, at rest.</summary>
  public void ResetHome() {
   knocks.Clear();
   foreach(var p in props){p.Position=p.Home;p.Yaw=p.HomeYaw;p.Velocity=Vector2.zero;p.Spin=p.Fall=0;p.Moving=p.Lost=false;p.SettledAt=double.NegativeInfinity;Show(p);}
   lastBodies.Clear();
  }

  /// <summary>
  /// Host step: bodies shove the props they walk into, props bump each other, slide, slow down and stop at static
  /// scenery. A body is never slowed: a prop it cannot move (wedged against a wall) simply lets it through.
  /// </summary>
  public void Simulate(IReadOnlyList<CharacterController> bodies,float seconds,double hostTime) {
   if(props.Count==0||seconds<=0)return;
   if(lastBodies.Count!=bodies.Count){lastBodies.Clear();foreach(var b in bodies)lastBodies.Add(b.transform.position);}
   for(int i=0;i<bodies.Count;i++) {
    var body=bodies[i];var now=body.transform.position;var delta=now-lastBodies[i];lastBodies[i]=now;
    if(!body.enabled)continue;
    // A warp (spawn, recovery) is not a shove.
    var velocity=delta.sqrMagnitude>9?Vector2.zero:new Vector2(delta.x,delta.z)/seconds;
    for(int k=0;k<props.Count;k++){var p=props[k];if(p.Lost)continue;float before=p.Moving?p.Velocity.magnitude:0;if(Shove(p,now,velocity,seconds))Knock(k,i,before);}
   }
   Collide();
   foreach(var p in props)if(p.Moving&&!p.Lost){Slide(p,seconds);if(!p.Moving)p.SettledAt=hostTime;Show(p);}
  }
  bool Shove(Prop p,Vector3 body,Vector2 velocity,float seconds) {
   // Feet ride a skin above the floor, so a low pot still meets the shins.
   if(body.y>p.Position.y+p.Height+.2f||body.y+BodyHeight<p.Position.y)return false;
   var offset=new Vector2(p.Position.x-body.x,p.Position.z-body.z);float reach=BodyRadius+p.Radius,distance=offset.magnitude;
   if(distance>=reach)return false;
   var normal=distance>1e-4f?offset/distance:velocity.sqrMagnitude>1e-6f?velocity.normalized:Vector2.right;
   // Run ahead of the body, plus a gentle push out of the overlap.
   float wanted=Mathf.Max(0,Vector2.Dot(velocity,normal))*1.1f+(reach-distance)*6;
   float along=Vector2.Dot(p.Velocity,normal);
   if(along<wanted)p.Velocity+=normal*(wanted-along);
   // Off-centre shoves turn the prop a little.
   p.Spin=Mathf.Clamp(p.Spin+(normal.x*velocity.y-normal.y*velocity.x)*40*seconds,-240,240);
   Wake(p);return true;
  }
  void Collide() {
   for(int i=0;i<props.Count;i++)for(int j=i+1;j<props.Count;j++) {
    var a=props[i];var b=props[j];if(a.Lost||b.Lost||!a.Moving&&!b.Moving)continue;
    if(a.Position.y>b.Position.y+b.Height||b.Position.y>a.Position.y+a.Height)continue;
    var offset=new Vector2(b.Position.x-a.Position.x,b.Position.z-a.Position.z);float reach=a.Radius+b.Radius,distance=offset.magnitude;
    if(distance>=reach)continue;
    var normal=distance>1e-4f?offset/distance:Vector2.right;
    float closing=Vector2.Dot(a.Velocity-b.Velocity,normal);
    if(closing>0){float impulse=closing*.65f;a.Velocity-=normal*impulse;b.Velocity+=normal*impulse;}
    float apart=(reach-distance)*3;a.Velocity-=normal*apart;b.Velocity+=normal*apart;
    Wake(a);Wake(b);
   }
  }
  static void Wake(Prop p){p.Moving=true;if(p.Velocity.magnitude>MaxSpeed)p.Velocity=p.Velocity.normalized*MaxSpeed;}

  void Slide(Prop p,float seconds) {
   float radius=Mathf.Max(.05f,p.Radius*.9f);
   Vector3 Center()=>p.Position+Vector3.up*(radius+.06f);
   Depenetrate(p,Center(),radius);
   var move=new Vector3(p.Velocity.x,0,p.Velocity.y)*seconds;
   for(int pass=0;pass<2&&move.sqrMagnitude>1e-10f;pass++) {
    float length=move.magnitude;var direction=move/length;
    if(!Sweep(Center(),radius,direction,length+Skin,out var hit)){p.Position+=move;break;}
    float travel=Mathf.Max(0,hit.distance-Skin);p.Position+=direction*travel;
    var normal=new Vector2(hit.normal.x,hit.normal.z);if(normal.sqrMagnitude<1e-6f)break;normal.Normalize();
    float into=Vector2.Dot(p.Velocity,normal);if(into<0)p.Velocity-=normal*into*1.2f;
    var rest=new Vector2(move.x,move.z)*(1-travel/length);rest-=normal*Vector2.Dot(rest,normal);move=new Vector3(rest.x,0,rest.y);
   }
   Ground(p,seconds);
   float speed=p.Velocity.magnitude;
   if(p.Fall==0)speed=Mathf.Max(0,speed-Friction*seconds);
   p.Velocity=speed>0?p.Velocity.normalized*speed:Vector2.zero;
   p.Spin=Mathf.MoveTowards(p.Spin,0,SpinFriction*seconds);p.Yaw=Mathf.Repeat(p.Yaw+p.Spin*seconds,360);
   if(p.Position.y<LostDepth){p.Lost=true;p.Moving=false;p.Velocity=Vector2.zero;return;}
   if(p.Fall==0&&speed<.05f&&Mathf.Abs(p.Spin)<5){p.Velocity=Vector2.zero;p.Spin=0;p.Moving=false;}
  }
  // Pushed out of any static scenery it was squeezed into (another body may have shoved it against a wall).
  static void Depenetrate(Prop p,Vector3 center,float radius) {
   foreach(var collider in Physics.OverlapSphere(center,radius,~0,QueryTriggerInteraction.Ignore)) {
    if(collider is CharacterController||collider is MeshCollider mesh&&!mesh.convex)continue;
    var closest=collider.ClosestPoint(center);var away=center-closest;away.y=0;float distance=away.magnitude;
    if(distance<1e-4f||distance>=radius)continue;
    var normal=away/distance;p.Position+=normal*(radius-distance+Skin);center+=normal*(radius-distance+Skin);
    var flat=new Vector2(normal.x,normal.z);float into=Vector2.Dot(p.Velocity,flat);if(into<0)p.Velocity-=flat*into;
   }
  }
  static bool Sweep(Vector3 center,float radius,Vector3 direction,float distance,out RaycastHit nearest) {
   nearest=default;bool found=false;
   foreach(var hit in Physics.SphereCastAll(center,radius,direction,distance,~0,QueryTriggerInteraction.Ignore)) {
    if(hit.collider is CharacterController||hit.distance<=0)continue;
    if(!found||hit.distance<nearest.distance){nearest=hit;found=true;}
   }
   return found;
  }
  // Rides gentle slopes; falls off a ledge or the map edge (the sweep stops it at any real step).
  static void Ground(Prop p,float seconds) {
   float drop=.05f+Mathf.Max(0,-p.Fall*seconds),best=float.NegativeInfinity;
   foreach(var hit in Physics.RaycastAll(p.Position+Vector3.up*StepUp,Vector3.down,StepUp+drop,~0,QueryTriggerInteraction.Ignore))
    if(!(hit.collider is CharacterController)&&hit.normal.y>.5f&&hit.point.y>best)best=hit.point.y;
   if(best>float.NegativeInfinity){p.Position.y=best;p.Fall=0;return;}
   p.Fall-=Gravity*seconds;p.Position.y+=p.Fall*seconds;
  }
  /// <summary>
  /// Earliest prop a bubble sweep meets within <paramref name="distance"/>. Each prop is an upright cylinder grown by
  /// the bubble radius: the sweep's span inside the footprint circle is intersected with its span between the
  /// grown bottom and top, so entries through the side, the top or the bottom all count.
  /// </summary>
  public bool Hit(Vector3 origin,Vector3 direction,float bubbleRadius,float distance,out int index,out float at) {
   index=-1;at=float.MaxValue;var flat=new Vector2(direction.x,direction.z);float a=flat.sqrMagnitude;
   for(int i=0;i<props.Count;i++) {
    var p=props[i];if(p.Lost)continue;
    float enter=0,leave=distance;
    // Inside the footprint circle (radius grown by the bubble).
    var c=new Vector2(p.Position.x-origin.x,p.Position.z-origin.z);float reach=p.Radius+bubbleRadius,cc=c.sqrMagnitude-reach*reach;
    if(a<1e-8f){if(cc>0)continue;}
    else {
     float b=-2*Vector2.Dot(c,flat),disc=b*b-4*a*cc;if(disc<0)continue;float root=Mathf.Sqrt(disc);
     enter=Mathf.Max(enter,(-b-root)/(2*a));leave=Mathf.Min(leave,(-b+root)/(2*a));
    }
    // Between the grown bottom and top.
    float low=p.Position.y-bubbleRadius,high=p.Position.y+p.Height+bubbleRadius;
    if(Mathf.Abs(direction.y)<1e-6f){if(origin.y<low||origin.y>high)continue;}
    else {float s1=(low-origin.y)/direction.y,s2=(high-origin.y)/direction.y;enter=Mathf.Max(enter,Mathf.Min(s1,s2));leave=Mathf.Min(leave,Mathf.Max(s1,s2));}
    if(enter>leave||enter>=at)continue;
    index=i;at=enter;
   }
   return index>=0;
  }
  /// <summary>Host: a bubble (or other impulse) knocks a prop along, in metres per second.</summary>
  public void Push(int index,Vector3 velocity,int actor=-1) {
   if(index<0||index>=props.Count||props[index].Lost)return;var p=props[index];float before=p.Moving?p.Velocity.magnitude:0;
   p.Velocity+=new Vector2(velocity.x,velocity.z);p.Spin=Mathf.Clamp(p.Spin+(index%2==0?1:-1)*velocity.magnitude*12,-240,240);Wake(p);
   // A bubble always lands a knock, even on a prop already sliding.
   if(actor>=0&&p.Velocity.magnitude>=KnockSpeed)knocks.Add((index,actor,Mathf.Clamp01(velocity.magnitude/MaxSpeed)));else Knock(index,actor,before);
  }

  /// <summary>
  /// Host frame payload: props moving now or settled within <see cref="SettleWindow"/>; with <paramref name="refresh"/>
  /// also every prop away from home, so a late joiner learns where resting props lie.
  /// </summary>
  public NetworkPropMotion[] Capture(double hostTime,bool refresh) {
   var result=new List<NetworkPropMotion>();
   for(int i=0;i<props.Count&&result.Count<512;i++) {
    var p=props[i];
    bool recent=p.Moving||hostTime-p.SettledAt<SettleWindow;
    bool displaced=p.Lost||(p.Position-p.Home).sqrMagnitude>1e-6f||Mathf.Abs(Mathf.DeltaAngle(p.Yaw,p.HomeYaw))>.05f;
    if(recent||refresh&&displaced)result.Add(NetworkPropMotion.Encode(i,p.Lost?new WorldPosition(p.Position.x,LostDepth*2,p.Position.z):new WorldPosition(p.Position.x,p.Position.y,p.Position.z),p.Yaw));
   }
   return result.ToArray();
  }
  // Remote view. Each prop keeps its received poses in host-time order, apart from the frame interpolation buffer
  // (which drops frames it plays over): the newest pose at or before the playback time is the prop's confirmed state,
  // so a sparse refresh the playback stepped over still lands.
  List<(double time,NetworkPropMotion pose)>[] received;
  /// <summary>Two received poses this close in host time are one continuous slide and are interpolated.</summary>
  const double ContinuousGap=.15;
  const int ReceivedLimit=64;
  /// <summary>Remote view: records a received host frame's prop poses (call for every accepted frame, oldest first).</summary>
  public void Receive(NetworkMotionFrame frame) {
   if(props.Count==0||frame?.Props==null)return;
   if(received==null||received.Length!=props.Count){received=new List<(double,NetworkPropMotion)>[props.Count];for(int i=0;i<received.Length;i++)received[i]=new List<(double,NetworkPropMotion)>();}
   foreach(var pose in frame.Props) {
    if(pose.Index<0||pose.Index>=props.Count)continue;var list=received[pose.Index];
    if(list.Count>0&&list[list.Count-1].time>=frame.HostTime)continue;
    list.Add((frame.HostTime,pose));if(list.Count>ReceivedLimit)list.RemoveAt(0);
   }
  }
  /// <summary>Remote view: shows every prop at the host time being played back.</summary>
  public void Present(double renderTime) {
   if(received==null)return;
   for(int i=0;i<props.Count;i++) {
    var list=received[i];
    // Poses older than the newest one already reached are settled history.
    while(list.Count>=2&&list[1].time<=renderTime)list.RemoveAt(0);
    if(list.Count==0||list[0].time>renderTime)continue;
    var p=props[i];var from=list[0];var target=Vector(from.pose);float yaw=from.pose.AimYaw();bool moving=list.Count>=2;
    if(list.Count>=2&&list[1].time-from.time<=ContinuousGap) {
     var to=list[1];float t=(float)((renderTime-from.time)/(to.time-from.time));var end=Vector(to.pose);
     if((end-target).sqrMagnitude<=NetworkBodyMotion.TeleportDistance*NetworkBodyMotion.TeleportDistance){target=Vector3.Lerp(target,end,t);yaw=Mathf.LerpAngle(yaw,to.pose.AimYaw(),t);}
    }
    p.Position=target;p.Yaw=Mathf.Repeat(yaw,360);p.Lost=p.Position.y<LostDepth;p.Moving=moving;
    Show(p);
   }
  }
  /// <summary>Remote view: forget what earlier frames said (new round, rejoin); every prop shows at home.</summary>
  public void ForgetRemote(){received=null;ResetHome();}
  static Vector3 Vector(NetworkPropMotion pose){var w=pose.Position();return new Vector3(w.X,w.Y,w.Z);}

  void Show(Prop p) {
   if(p.Visual==null)return;
   if(p.Visual.gameObject.activeSelf==p.Lost)p.Visual.gameObject.SetActive(!p.Lost);
   var turn=Quaternion.Euler(0,p.Yaw-p.HomeYaw,0);p.Visual.SetPositionAndRotation(p.Position+turn*p.PivotOffset,turn*p.HomeRotation);
  }
 }
}
