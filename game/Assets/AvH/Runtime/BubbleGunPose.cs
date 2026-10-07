using UnityEngine;
namespace AvH {
 // Runs after the Animator: locomotion stays on the legs while both arms hold the emitter.
 public sealed class BubbleGunPose : MonoBehaviour {
  Transform visual,rightUpper,rightLower,rightHand,leftUpper,leftLower,leftHand;
  Transform rightFingers,leftFingers;
  public float AimYaw {get;private set;}
  public float AimPitch {get;private set;}
  /// <summary>Leg heading relative to aim (degrees), set by CharacterAnimator for strafing and backpedalling.</summary>
  public float LegYaw {get;set;}
  Transform spine,chest;
  public void SetAim(float yaw,float pitch){AimYaw=yaw;AimPitch=pitch;}
  public void Bind(Transform model) {
   visual=model;SetAim(transform.eulerAngles.y,0);var animator=model.GetComponentInChildren<Animator>();
   if(animator==null||!animator.isHuman)return;
   rightUpper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);rightLower=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
   leftUpper=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);leftLower=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
   spine=animator.GetBoneTransform(HumanBodyBones.Spine);chest=animator.GetBoneTransform(HumanBodyBones.Chest);
   rightFingers=animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);leftFingers=animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
  }
  void LateUpdate(){ApplyPose();}
  public void ApplyPose() {
   if(rightUpper==null||rightLower==null||rightHand==null||leftUpper==null||leftLower==null||leftHand==null)return;
   var aim=Quaternion.Euler(AimPitch,AimYaw,0);transform.rotation=aim;var flat=Vector3.ProjectOnPlane(transform.forward,Vector3.up);
   if(flat.sqrMagnitude<.001f)flat=visual.forward;
   // Aiming also turns the visible human. Legs may point along a side step (LegYaw);
   // the spine then counter-twists so the chest and arms stay on the aim.
   var facing=Quaternion.LookRotation(flat,Vector3.up);
   visual.rotation=Quaternion.RotateTowards(visual.rotation,facing*Quaternion.Euler(0,LegYaw,0),720*Time.deltaTime);
   float twist=Mathf.Clamp(Mathf.DeltaAngle(visual.eulerAngles.y,facing.eulerAngles.y),-90,90);
   if(spine!=null&&chest!=null&&Mathf.Abs(twist)>.01f){spine.rotation=Quaternion.AngleAxis(twist*.5f,Vector3.up)*spine.rotation;chest.rotation=Quaternion.AngleAxis(twist*.5f,Vector3.up)*chest.rotation;}
   var side=Quaternion.LookRotation(flat,Vector3.up)*Vector3.right;
   transform.position=(rightUpper.position+leftUpper.position)*.5f+side*.1f+transform.forward*.22f-Vector3.up*.14f;
   transform.rotation=aim;
   Solve(rightUpper,rightLower,rightHand,transform.TransformPoint(new Vector3(0,-.1f,0)),side*.5f-Vector3.up*.65f);
   Solve(leftUpper,leftLower,leftHand,transform.TransformPoint(new Vector3(-.07f,-.035f,.22f)),-side*.5f-Vector3.up*.65f);
   OrientHand(rightHand,rightFingers,transform.forward);OrientHand(leftHand,leftFingers,transform.right);
  }
  static void Solve(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 elbowHint) {
   float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);
   var offset=target-upper.position;
   if(offset.sqrMagnitude<.000001f||a<.001f||b<.001f)return;
   float distance=Mathf.Clamp(offset.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
   var direction=offset.normalized;var pole=Vector3.ProjectOnPlane(elbowHint,direction).normalized;
   if(pole.sqrMagnitude<.001f)pole=Vector3.Cross(direction,Mathf.Abs(direction.y)<.9f?Vector3.up:Vector3.right).normalized;
   float along=(a*a+distance*distance-b*b)/(2*distance),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
   var elbow=upper.position+direction*along+pole*height;
   upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
   lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
  }
  static void OrientHand(Transform hand,Transform fingers,Vector3 direction) {
   if(fingers!=null)hand.rotation=Quaternion.FromToRotation(fingers.position-hand.position,direction)*hand.rotation;
  }
 }
}
