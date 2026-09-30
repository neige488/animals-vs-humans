using UnityEngine;
namespace AvH {
 // Runs after the Animator: locomotion stays on the legs while both arms hold the emitter.
 public sealed class BubbleGunPose : MonoBehaviour {
  Transform visual,chest,rightUpper,rightLower,rightHand,leftUpper,leftLower,leftHand;
  Transform rightFingers,leftFingers;
  public void Bind(Transform model) {
   visual=model;var animator=model.GetComponentInChildren<Animator>();
   if(animator==null||!animator.isHuman)return;
   chest=animator.GetBoneTransform(HumanBodyBones.Chest);
   rightUpper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);rightLower=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
   leftUpper=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);leftLower=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
   rightFingers=animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);leftFingers=animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
  }
  void LateUpdate(){ApplyPose();}
  public void ApplyPose() {
   if(chest==null||rightHand==null||leftHand==null)return;
   var aim=transform.rotation;var flat=Vector3.ProjectOnPlane(transform.forward,Vector3.up);
   if(flat.sqrMagnitude<.001f)flat=visual.forward;
   // Aiming also turns the visible human, including when strafing or standing still.
   visual.rotation=Quaternion.RotateTowards(visual.rotation,Quaternion.LookRotation(flat,Vector3.up),720*Time.deltaTime);
   var side=Quaternion.LookRotation(flat,Vector3.up)*Vector3.right;
   transform.position=(rightUpper.position+leftUpper.position)*.5f+side*.1f+transform.forward*.22f-Vector3.up*.14f;
   transform.rotation=aim;
   Solve(rightUpper,rightLower,rightHand,transform.TransformPoint(new Vector3(0,-.1f,0)),side*.5f-Vector3.up*.65f);
   Solve(leftUpper,leftLower,leftHand,transform.TransformPoint(new Vector3(-.07f,-.035f,.22f)),-side*.5f-Vector3.up*.65f);
   OrientHand(rightHand,rightFingers,transform.forward);OrientHand(leftHand,leftFingers,transform.right);
  }
  static void Solve(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 elbowHint) {
   float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);
   var offset=target-upper.position;float distance=Mathf.Clamp(offset.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
   if(offset.sqrMagnitude<.000001f||a<.001f||b<.001f)return;
   var direction=offset.normalized;var pole=Vector3.ProjectOnPlane(elbowHint,direction).normalized;
   if(pole.sqrMagnitude<.001f)pole=Vector3.Cross(direction,Vector3.forward).normalized;
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
