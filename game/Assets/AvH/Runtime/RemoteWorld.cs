using UnityEngine;
using System.Collections.Generic;
using System.Linq;
namespace AvH {
 public sealed partial class UnityPlaytestSession {
  SessionState remoteSnapshot;
  NetworkVisualState remoteVisuals;
  readonly Dictionary<int,GameObject> remoteBubbles=new Dictionary<int,GameObject>();
  readonly Dictionary<int,GameObject> remoteBursts=new Dictionary<int,GameObject>();
  public bool IsRemote=>remoteSnapshot!=null;
  public void StartRemote(SessionState state) {
   StartSolo("연결 중");AutomaticStep=false;ApplyRemoteSnapshot(state,true);
  }
  public void ApplyRemoteSnapshot(SessionState state,bool immediate=false) {
   if(!immediate&&ReferenceEquals(remoteSnapshot,state))return;
   var old=remoteSnapshot;remoteSnapshot=state;
   foreach(var p in state.Players){
    if(old==null||old.Players[p.Slot].Faction!=p.Faction)RefreshVisual(p.Slot,p.Faction);
    if(immediate||old==null||state.Round!=old.Round||Vector3.Distance(bodies[p.Slot].transform.position,ToVector(p.Position))>6)Warp(bodies[p.Slot],ToVector(p.Position));
    bodies[p.Slot].enabled=false;
   }
  }
  void InterpolateRemote() {
   foreach(var p in remoteSnapshot.Players){var body=bodies[p.Slot];var next=ToVector(p.Position);var delta=next-body.transform.position;
    body.transform.position=Vector3.Lerp(body.transform.position,next,Mathf.Min(1,Time.deltaTime*20));
    var flat=new Vector3(delta.x,0,delta.z);if(remoteVisuals!=null&&remoteVisuals.Yaws.Length==12)body.transform.rotation=Quaternion.Euler(0,remoteVisuals.Yaws[p.Slot],0);
    foreach(var animator in body.GetComponentsInChildren<Animator>())foreach(var parameter in animator.parameters)if(parameter.type==AnimatorControllerParameterType.Bool&&parameter.name=="isRunning")animator.SetBool(parameter.name,flat.sqrMagnitude>.002f);
   }
  }
  public void ApplyRemoteVisuals(NetworkVisualState visual) {
   if(ReferenceEquals(remoteVisuals,visual))return;
   remoteVisuals=visual;if(visual==null)return;
   if(bubbleMaterial==null){bubbleMaterial=PrototypeVillage.Material(new Color(.25f,.8f,1,.55f));bubbleMaterial.SetFloat("_Mode",3);bubbleMaterial.SetInt("_SrcBlend",5);bubbleMaterial.SetInt("_DstBlend",10);bubbleMaterial.SetInt("_ZWrite",0);bubbleMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");bubbleMaterial.renderQueue=3000;}
   var incoming=new HashSet<int>(visual.Bubbles.Select(b=>b.Id));
   foreach(int id in remoteBubbles.Keys.ToArray())if(!incoming.Contains(id)){Destroy(remoteBubbles[id]);remoteBubbles.Remove(id);}
   foreach(var bubble in visual.Bubbles){GameObject obj;if(!remoteBubbles.TryGetValue(bubble.Id,out obj)){
    obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name="Remote Bubble";obj.transform.SetParent(transform);obj.GetComponent<Collider>().enabled=false;Destroy(obj.GetComponent<Collider>());
    obj.GetComponent<Renderer>().sharedMaterial=bubbleMaterial;remoteBubbles[bubble.Id]=obj;
   }obj.transform.position=ToVector(bubble.Position);obj.transform.localScale=Vector3.one*visual.CurrentRules.BubbleRadius*2;}
   ApplyRemoteBursts(visual);
  }
  void ApplyRemoteBursts(NetworkVisualState visual) {
   var incoming=new HashSet<int>(visual.Bursts.Select(b=>b.Id));
   foreach(int id in remoteBursts.Keys.ToArray())if(!incoming.Contains(id)){Destroy(remoteBursts[id]);remoteBursts.Remove(id);}
   foreach(var burst in visual.Bursts){GameObject obj;if(!remoteBursts.TryGetValue(burst.Id,out obj)){obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name="Remote Bubble Burst";obj.transform.SetParent(transform);obj.GetComponent<Collider>().enabled=false;Destroy(obj.GetComponent<Collider>());obj.GetComponent<Renderer>().sharedMaterial=bubbleMaterial;remoteBursts[burst.Id]=obj;}
    obj.transform.position=ToVector(burst.Position);obj.transform.localScale=Vector3.one*visual.CurrentRules.BubbleRadius*2*(1+(.15f-burst.Remaining)*5);
   }
  }
  public SettingsState ObserveActiveSettings()=>remoteVisuals==null?Session.ObserveSettings():new SettingsState{Current=remoteVisuals.CurrentRules.Copy(),Pending=remoteVisuals.HasPending?remoteVisuals.PendingRules.Copy():null,Version=remoteVisuals.SettingsVersion};
  public void ResetSession() {
   AutomaticStep=true;remoteSnapshot=null;remoteVisuals=null;Session=null;remoteBubbles.Clear();remoteBursts.Clear();bubbles.Clear();bursts.Clear();System.Array.Clear(pushVelocity,0,pushVelocity.Length);
   foreach(Transform child in transform)Destroy(child.gameObject);bodies.Clear();
   System.Array.Clear(inputs,0,inputs.Length);System.Array.Clear(vertical,0,vertical.Length);
  }
 }
}
