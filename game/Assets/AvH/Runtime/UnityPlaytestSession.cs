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
  readonly MotionState[] motion = new MotionState[12];
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
  void Update() { if(remoteSnapshot!=null){if(AutomaticRemotePlayback)InterpolateRemote(Time.deltaTime);return;} if(AutomaticStep && Session!=null) Step(Time.deltaTime); }
  /// <summary>Advances the host simulation, or on a remote view advances frame playback by the same time.</summary>
  public void Step(float seconds) {
   if(remoteSnapshot!=null){InterpolateRemote(seconds);return;}
   if(seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
   var before=Session.Observe();
   Session.Advance(seconds);
   var state=Session.Observe();
   PrepareCombatWorld(state,before.Round);
   if(state.Phase==RoundPhase.Results&&before.Phase!=RoundPhase.Results)CutTransformations();
   if(BotAutomationEnabled)botDirector.Step(this,seconds);
   var rules=Session.ObserveSettings().Current;
   for(int i=0;i<bodies.Count;i++) {
    var body=bodies[i]; var p=state.Players[i];var modifiers=AnimalBalance.For(rules,p);
    if(state.Round!=before.Round) { Warp(body,ToVector(p.Position)); vertical[i]=0; motion[i]=default; }
    if(p.Faction!=before.Players[i].Faction||p.CharacterId!=before.Players[i].CharacterId)ChangeVisual(before,state,i,body.transform.position);
    if(state.Phase!=RoundPhase.Results) {
     var input=inputs[i].RoundId==state.Round?inputs[i]:default;
     if(guns[i]!=null&&input.RoundId==state.Round)guns[i].GetComponent<BubbleGunPose>().SetAim(input.Yaw,input.Pitch);
     if(CharacterMotion.Frozen(p)) {
      // Hit-stop holds only this body: no steering, gravity or knockback until it ends. Momentum resumes after.
      Animate(i,new LocomotionState{Grounded=body.isGrounded,AimYaw=input.Yaw,Action=p.Action},seconds);
      var held=body.transform.position;Session.RecordWorldPosition(i,new WorldPosition(held.x,held.y,held.z));inputs[i].Jump=false;continue;
     }
     if(body.isGrounded && vertical[i]<0) vertical[i]=-2;
     if(body.isGrounded && input.Jump){vertical[i]=Mathf.Sqrt(2*22*(p.Faction==Faction.Human?rules.HumanJump:rules.AnimalJump*modifiers.Jump));Effects.Emit(body.transform.position,new Color(.9f,.83f,.64f,.55f),5,.65f);}
     vertical[i]-=22*seconds;
     var direction=Quaternion.Euler(0,input.Yaw,0)*Vector3.ClampMagnitude(new Vector3(input.Right,0,input.Forward),1);
     bool wasGrounded=body.isGrounded;float fallingSpeed=vertical[i];
     var profile=CharacterMotion.Profile(rules,p);
     motion[i]=CharacterMotion.Next(motion[i],CharacterMotion.Restrain(new MotionIntent{DirectionX=direction.x,DirectionZ=direction.z,Grounded=wasGrounded},p),profile,seconds);
     var planar=new Vector3(motion[i].VelocityX,0,motion[i].VelocityZ);
     var from=body.transform.position;
     body.Move((planar+Vector3.up*vertical[i]+pushVelocity[i])*seconds);
     // Animate what the body actually did: a wall-blocked body does not run at full cadence.
     var moved=body.transform.position-from;moved.y=0;float actual=moved.magnitude/seconds;
     var shown=planar.sqrMagnitude>actual*actual?planar.normalized*actual:planar;
     Animate(i,new LocomotionState{VelocityX=shown.x,VelocityZ=shown.z,VerticalSpeed=vertical[i],Grounded=body.isGrounded,AimYaw=input.Yaw,TopSpeed=profile.MaxSpeed,Action=p.Action,ActionProgress=ActionProgress(p,rules)},seconds);
     if(!wasGrounded&&body.isGrounded&&fallingSpeed< -PlaytestSession.HardLandingSpeed){Effects.Emit(body.transform.position,new Color(.9f,.83f,.64f,.6f),7,1);var landed=body.transform.position;Session.RecordWorldPosition(i,new WorldPosition(landed.x,landed.y,landed.z));Session.RecordLanding(i,-fallingSpeed);}
     if(direction.sqrMagnitude>.01f) body.transform.rotation=Quaternion.RotateTowards(body.transform.rotation,Quaternion.LookRotation(direction),540f*seconds);
     if(body.transform.position.y < -12) {
      var closest=returns[0]; float distance=float.MaxValue;
      foreach(var point in returns) { var d=Vector2.SqrMagnitude(new Vector2(point.x-body.transform.position.x,point.z-body.transform.position.z)); if(d<distance) {distance=d;closest=point;} }
      Warp(body,closest); vertical[i]=0; motion[i]=default;
     }
    } else {motion[i]=default;Animate(i,new LocomotionState{Grounded=true,AimYaw=PresentationYaw(i)},seconds);}
    var pos=body.transform.position;
    Session.RecordWorldPosition(i,new WorldPosition(pos.x,pos.y,pos.z));
    var push=pushVelocity[i];CharacterMotion.DecayKnockback(ref push.x,ref push.y,ref push.z,body.isGrounded,seconds);pushVelocity[i]=push;
    inputs[i].Jump=false;
   }
   StepCombatWorld(seconds,state);
   var presented=Session.Observe();PresentEvents(presented);PresentStateAudio(presented,false);PresentMotionAudio(presented,seconds);
  }
  internal static float ActionProgress(PlayerState p,PlaytestValues rules) {
   double left,total;
   switch(p.Action) {
    case ActionPhase.Windup:left=p.AttackWindupRemaining;total=rules.AttackWindupSeconds;break;
    case ActionPhase.Swing:left=p.SwingRemaining;total=CombatRules.SwingSeconds;break;
    case ActionPhase.Stunned:left=p.StunRemaining;total=rules.HitStunSeconds;break;
    default:return 0;
   }
   return total<=0?1:Mathf.Clamp01(1-(float)(left/total));
  }
  internal void ResolveKnockbackContact(int slot,Vector3 normal) {
   float inward=Vector3.Dot(pushVelocity[slot],normal);
   if(inward<0)pushVelocity[slot]-=normal*inward;
  }
  readonly (int round,string id)[] presentedBirths=new (int,string)[12];
  // One path for every faction/character change. A same-round human->animal change is a birth and is
  // presented once per (round, animal): particles plus the short cosmetic pop. Joins and round resets swap silently.
  void ChangeVisual(SessionState before,SessionState after,int slot,Vector3 position) {
   var p=after.Players[slot];
   // The hit that ends the round swaps the model without a new presentation.
   bool birth=before!=null&&before.Round==after.Round&&after.Phase!=RoundPhase.Results&&before.Players[slot].Faction==Faction.Human&&p.Faction==Faction.Animal&&presentedBirths[slot]!=(after.Round,p.CharacterId);
   if(birth){presentedBirths[slot]=(after.Round,p.CharacterId);Effects.Emit(position+Vector3.up*.6f,new Color(1,.67f,.24f),18,2.4f);}
   RefreshVisual(slot,p.Faction,birth);
  }
  /// <summary>Round end: every transformation pop settles and every shrinking ghost disappears at once.</summary>
  void CutTransformations() {
   foreach(var body in bodies) {
    var animator=body.GetComponent<CharacterAnimator>();if(animator!=null)animator.EndTransform();
    foreach(var ghost in body.GetComponentsInChildren<TransformGhost>(true)){ghost.gameObject.SetActive(false);Destroy(ghost.gameObject);}
   }
  }
  public Transform PlayerTransform(int slot) => bodies[slot].transform;
  /// <summary>Locomotion animation currently shown for a slot (presentation only).</summary>
  public CharacterAnimationView ObserveAnimation(int slot)=>bodies[slot].GetComponent<CharacterAnimator>().Observe();
  readonly LocomotionState[] presented=new LocomotionState[12];
  void Animate(int slot,LocomotionState state,float seconds){presented[slot]=state;bodies[slot].GetComponent<CharacterAnimator>().Apply(state,seconds);}
  /// <summary>Host presentation frame for remote viewers: each body's position, facing, aim pitch and the motion it was animated with.</summary>
  public NetworkMotionFrame CaptureMotion()=>new NetworkMotionFrame{HostTime=Session.HostTime,Bodies=Enumerable.Range(0,bodies.Count).Select(i=>{
   var p=bodies[i].transform.position;float pitch=guns[i]!=null?guns[i].GetComponent<BubbleGunPose>().AimPitch:0;
   return NetworkBodyMotion.Encode(new WorldPosition(p.x,p.y,p.z),PresentationYaw(i),pitch,presented[i]);}).ToArray()};
  internal float PresentationYaw(int slot)=>guns[slot]!=null?guns[slot].GetComponent<BubbleGunPose>().AimYaw:bodies[slot].transform.eulerAngles.y;
  static void Warp(CharacterController body,Vector3 position) {body.enabled=false;body.transform.position=position;body.enabled=true;}
  static Vector3 ToVector(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  void RefreshVisual(int slot,Faction faction,bool transformation=false) {
   var parent=bodies[slot].transform;
   if(guns[slot]!=null){guns[slot].gameObject.SetActive(false);Destroy(guns[slot].gameObject);}guns[slot]=faction==Faction.Human?Effects.Gun(parent):null;
   var old=parent.Find("Visual");
   if(old!=null){if(transformation){old.name="Transform ghost";old.gameObject.AddComponent<TransformGhost>();}else{old.gameObject.SetActive(false);Destroy(old.gameObject);}}
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
   foreach(var animator in visual.GetComponentsInChildren<Animator>()){animator.fireEvents=false;animator.applyRootMotion=false;animator.Rebind();animator.Update(0);}
   var gunPose=guns[slot]!=null?guns[slot].GetComponent<BubbleGunPose>():null;
   if(gunPose!=null)gunPose.Bind(visual.transform);
   var locomotion=parent.GetComponent<CharacterAnimator>()??parent.gameObject.AddComponent<CharacterAnimator>();
   locomotion.Bind(visual.transform,faction,Observe().Players[slot].CharacterId,gunPose);
   if(transformation)locomotion.PlayTransform();
   foreach(var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled=false;
   // Every sound goes through the AudioDirector; vendor model audio sources stay silent.
   foreach(var vendor in visual.GetComponentsInChildren<AudioSource>(true))vendor.enabled=false;
  }
 }
 /// <summary>The replaced model shrinks away quickly under the transformation burst. Presentation only.</summary>
 public sealed class TransformGhost : MonoBehaviour {
  const float Seconds=.15f;float age;Vector3 start;
  void Awake(){start=transform.localScale;}
  void Update(){age+=Time.deltaTime;if(age>=Seconds){Destroy(gameObject);return;}transform.localScale=start*(1-age/Seconds);}
 }
}
