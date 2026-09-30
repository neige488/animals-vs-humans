using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AvH {
 [Serializable] public struct PlayerInput { public float Right, Forward, Yaw, Pitch; public bool Jump, Attack, Reload; public int RoundId; }
 /// <summary>Public game execution boundary. Inputs flow through real CharacterControllers.</summary>
 public sealed partial class UnityPlaytestSession : MonoBehaviour {
  public bool AutomaticStep = true;
  public bool BotAutomationEnabled=true;
  public IReadOnlyList<Vector3> ShelterPoints {get;private set;}
  readonly BotDirector botDirector=new BotDirector();
  public PlaytestSession Session { get; private set; }
  readonly List<CharacterController> bodies = new List<CharacterController>();
  readonly PlayerInput[] inputs = new PlayerInput[12];
  readonly float[] vertical = new float[12];
  readonly Transform[] guns=new Transform[12];
  readonly Vector3[] returns = { new Vector3(-12,1,-6), new Vector3(12,1,0),new Vector3(0,1,-12),new Vector3(0,1,12) };
  public Vector3[] RecoveryPoints => (Vector3[])returns.Clone();
  public void StartSolo(string nickname, int? randomSeed = null, string settingsPath = null) {
   if(Session != null) throw new InvalidOperationException("이미 시작된 세션입니다.");
   Session = new PlaytestSession(randomSeed ?? Environment.TickCount, settingsPath ?? System.IO.Path.Combine(Application.persistentDataPath,"playtest-settings.xml"));
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   Session.StartSolo(nickname, catalog==null?"임시 동물":catalog.AnimalDisplayName,catalog==null?"일반":catalog.Rarity,
    catalog!=null&&catalog.Humans.Length>0?catalog.Humans.Select(c=>c.Definition()).ToArray():null,catalog!=null&&catalog.Animals.Length>0?catalog.Animals.Select(c=>c.Definition()).ToArray():null);
   ShelterPoints=PrototypeVillage.Build(transform);
   foreach(var p in Session.Observe().Players) {
    var body = new GameObject("Slot " + p.Slot); body.transform.SetParent(transform);
    body.transform.position=ToVector(p.Position);
    var controller=body.AddComponent<CharacterController>();
    controller.height=1.8f; controller.radius=.38f; controller.center=new Vector3(0,.9f,0);
    controller.stepOffset=.3f; controller.slopeLimit=50;
    bodies.Add(controller);
    var contacts=body.AddComponent<KnockbackContact>();contacts.World=this;contacts.Slot=p.Slot;
    RefreshVisual(p.Slot,p.Faction);
   }
  }
  public SessionState Observe() => remoteSnapshot ?? Session.Observe();
  public BotNavigationDiagnostics ObserveBotNavigation()=>botDirector.ObserveNavigation();
  public void SubmitInput(int slot, PlayerInput input) {
   if(slot < 0 || slot >= bodies.Count) throw new ArgumentOutOfRangeException(nameof(slot));
   if(float.IsNaN(input.Right) || float.IsInfinity(input.Right) || float.IsNaN(input.Forward) || float.IsInfinity(input.Forward) || float.IsNaN(input.Yaw) || float.IsInfinity(input.Yaw)) return;
   input.Right=Mathf.Clamp(input.Right,-1,1); input.Forward=Mathf.Clamp(input.Forward,-1,1);
   if(float.IsNaN(input.Pitch)||float.IsInfinity(input.Pitch))return;
   input.Pitch=Mathf.Clamp(input.Pitch,-80,80);
   if(input.RoundId==0)input.RoundId=Session.Observe().Round;
   inputs[slot]=input;
  }
  void Update() { if(remoteSnapshot!=null){InterpolateRemote();return;} if(AutomaticStep && Session!=null) Step(Time.deltaTime); }
  public void Step(float seconds) {
   if(seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
   var before=Session.Observe();
   Session.Advance(seconds);
   var state=Session.Observe();
   PrepareCombatWorld(state,before.Round);
   if(BotAutomationEnabled)botDirector.Step(this,seconds);
   var rules=Session.ObserveSettings().Current;
   for(int i=0;i<bodies.Count;i++) {
    var body=bodies[i]; var p=state.Players[i];
    if(state.Round!=before.Round) { Warp(body,ToVector(p.Position)); vertical[i]=0; }
    if(p.Faction!=before.Players[i].Faction||p.CharacterId!=before.Players[i].CharacterId){PresentAnimalBirth(before,state,i,body.transform.position);RefreshVisual(i,p.Faction);}
    if(state.Phase!=RoundPhase.Results) {
     var input=inputs[i].RoundId==state.Round?inputs[i]:default;
     if(guns[i]!=null&&input.RoundId==state.Round)guns[i].GetComponent<BubbleGunPose>().SetAim(input.Yaw,input.Pitch);
     if(body.isGrounded && vertical[i]<0) vertical[i]=-2;
     if(body.isGrounded && input.Jump){vertical[i]=Mathf.Sqrt(2*22*(p.Faction==Faction.Human?rules.HumanJump:rules.AnimalJump));Effects.Emit(body.transform.position,new Color(.9f,.83f,.64f,.55f),5,.65f);}
     vertical[i]-=22*seconds;
     var direction=Quaternion.Euler(0,input.Yaw,0)*Vector3.ClampMagnitude(new Vector3(input.Right,0,input.Forward),1);
     bool wasGrounded=body.isGrounded;float fallingSpeed=vertical[i];
     body.Move((direction*(p.Faction==Faction.Human?rules.HumanSpeed:rules.AnimalSpeed)+Vector3.up*vertical[i]+pushVelocity[i])*seconds);
     if(!wasGrounded&&body.isGrounded&&fallingSpeed< -4)Effects.Emit(body.transform.position,new Color(.9f,.83f,.64f,.6f),7,1);
     if(direction.sqrMagnitude>.01f) body.transform.rotation=Quaternion.RotateTowards(body.transform.rotation,Quaternion.LookRotation(direction),540f*seconds);
     foreach(var animator in body.GetComponentsInChildren<Animator>()) {
      animator.applyRootMotion=false;
      foreach(var parameter in animator.parameters) {
       if(parameter.type!=AnimatorControllerParameterType.Bool)continue;
       if(parameter.name=="isRunning")animator.SetBool(parameter.name,direction.sqrMagnitude>.01f);
       if(parameter.name=="isWalking")animator.SetBool(parameter.name,false);
      }
     }
     if(body.transform.position.y < -12) {
      var closest=returns[0]; float distance=float.MaxValue;
      foreach(var point in returns) { var d=Vector2.SqrMagnitude(new Vector2(point.x-body.transform.position.x,point.z-body.transform.position.z)); if(d<distance) {distance=d;closest=point;} }
      Warp(body,closest); vertical[i]=0;
     }
    }
    var pos=body.transform.position;
    Session.RecordWorldPosition(i,new WorldPosition(pos.x,pos.y,pos.z));
    pushVelocity[i]=Vector3.MoveTowards(pushVelocity[i],Vector3.zero,20*seconds);
    inputs[i].Jump=false;
   }
   StepCombatWorld(seconds,state);
  }
  internal void ResolveKnockbackContact(int slot,Vector3 normal) {
   float inward=Vector3.Dot(pushVelocity[slot],normal);
   if(inward<0)pushVelocity[slot]-=normal*inward;
  }
  void PresentAnimalBirth(SessionState before,SessionState after,int slot,Vector3 position) {
   if(before!=null&&before.Round==after.Round&&before.Players[slot].Faction==Faction.Human&&after.Players[slot].Faction==Faction.Animal)
    Effects.Emit(position+Vector3.up*.6f,new Color(1,.67f,.24f),18,2.4f);
  }
  public Transform PlayerTransform(int slot) => bodies[slot].transform;
  internal float PresentationYaw(int slot)=>guns[slot]!=null?guns[slot].GetComponent<BubbleGunPose>().AimYaw:bodies[slot].transform.eulerAngles.y;
  static void Warp(CharacterController body,Vector3 position) {body.enabled=false;body.transform.position=position;body.enabled=true;}
  static Vector3 ToVector(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  void RefreshVisual(int slot,Faction faction) {
   var parent=bodies[slot].transform;
   if(guns[slot]!=null){guns[slot].gameObject.SetActive(false);Destroy(guns[slot].gameObject);}guns[slot]=faction==Faction.Human?Effects.Gun(parent):null;
   var old=parent.Find("Visual"); if(old!=null){old.gameObject.SetActive(false);Destroy(old.gameObject);}
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   var prefab=catalog==null?null:catalog.Character(Observe().Players[slot].CharacterId,faction);
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
   foreach(var animator in visual.GetComponentsInChildren<Animator>()){animator.fireEvents=false;animator.Rebind();animator.Update(0);}
   if(guns[slot]!=null)guns[slot].GetComponent<BubbleGunPose>().Bind(visual.transform);
   foreach(var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled=false;
  }
 }
}
