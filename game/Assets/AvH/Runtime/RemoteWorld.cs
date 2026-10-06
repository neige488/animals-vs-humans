using UnityEngine;
using System.Collections.Generic;
using System.Linq;
namespace AvH {
 public sealed partial class UnityPlaytestSession {
  SessionState remoteSnapshot;
  NetworkVisualState remoteVisuals;
  readonly bool[] snapRemoteFacing=new bool[12];
  readonly Dictionary<int,GameObject> remoteBubbles=new Dictionary<int,GameObject>();
  readonly Dictionary<int,GameObject> remoteBursts=new Dictionary<int,GameObject>();
  readonly HashSet<int> seenRemoteImpacts=new HashSet<int>();
  readonly Queue<int> recentRemoteImpacts=new Queue<int>();
  public bool IsRemote=>remoteSnapshot!=null;
  /// <summary>Remote views play host frames from the jitter buffer each frame (tests drive <see cref="Step"/> instead).</summary>
  public bool AutomaticRemotePlayback=true;
  /// <summary>The viewer's own slot on a remote view: it follows the newest frame instead of the jitter buffer, so control latency does not grow.</summary>
  public int LocalViewerSlot=-1;
  // Snapshot buffer interpolation: host presentation frames on a local playback clock.
  readonly InterpolationTimeline<NetworkMotionFrame> remoteMotion=new InterpolationTimeline<NetworkMotionFrame>();
  double remoteClock,lastFrameArrival;
  // A stream silent this long stops animating in place; the newest pose is held.
  const float StalledSeconds=.35f;
  public void StartRemote(SessionState state) {
   StartSolo("연결 중");AutomaticStep=false;ApplyRemoteSnapshot(state,true);
  }
  public void ApplyRemoteSnapshot(SessionState state,bool immediate=false) {
   if(!immediate&&ReferenceEquals(remoteSnapshot,state))return;
   var old=remoteSnapshot;remoteSnapshot=state;
   foreach(var p in state.Players){
    if(old==null||old.Players[p.Slot].Faction!=p.Faction||old.Players[p.Slot].CharacterId!=p.CharacterId)ChangeVisual(immediate?null:old,state,p.Slot,ToVector(p.Position));
    // With host frames flowing, the frames themselves show warps at the right moment; snapshots only reset on joins and new rounds.
    bool reset=immediate||old==null||state.Round!=old.Round;
    if(reset||remoteMotion.Count==0&&Vector3.Distance(bodies[p.Slot].transform.position,ToVector(p.Position))>NetworkBodyMotion.TeleportDistance){Warp(bodies[p.Slot],ToVector(p.Position));snapRemoteFacing[p.Slot]=true;}
    bodies[p.Slot].enabled=false;
   }
   if(immediate||old==null||state.Round!=old.Round)remoteMotion.Clear();
   if(state.Phase==RoundPhase.Results&&(old==null||old.Phase!=RoundPhase.Results))CutTransformations();
   if(immediate)ForgetPresentedEvents();PresentEvents(state);PresentStateAudio(state,immediate);
  }
  /// <summary>Adds one host presentation frame to the playback buffer (duplicates and stale frames are ignored).</summary>
  public void ReceiveRemoteMotion(NetworkMotionFrame frame) {
   if(remoteSnapshot==null||frame==null||frame.Bodies.Length!=bodies.Count)return;
   // A receive can carry the next round's snapshot together with the previous round's last frames; those are never replayed.
   if(frame.Round!=remoteSnapshot.Round)return;
   int before=remoteMotion.Count;var newest=remoteMotion.Latest;remoteMotion.Add(frame.HostTime,remoteClock,frame);
   if(remoteMotion.Latest!=newest||remoteMotion.Count!=before)lastFrameArrival=remoteClock;
   for(int i=0;i<bodies.Count;i++)if(snapRemoteFacing[i]){var pose=frame.Bodies[i];bodies[i].transform.rotation=Quaternion.Euler(0,pose.AimYaw(),0);snapRemoteFacing[i]=false;}
  }
  void InterpolateRemote(float dt) {
   if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
   remoteClock+=dt;
   if(!remoteMotion.Sample(remoteClock,out var from,out var to,out float t)){HoldSnapshot(dt);return;}
   var newest=remoteMotion.Latest;bool stalled=remoteClock-lastFrameArrival>StalledSeconds;
   foreach(var p in remoteSnapshot.Players){var body=bodies[p.Slot];bool own=p.Slot==LocalViewerSlot;
    var pose=own?NetworkBodyMotion.Blend(newest.Bodies[p.Slot],newest.Bodies[p.Slot],1):NetworkBodyMotion.Blend(from.Bodies[p.Slot],to.Bodies[p.Slot],t);
    var target=ToVector(pose.Position);
    if(Vector3.Distance(body.transform.position,target)>NetworkBodyMotion.TeleportDistance)body.transform.position=target;
    // The viewer's own body keeps the previous light smoothing toward the newest frame.
    else body.transform.position=own?Vector3.Lerp(body.transform.position,target,Mathf.Min(1,dt*20)):target;
    body.transform.rotation=Quaternion.Euler(0,pose.Yaw,0);
    if(guns[p.Slot]!=null)guns[p.Slot].GetComponent<BubbleGunPose>().SetAim(pose.Yaw,pose.Pitch);
    var motion=pose.Motion;if(stalled){motion.VelocityX=motion.VelocityZ=motion.VerticalSpeed=0;}
    body.GetComponent<CharacterAnimator>().Apply(motion,dt);
   }
   // Footsteps and growls follow the played-back animation (merged S3 audio with S4 playback).
   PresentMotionAudio(remoteSnapshot,dt);
  }
  // Before the first host frame (joining): stand at the snapshot position with the snapshot's action.
  void HoldSnapshot(float dt) {
   foreach(var p in remoteSnapshot.Players){var body=bodies[p.Slot];
    body.transform.position=Vector3.Lerp(body.transform.position,ToVector(p.Position),Mathf.Min(1,dt*20));
    body.GetComponent<CharacterAnimator>().Apply(new LocomotionState{Grounded=true,AimYaw=body.transform.eulerAngles.y,Action=p.Action},dt);
   }
   PresentMotionAudio(remoteSnapshot,dt);
  }
  public void ApplyRemoteVisuals(NetworkVisualState visual) {
   if(ReferenceEquals(remoteVisuals,visual))return;
   bool liveEffects=remoteVisuals!=null;remoteVisuals=visual;if(visual==null)return;
   ReceiveRemoteMotion(visual.Motion);
   var incoming=new HashSet<int>(visual.Bubbles.Select(b=>b.Id));
   foreach(int id in remoteBubbles.Keys.ToArray())if(!incoming.Contains(id)){Destroy(remoteBubbles[id]);remoteBubbles.Remove(id);}
   foreach(var bubble in visual.Bubbles){GameObject obj;if(!remoteBubbles.TryGetValue(bubble.Id,out obj)){
    obj=Effects.Bubble(transform,visual.CurrentRules.BubbleRadius);obj.name="Remote Bubble";remoteBubbles[bubble.Id]=obj;
    if(liveEffects&&bubble.OwnerSlot>=0&&bubble.OwnerSlot<bodies.Count){var direction=ToVector(bubble.Direction);var muzzle=bodies[bubble.OwnerSlot].transform.position+Vector3.up*1.3f+Vector3.Cross(Vector3.up,direction).normalized*.1f+direction*.6f;Effects.Emit(muzzle,new Color(.63f,.93f,1,.7f),4,.65f,false);Audio.Play("fire",muzzle);}
    if(bubble.OwnerSlot>=0&&bubble.OwnerSlot<guns.Length&&guns[bubble.OwnerSlot]!=null&&ToVector(bubble.Direction).sqrMagnitude>.001f){var direction=ToVector(bubble.Direction);guns[bubble.OwnerSlot].GetComponent<BubbleGunPose>().SetAim(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg);}
   }obj.transform.position=ToVector(bubble.Position);obj.transform.localScale=Vector3.one*visual.CurrentRules.BubbleRadius*2;}
   ApplyRemoteBursts(visual,liveEffects);
  }
  void ApplyRemoteBursts(NetworkVisualState visual,bool liveEffects) {
   var incoming=new HashSet<int>(visual.Bursts.Select(b=>b.Id));
   foreach(int id in remoteBursts.Keys.ToArray())if(!incoming.Contains(id)){Destroy(remoteBursts[id]);remoteBursts.Remove(id);}
   foreach(var burst in visual.Bursts){GameObject obj;if(!remoteBursts.TryGetValue(burst.Id,out obj)){obj=new GameObject("Remote impact");obj.transform.SetParent(transform,false);remoteBursts[burst.Id]=obj;if(seenRemoteImpacts.Add(burst.Id)){recentRemoteImpacts.Enqueue(burst.Id);if(recentRemoteImpacts.Count>256)seenRemoteImpacts.Remove(recentRemoteImpacts.Dequeue());if(liveEffects){Effects.Emit(ToVector(burst.Position),new Color(.66f,.92f,1,.9f),12,1.9f);Audio.Play("pop",ToVector(burst.Position));}}}
    obj.transform.position=ToVector(burst.Position);
   }
  }
  public SettingsState ObserveActiveSettings()=>remoteVisuals==null?Session.ObserveSettings():new SettingsState{Current=remoteVisuals.CurrentRules.Copy(),Pending=remoteVisuals.HasPending?remoteVisuals.PendingRules.Copy():null,Version=remoteVisuals.SettingsVersion};
  public void ResetSession() {
   AutomaticStep=true;remoteSnapshot=null;remoteVisuals=null;remoteMotion.Clear();LocalViewerSlot=-1;Session=null;remoteBubbles.Clear();remoteBursts.Clear();bubbles.Clear();bursts.Clear();System.Array.Clear(pushVelocity,0,pushVelocity.Length);
   seenRemoteImpacts.Clear();recentRemoteImpacts.Clear();effects=null;ForgetAudio();System.Array.Clear(presentedBirths,0,presentedBirths.Length);ForgetPresentedEvents();
   foreach(Transform child in transform)Destroy(child.gameObject);bodies.Clear();
   System.Array.Clear(inputs,0,inputs.Length);System.Array.Clear(vertical,0,vertical.Length);System.Array.Clear(motion,0,motion.Length);
  }
 }
}
