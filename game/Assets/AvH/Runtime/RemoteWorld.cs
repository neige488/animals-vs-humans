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
  public void StartRemote(SessionState state) {
   StartSolo("연결 중");AutomaticStep=false;ApplyRemoteSnapshot(state,true);
  }
  public void ApplyRemoteSnapshot(SessionState state,bool immediate=false) {
   if(!immediate&&ReferenceEquals(remoteSnapshot,state))return;
   var old=remoteSnapshot;remoteSnapshot=state;
   foreach(var p in state.Players){
    if(old==null||old.Players[p.Slot].Faction!=p.Faction||old.Players[p.Slot].CharacterId!=p.CharacterId){if(!immediate)PresentAnimalBirth(old,state,p.Slot,ToVector(p.Position));RefreshVisual(p.Slot,p.Faction);}
    if(immediate||old==null||state.Round!=old.Round||Vector3.Distance(bodies[p.Slot].transform.position,ToVector(p.Position))>6){Warp(bodies[p.Slot],ToVector(p.Position));snapRemoteFacing[p.Slot]=true;}
    bodies[p.Slot].enabled=false;
   }
  }
  void InterpolateRemote() {
   float dt=Time.deltaTime;bool yaws=remoteVisuals!=null&&remoteVisuals.Yaws.Length==12;
   foreach(var p in remoteSnapshot.Players){var body=bodies[p.Slot];var next=ToVector(p.Position);var from=body.transform.position;
    body.transform.position=Vector3.Lerp(from,next,Mathf.Min(1,dt*20));
    if(yaws)body.transform.rotation=Quaternion.Slerp(body.transform.rotation,Quaternion.Euler(0,remoteVisuals.Yaws[p.Slot],0),1-Mathf.Exp(-24f*dt));
    // Until snapshots carry motion state (S4), the shared animator reads motion estimated from the interpolation.
    if(dt>0){var step=(body.transform.position-from)/dt;
     body.GetComponent<CharacterAnimator>().Apply(new LocomotionState{VelocityX=step.x,VelocityZ=step.z,VerticalSpeed=step.y,Grounded=Mathf.Abs(step.y)<1.5f,
      AimYaw=yaws?remoteVisuals.Yaws[p.Slot]:body.transform.eulerAngles.y},dt);}
   }
  }
  public void ApplyRemoteVisuals(NetworkVisualState visual) {
   if(ReferenceEquals(remoteVisuals,visual))return;
   bool liveEffects=remoteVisuals!=null;remoteVisuals=visual;if(visual==null)return;
   for(int i=0;i<bodies.Count&&i<visual.Yaws.Length;i++)if(snapRemoteFacing[i]){bodies[i].transform.rotation=Quaternion.Euler(0,visual.Yaws[i],0);snapRemoteFacing[i]=false;}
   for(int i=0;i<guns.Length&&i<visual.Yaws.Length;i++)if(guns[i]!=null){var pose=guns[i].GetComponent<BubbleGunPose>();pose.SetAim(visual.Yaws[i],pose.AimPitch);}
   var incoming=new HashSet<int>(visual.Bubbles.Select(b=>b.Id));
   foreach(int id in remoteBubbles.Keys.ToArray())if(!incoming.Contains(id)){Destroy(remoteBubbles[id]);remoteBubbles.Remove(id);}
   foreach(var bubble in visual.Bubbles){GameObject obj;if(!remoteBubbles.TryGetValue(bubble.Id,out obj)){
    obj=Effects.Bubble(transform,visual.CurrentRules.BubbleRadius);obj.name="Remote Bubble";remoteBubbles[bubble.Id]=obj;
    if(liveEffects&&bubble.OwnerSlot>=0&&bubble.OwnerSlot<bodies.Count){var direction=ToVector(bubble.Direction);var muzzle=bodies[bubble.OwnerSlot].transform.position+Vector3.up*1.3f+Vector3.Cross(Vector3.up,direction).normalized*.1f+direction*.6f;Effects.Emit(muzzle,new Color(.63f,.93f,1,.7f),4,.65f,false);Effects.Sound(muzzle,true);}
    if(bubble.OwnerSlot>=0&&bubble.OwnerSlot<guns.Length&&guns[bubble.OwnerSlot]!=null&&ToVector(bubble.Direction).sqrMagnitude>.001f){var direction=ToVector(bubble.Direction);guns[bubble.OwnerSlot].GetComponent<BubbleGunPose>().SetAim(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg);}
   }obj.transform.position=ToVector(bubble.Position);obj.transform.localScale=Vector3.one*visual.CurrentRules.BubbleRadius*2;}
   ApplyRemoteBursts(visual,liveEffects);
  }
  void ApplyRemoteBursts(NetworkVisualState visual,bool liveEffects) {
   var incoming=new HashSet<int>(visual.Bursts.Select(b=>b.Id));
   foreach(int id in remoteBursts.Keys.ToArray())if(!incoming.Contains(id)){Destroy(remoteBursts[id]);remoteBursts.Remove(id);}
   foreach(var burst in visual.Bursts){GameObject obj;if(!remoteBursts.TryGetValue(burst.Id,out obj)){obj=new GameObject("Remote impact");obj.transform.SetParent(transform,false);remoteBursts[burst.Id]=obj;if(seenRemoteImpacts.Add(burst.Id)){recentRemoteImpacts.Enqueue(burst.Id);if(recentRemoteImpacts.Count>256)seenRemoteImpacts.Remove(recentRemoteImpacts.Dequeue());if(liveEffects){Effects.Emit(ToVector(burst.Position),new Color(.66f,.92f,1,.9f),12,1.9f);Effects.Sound(ToVector(burst.Position),false);}}}
    obj.transform.position=ToVector(burst.Position);
   }
  }
  public SettingsState ObserveActiveSettings()=>remoteVisuals==null?Session.ObserveSettings():new SettingsState{Current=remoteVisuals.CurrentRules.Copy(),Pending=remoteVisuals.HasPending?remoteVisuals.PendingRules.Copy():null,Version=remoteVisuals.SettingsVersion};
  public void ResetSession() {
   AutomaticStep=true;remoteSnapshot=null;remoteVisuals=null;Session=null;remoteBubbles.Clear();remoteBursts.Clear();bubbles.Clear();bursts.Clear();System.Array.Clear(pushVelocity,0,pushVelocity.Length);
   seenRemoteImpacts.Clear();recentRemoteImpacts.Clear();effects=null;
   foreach(Transform child in transform)Destroy(child.gameObject);bodies.Clear();
   System.Array.Clear(inputs,0,inputs.Length);System.Array.Clear(vertical,0,vertical.Length);System.Array.Clear(motion,0,motion.Length);
  }
 }
}
