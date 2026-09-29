using System;
using System.Collections.Generic;
using UnityEngine;
namespace AvH {
 [Serializable] public struct PlayerInput { public float Right, Forward, Yaw; public bool Jump; }
 /// <summary>Public game execution boundary. Inputs flow through real CharacterControllers.</summary>
 public sealed class UnityPlaytestSession : MonoBehaviour {
  public bool AutomaticStep = true;
  public PlaytestSession Session { get; private set; }
  readonly List<CharacterController> bodies = new List<CharacterController>();
  readonly PlayerInput[] inputs = new PlayerInput[12];
  readonly float[] vertical = new float[12];
  readonly Vector3[] returns = { new Vector3(-12,1,0), new Vector3(12,1,0),new Vector3(0,1,-12),new Vector3(0,1,12) };
  public void StartSolo(string nickname) {
   if(Session != null) throw new InvalidOperationException("이미 시작된 세션입니다.");
   Session = new PlaytestSession(Environment.TickCount);
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   Session.StartSolo(nickname, catalog==null?"임시 동물":catalog.AnimalDisplayName,catalog==null?"일반":catalog.Rarity);
   PrototypeVillage.Build(transform);
   foreach(var p in Session.Observe().Players) {
    var body = new GameObject("Slot " + p.Slot); body.transform.SetParent(transform);
    body.transform.position=ToVector(p.Position);
    var controller=body.AddComponent<CharacterController>();
    controller.height=1.8f; controller.radius=.38f; controller.center=new Vector3(0,.9f,0);
    controller.stepOffset=.3f; controller.slopeLimit=50;
    bodies.Add(controller);
    RefreshVisual(p.Slot,p.Faction);
   }
  }
  public SessionState Observe() => Session.Observe();
  public void SubmitInput(int slot, PlayerInput input) {
   if(slot < 0 || slot >= bodies.Count) throw new ArgumentOutOfRangeException(nameof(slot));
   if(float.IsNaN(input.Right) || float.IsInfinity(input.Right) || float.IsNaN(input.Forward) || float.IsInfinity(input.Forward) || float.IsNaN(input.Yaw) || float.IsInfinity(input.Yaw)) return;
   input.Right=Mathf.Clamp(input.Right,-1,1); input.Forward=Mathf.Clamp(input.Forward,-1,1);
   inputs[slot]=input;
  }
  void Update() { if(AutomaticStep && Session!=null) Step(Time.deltaTime); }
  public void Step(float seconds) {
   if(seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
   var before=Session.Observe();
   Session.Advance(seconds);
   var state=Session.Observe();
   for(int i=0;i<bodies.Count;i++) {
    var body=bodies[i]; var p=state.Players[i];
    if(state.Round!=before.Round) { Warp(body,ToVector(p.Position)); vertical[i]=0; }
    if(p.Faction!=before.Players[i].Faction) RefreshVisual(i,p.Faction);
    if(state.Phase!=RoundPhase.Results) {
     var input=inputs[i];
     if(body.isGrounded && vertical[i]<0) vertical[i]=-2;
     if(body.isGrounded && input.Jump) vertical[i]=Mathf.Sqrt(2*22*1.5f);
     vertical[i]-=22*seconds;
     var direction=Quaternion.Euler(0,input.Yaw,0)*Vector3.ClampMagnitude(new Vector3(input.Right,0,input.Forward),1);
     body.Move((direction*(p.Faction==Faction.Human?5:5.6f)+Vector3.up*vertical[i])*seconds);
     if(direction.sqrMagnitude>.01f) body.transform.rotation=Quaternion.LookRotation(direction);
     if(body.transform.position.y < -12) {
      var closest=returns[0]; float distance=float.MaxValue;
      foreach(var point in returns) { var d=Vector2.SqrMagnitude(new Vector2(point.x-body.transform.position.x,point.z-body.transform.position.z)); if(d<distance) {distance=d;closest=point;} }
      Warp(body,closest); vertical[i]=0;
     }
    }
    var pos=body.transform.position;
    Session.RecordWorldPosition(i,new WorldPosition(pos.x,pos.y,pos.z));
    inputs[i].Jump=false;
   }
  }
  public Transform PlayerTransform(int slot) => bodies[slot].transform;
  static void Warp(CharacterController body,Vector3 position) {body.enabled=false;body.transform.position=position;body.enabled=true;}
  static Vector3 ToVector(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  void RefreshVisual(int slot,Faction faction) {
   var parent=bodies[slot].transform;
   var old=parent.Find("Visual"); if(old!=null) Destroy(old.gameObject);
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   var prefab=catalog==null?null:(faction==Faction.Human?catalog.Human:catalog.Animal);
   GameObject visual;
   if(prefab!=null) {visual=Instantiate(prefab,parent);visual.transform.localPosition=Vector3.zero;}
   else {
    visual=GameObject.CreatePrimitive(faction==Faction.Human?PrimitiveType.Capsule:PrimitiveType.Cube);
    visual.transform.SetParent(parent,false); visual.transform.localPosition=new Vector3(0,.9f,0);
    visual.transform.localScale=faction==Faction.Human?new Vector3(.65f,.85f,.65f):new Vector3(.8f,.9f,1.3f);
    Destroy(visual.GetComponent<Collider>());
    visual.GetComponent<Renderer>().sharedMaterial=PrototypeVillage.Material(faction==Faction.Human?new Color(.15f,.7f,.95f):new Color(1,.48f,.16f));
   }
   visual.name="Visual";
   foreach(var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled=false;
  }
 }
}
