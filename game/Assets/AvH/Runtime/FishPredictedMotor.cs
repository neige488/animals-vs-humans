using System.Linq;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using FishNet.Component.Transforming;
using FishNet.Component.Transforming.Beta;
using UnityEngine;
namespace AvH {
 public struct FishMotorState {
  public Vector3 Position,Push;public Quaternion Rotation;public float Vertical,JumpBuffer,Airborne;
  public ActionPhase Action;public float ActionProgress;public MotionState Motion;public MotionProfile Profile;public float JumpHeight,Control;public bool Grounded,Frozen;public int Round;
  // Ability effects on this body (server-decided): bent gravity and a straight-line run that steers itself.
  public AirRule Air;public bool Locked;public float LockYaw;
 }
 public struct FishMoveData : IReplicateData {
  public float Right,Forward,Yaw,Pitch;public bool Jump,Attack,Reload,Ability;public int Round;uint tick;
  public uint GetTick()=>tick;public void SetTick(uint value)=>tick=value;public void Dispose(){}
 }
 public struct FishReconcileData : IReconcileData {
  public FishMotorState State;uint tick;
  public uint GetTick()=>tick;public void SetTick(uint value)=>tick=value;public void Dispose(){}
 }
 /// <summary>FishNet stores inputs and replays them after full authoritative state restoration.</summary>
 [DefaultExecutionOrder(-50)] public sealed class FishPredictedMotor : NetworkBehaviour {
  readonly SyncVar<int> slot=new SyncVar<int>(-1);
  FishRoomBridge bridge;UnityPlaytestSession world;FishMotorState state;
  UniversalTickSmoother smoother;Transform cameraAnchor;float aimYaw,aimPitch;Vector3 beforeCorrection,graphicsBefore;Quaternion graphicsRotation;bool correcting;
  public float MaxCorrection {get;private set;}
  public bool Attached=>world!=null&&world.Session!=null&&slot.Value>=0;
  public int Slot=>slot.Value;
  public bool Grounded=>state.Grounded;
  public int ReplicateCount {get;private set;}public int ReconcileCount {get;private set;}public int ReplayCount {get;private set;}
  bool processedServerInput;uint lastProcessedInputTick;
  public uint LastProcessedInputTick=>lastProcessedInputTick;
  public uint LastRestoredTick {get;private set;}
  public Vector3 LastRestoredPosition {get;private set;}
  public void SetSlot(int value)=>slot.Value=value;
  public void Attach(FishRoomBridge owner){bridge=owner;TryAttach();}
  bool TryAttach(){
   if(bridge==null)bridge=Object.FindObjectsByType<FishRoomBridge>(FindObjectsSortMode.None).FirstOrDefault(b=>b.Manager==NetworkManager);
   if(bridge==null||bridge.World.Session==null||slot.Value<0||slot.Value>=12)return false;
   if(world==null){world=bridge.World;state=world.CaptureFishState(slot.Value);bridge.RegisterMotor(slot.Value,this);
    if(IsOwner||IsServerStarted){world.SetFishMotor(slot.Value,this);cameraAnchor=new GameObject("Predicted camera anchor").transform;cameraAnchor.SetParent(world.transform);cameraAnchor.position=state.Position;}}
   if((IsOwner||IsServerStarted)&&!world.FishControls(slot.Value)){world.SetFishMotor(slot.Value,this);if(cameraAnchor==null){cameraAnchor=new GameObject("Predicted camera anchor").transform;cameraAnchor.SetParent(world.transform);cameraAnchor.position=state.Position;}}
   return true;
  }
  public override void OnStartNetwork(){TimeManager.OnPreTick+=BeforeTick;TimeManager.OnTick+=Tick;TimeManager.OnPostTick+=PostTick;}
  public override void OnStartClient(){PredictionManager.OnPreReconcile+=BeforeReconcile;PredictionManager.OnPostReconcile+=AfterReconcile;PredictionManager.OnPostReplicateReplay+=AfterReplay;TimeManager.OnRoundTripTimeUpdated+=UpdateInterpolation;}
  public override void OnStopClient(){PredictionManager.OnPreReconcile-=BeforeReconcile;PredictionManager.OnPostReconcile-=AfterReconcile;PredictionManager.OnPostReplicateReplay-=AfterReplay;TimeManager.OnRoundTripTimeUpdated-=UpdateInterpolation;smoother?.ResetState();smoother=null;}
  public override void OnStopNetwork(){TimeManager.OnPreTick-=BeforeTick;TimeManager.OnTick-=Tick;TimeManager.OnPostTick-=PostTick;smoother?.ResetState();smoother=null;if(world!=null)world.ClearFishMotor(slot.Value);if(cameraAnchor!=null)Destroy(cameraAnchor.gameObject);world=null;}
  void Tick(){
   processedServerInput=false;
   if(!TryAttach()||!IsServerStarted&&!IsOwner)return;
   // Server owns tuning, combat restraints, knockback and round resets. Input contains no client position or velocity.
   if(IsServerStarted){var authority=world.CaptureFishState(slot.Value);state.Profile=authority.Profile;state.JumpHeight=authority.JumpHeight;state.Control=authority.Control;state.Frozen=authority.Frozen;state.Action=authority.Action;state.ActionProgress=authority.ActionProgress;state.Push=authority.Push;state.Air=authority.Air;state.Locked=authority.Locked;state.LockYaw=authority.LockYaw;
    if(world.TakeFishKick(slot.Value,out var kick)){state.Vertical=kick;state.Grounded=false;state.Airborne=CharacterMotion.CoyoteSeconds;}
    if(state.Round!=authority.Round||Vector3.Distance(state.Position,authority.Position)>NetworkBodyMotion.TeleportDistance)state=authority;}
   FishMoveData input=default;
   if(IsOwner||IsServerStarted&&!Owner.IsValid){var value=bridge.TakeInput(slot.Value);input=new FishMoveData{Right=value.Right,Forward=value.Forward,Yaw=value.Yaw,Pitch=value.Pitch,Jump=value.Jump,Attack=value.Attack,Reload=value.Reload,Ability=value.Ability,Round=value.RoundId};}
   Move(input);
  }
  void BeforeTick(){smoother?.OnPreTick();}
  void PostTick(){
   // FishNet's default estimated acknowledgement advances even when no input
   // arrived. Our external controller waits for ordered input, so acknowledge
   // only what was actually simulated, never elapsed server time.
   if(IsServerStarted&&Owner.IsValid&&lastProcessedInputTick!=0)
    Owner.ReplicateTick.Update(TimeManager,lastProcessedInputTick,FishNet.Managing.Timing.EstimatedTick.OldTickOption.SetRemoteTick);
   if(Attached&&(IsServerStarted||IsOwner))CreateReconcile();smoother?.OnPostTick(TimeManager.LocalTick);
  }
  void AfterReplay(uint clientTick,uint serverTick){smoother?.OnPostReplicateReplay(clientTick);}
  void UpdateInterpolation(long rtt){smoother?.UpdateRealtimeInterpolation();}
  public override void CreateReconcile(){if(Attached&&(!IsServerStarted||processedServerInput))Restore(new FishReconcileData{State=state});}
  // A short Wi-Fi loss run can exceed FishNet's three-input redundancy window.
  // Ordered delivery retains released keys and every movement tick; reconcile
  // state stays unreliable so stale corrections never block fresh state.
  [Replicate] void Move(FishMoveData input,ReplicateState replicateState=ReplicateState.Invalid,Channel channel=Channel.Reliable){
   if(!Attached)return;
   // This controller integrates every submitted tick itself. An absent input
   // is not a submitted zero-input tick: simulating and acknowledging it would
   // discard owner history before the delayed reliable input actually arrives.
   if(IsServerStarted&&!replicateState.ContainsCreated())return;
   if(IsServerStarted){processedServerInput=true;lastProcessedInputTick=input.GetTick();}
   ReplicateCount++;if(replicateState.ContainsReplayed())ReplayCount++;
   bool valid=input.Round==state.Round&&Finite(input.Right)&&Finite(input.Forward)&&Finite(input.Yaw)&&Finite(input.Pitch);
   if(!valid)input=default;
   if(!replicateState.ContainsReplayed()){aimYaw=input.Yaw;aimPitch=input.Pitch;}
   if(IsServerStarted&&!replicateState.ContainsReplayed())world.ApplyFishCombatInput(slot.Value,input);
   state=world.SimulateFishMovement(slot.Value,state,input,(float)TimeManager.TickDelta,!replicateState.ContainsReplayed(),IsServerStarted);
  }
  static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
  [Reconcile] void Restore(FishReconcileData data,Channel channel=Channel.Unreliable){if(!Attached)return;LastRestoredTick=data.GetTick();LastRestoredPosition=data.State.Position;ReconcileCount++;state=data.State;world.RestoreFishState(slot.Value,state);}
  void BeforeReconcile(uint clientTick,uint serverTick){if(!Attached||!IsOwner)return;beforeCorrection=world.PlayerBodyTransform(slot.Value).position;var graphics=world.FishGraphicalRoot(slot.Value);graphicsBefore=graphics.position;graphicsRotation=graphics.rotation;correcting=true;}
  void AfterReconcile(uint clientTick,uint serverTick){if(!correcting||!Attached)return;correcting=false;float correction=Vector3.Distance(beforeCorrection,world.PlayerBodyTransform(slot.Value).position);MaxCorrection=Mathf.Max(MaxCorrection,correction);
   // This motor drives a controller outside the NetworkObject hierarchy. Preserve its graphical world pose across the entire replay, as FishNet does for its own graphical child.
   if(correction>NetworkBodyMotion.TeleportDistance){smoother?.Teleport();return;}
   world.FishGraphicalRoot(slot.Value).SetPositionAndRotation(graphicsBefore,graphicsRotation);}
  void Update(){
   if(!TryAttach()||!IsOwner&&!IsServerStarted)return;
   // Animation advances once per rendered frame from prediction state, never from the slow room snapshot.
   world.PresentFishMovement(slot.Value,state,aimYaw,aimPitch,Time.deltaTime);
   if(IsOwner&&!IsServerStarted&&smoother==null){
    var settings=new InitializationSettings{TargetTransform=world.PlayerBodyTransform(slot.Value)};
    settings.SetNetworkedRuntimeValues(this,world.FishGraphicalRoot(slot.Value),false);
    // The owner already predicts current input. An RTT-sized graphical history
    // makes their own body keep sliding after release; only interpolate two ticks.
    var movement=new MovementSettings(true){InterpolationValue=2,AdaptiveInterpolationValue=AdaptiveInterpolationType.Off,EnableTeleport=true,TeleportThreshold=NetworkBodyMotion.TeleportDistance};
    smoother=new UniversalTickSmoother();smoother.Initialize(settings,movement,movement);
    // Reconcile may already have offset the graphical child before its first rendered frame.
    // That transient pose is never the permanent tracker origin (otherwise a guest can stay underground).
    smoother.TrySetGraphicalTrackerLocalProperties(new TransformProperties(Vector3.zero,Quaternion.identity,Vector3.one));
    smoother.StartSmoother();
   }
   smoother?.OnUpdate(Time.deltaTime);
  }
  void LateUpdate(){if(Attached&&cameraAnchor!=null)cameraAnchor.position=world.FishGraphicalRoot(slot.Value).position;}
  public Transform CameraAnchor=>cameraAnchor;
 }
}
