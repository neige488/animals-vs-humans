using UnityEngine;
namespace AvH {
 public sealed partial class UnityPlaytestSession {
  SessionState remoteSnapshot;
  public bool IsRemote=>remoteSnapshot!=null;
  public void StartRemote(SessionState state) {
   StartSolo("연결 중");AutomaticStep=false;ApplyRemoteSnapshot(state,true);
  }
  public void ApplyRemoteSnapshot(SessionState state,bool immediate=false) {
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
    var flat=new Vector3(delta.x,0,delta.z);if(flat.sqrMagnitude>.0001f)body.transform.rotation=Quaternion.LookRotation(flat);
    foreach(var animator in body.GetComponentsInChildren<Animator>())foreach(var parameter in animator.parameters)if(parameter.type==AnimatorControllerParameterType.Bool&&parameter.name=="isRunning")animator.SetBool(parameter.name,flat.sqrMagnitude>.002f);
   }
  }
  public void ResetSession() {
   AutomaticStep=true;remoteSnapshot=null;Session=null;
   foreach(Transform child in transform)Destroy(child.gameObject);bodies.Clear();
   System.Array.Clear(inputs,0,inputs.Length);System.Array.Clear(vertical,0,vertical.Length);
  }
 }
}
