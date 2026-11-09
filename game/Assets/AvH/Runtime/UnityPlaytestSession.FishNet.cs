using UnityEngine;
namespace AvH {
 public sealed partial class UnityPlaytestSession {
  internal FishRoomBridge FishBridge;
  readonly FishPredictedMotor[] fishMotors=new FishPredictedMotor[12];
  public bool FishControls(int slot)=>slot>=0&&slot<12&&fishMotors[slot]!=null&&fishMotors[slot].Attached;
  internal Transform FishGraphicalRoot(int slot){
   var body=bodies[slot].transform;var root=body.Find("Predicted presentation");if(root!=null)return root;
   root=new GameObject("Predicted presentation").transform;root.SetParent(body,false);
   // Smoothing owns this parent; CharacterAnimator and gun IK retain their own local transforms.
   var children=new System.Collections.Generic.List<Transform>();foreach(Transform child in body)if(child!=root)children.Add(child);
   foreach(var child in children)child.SetParent(root,false);return root;
  }
  internal void SetFishMotor(int slot,FishPredictedMotor motor){fishMotors[slot]=motor;bodies[slot].enabled=true;FishGraphicalRoot(slot);}
  internal void ClearFishMotor(int slot){if(slot>=0&&slot<12){fishMotors[slot]=null;var root=slot<bodies.Count&&bodies[slot]!=null?bodies[slot].transform.Find("Predicted presentation"):null;if(root!=null){root.localPosition=Vector3.zero;root.localRotation=Quaternion.identity;}}}
  public Transform PlayerBodyTransform(int slot)=>bodies[slot].transform;
  internal void ApplyFishCombatInput(int slot,FishMoveData input){inputs[slot]=new PlayerInput{Yaw=input.Yaw,Pitch=Mathf.Clamp(input.Pitch,-80,80),Attack=input.Attack,Reload=input.Reload,RoundId=input.Round};}
  internal FishMotorState CaptureFishState(int slot){
   var p=Observe().Players[slot];var rules=ObserveActiveSettings().Current;var modifiers=AnimalBalance.For(rules,p);
   return new FishMotorState{Position=bodies[slot].transform.position,Rotation=bodies[slot].transform.rotation,Vertical=vertical[slot],Motion=motion[slot],Push=pushVelocity[slot],Grounded=bodies[slot].isGrounded,Round=Observe().Round,
    Action=p.Action,ActionProgress=ActionProgress(p,rules),Profile=CharacterMotion.Profile(rules,p),JumpHeight=p.Faction==Faction.Human?rules.HumanJump:rules.AnimalJump*modifiers.Jump,Control=p.StunRemaining>0?CharacterMotion.StunnedControl:1,Frozen=CharacterMotion.Frozen(p)||Observe().Phase==RoundPhase.Results};
  }
  internal void RestoreFishState(int slot,FishMotorState state){
   Warp(bodies[slot],state.Position);bodies[slot].transform.rotation=state.Rotation;vertical[slot]=state.Vertical;motion[slot]=state.Motion;pushVelocity[slot]=state.Push;
  }
  internal void PresentFishMovement(int slot,FishMotorState state,float yaw,float pitch,float dt){
   Animate(slot,new LocomotionState{VelocityX=state.Motion.VelocityX,VelocityZ=state.Motion.VelocityZ,VerticalSpeed=state.Vertical,Grounded=state.Grounded,AimYaw=yaw,TopSpeed=state.Profile.MaxSpeed,Action=state.Action,ActionProgress=state.ActionProgress},dt);
   if(guns[slot]!=null)guns[slot].GetComponent<BubbleGunPose>().SetAim(yaw,pitch);
  }
  internal FishMotorState SimulateFishMovement(int slot,FishMotorState state,FishMoveData input,float dt,bool present,bool server){
   var body=bodies[slot];body.enabled=true;
   if(state.Frozen)return state;
   if(input.Jump)state.JumpBuffer=.12f;else state.JumpBuffer=Mathf.Max(0,state.JumpBuffer-dt);
   if(state.Grounded&&state.Vertical<0)state.Vertical=-2;
   if(state.Grounded&&state.JumpBuffer>0){state.Vertical=Mathf.Sqrt(44*state.JumpHeight);state.Grounded=false;state.JumpBuffer=0;}
   state.Vertical-=22*dt;
   var direction=Quaternion.Euler(0,input.Yaw,0)*Vector3.ClampMagnitude(new Vector3(Mathf.Clamp(input.Right,-1,1),0,Mathf.Clamp(input.Forward,-1,1)),1);
   state.Motion=CharacterMotion.Next(state.Motion,new MotionIntent{DirectionX=direction.x*state.Control,DirectionZ=direction.z*state.Control,Grounded=state.Grounded},state.Profile,dt);
   bool wasGrounded=state.Grounded;float falling=state.Vertical;var before=body.transform.position;
   pushVelocity[slot]=state.Push;
   var flags=body.Move((new Vector3(state.Motion.VelocityX,state.Vertical,state.Motion.VelocityZ)+state.Push)*dt);
   state.Push=pushVelocity[slot];
   state.Grounded=(flags&CollisionFlags.Below)!=0;
   if((flags&CollisionFlags.Above)!=0&&state.Vertical>0)state.Vertical=0;
   if(state.Grounded&&state.Vertical<0)state.Vertical=-2;
   if(direction.sqrMagnitude>.01f)body.transform.rotation=Quaternion.RotateTowards(body.transform.rotation,Quaternion.LookRotation(direction),540*dt);
   // Only the host judges a fall; a predicting guest keeps falling until the host's restart corrects it.
   if(body.transform.position.y<FallLine&&(server||!GameMaps.FallTurnsAnimal(builtMap))){Warp(body,server?Restart(slot,body.transform.position):NearestReturn(body.transform.position));state.Vertical=0;state.Motion=default;state.Push=Vector3.zero;state.Grounded=false;}
   state.Position=body.transform.position;state.Rotation=body.transform.rotation;vertical[slot]=state.Vertical;motion[slot]=state.Motion;
   CharacterMotion.DecayKnockback(ref state.Push.x,ref state.Push.y,ref state.Push.z,state.Grounded,dt);pushVelocity[slot]=state.Push;
   if(server){Session.RecordWorldPosition(slot,new WorldPosition(state.Position.x,state.Position.y,state.Position.z));if(!wasGrounded&&state.Grounded&&falling< -PlaytestSession.HardLandingSpeed)Session.RecordLanding(slot,-falling);}
   return state;
  }
 }
}
