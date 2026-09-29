using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AvH {
 [Serializable] public sealed class BubbleState {
  public int Id,OwnerSlot,Round;
  public WorldPosition Position,Direction;
  public float RemainingLife,Travelled;
 }
 public sealed partial class UnityPlaytestSession {
  sealed class Bubble {
   public int Id,Owner,Round;public Vector3 Position,Direction;public float Life,Travelled,Radius,Speed,Range,PushForce;public bool FriendlyPush;
   public GameObject Visual;
  }
  readonly List<Bubble> bubbles=new List<Bubble>();
  readonly List<(GameObject visual,float remaining)> bursts=new List<(GameObject,float)>();
  readonly Vector3[] pushVelocity=new Vector3[12];
  int nextBubbleId;
  Material bubbleMaterial;
  public BubbleState[] ObserveBubbles()=>bubbles.Select(b=>new BubbleState{Id=b.Id,OwnerSlot=b.Owner,Round=b.Round,Position=ToPosition(b.Position),Direction=ToPosition(b.Direction),RemainingLife=b.Life,Travelled=b.Travelled}).ToArray();
  static WorldPosition ToPosition(Vector3 v)=>new WorldPosition(v.x,v.y,v.z);
  void PrepareCombatWorld(SessionState state,int oldRound) {
   if(state.Round!=oldRound){foreach(var bubble in bubbles)Destroy(bubble.Visual);bubbles.Clear();Array.Clear(pushVelocity,0,pushVelocity.Length);Array.Clear(inputs,0,inputs.Length);}
   var rules=Session.ObserveSettings().Current;
   for(int i=0;i<bodies.Count;i++)for(int j=i+1;j<bodies.Count;j++) {
    bool same=state.Players[i].Faction==state.Players[j].Faction;
    Physics.IgnoreCollision(bodies[i],bodies[j],same?!rules.FriendlyCollision:!rules.EnemyCollision);
   }
  }
  void StepCombatWorld(float seconds,SessionState beforeCombat) {
   for(int i=bursts.Count-1;i>=0;i--){var burst=bursts[i];burst.remaining-=seconds;if(burst.remaining<=0){Destroy(burst.visual);bursts.RemoveAt(i);}else{burst.visual.transform.localScale*=1+seconds*5;bursts[i]=burst;}}
   for(int i=bubbles.Count-1;i>=0;i--) {
    var bubble=bubbles[i];float distance=Mathf.Min(bubble.Speed*seconds,bubble.Range-bubble.Travelled,bubble.Speed*bubble.Life);
    bool popped=false;
    foreach(var hit in Physics.SphereCastAll(bubble.Position,bubble.Radius,bubble.Direction,distance,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance)) {
     int target=bodies.IndexOf(hit.collider as CharacterController);
     if(target==bubble.Owner)continue;
     if(target>=0 && beforeCombat.Players[target].Faction==Faction.Human && !bubble.FriendlyPush)continue;
     bubble.Position+=bubble.Direction*hit.distance;
     if(target>=0)pushVelocity[target]+=bubble.Direction*bubble.PushForce+Vector3.up*1.5f;
     Pop(bubble);popped=true;break;
    }
    if(!popped){bubble.Position+=bubble.Direction*distance;bubble.Travelled+=distance;bubble.Life-=seconds;bubble.Visual.transform.position=bubble.Position;
     if(bubble.Life<=0 || bubble.Travelled>=bubble.Range || beforeCombat.Phase==RoundPhase.Results || bubble.Round!=beforeCombat.Round){Destroy(bubble.Visual);popped=true;}}
    if(popped)bubbles.RemoveAt(i);
   }
   if(beforeCombat.Phase==RoundPhase.Results)return;
   for(int slot=0;slot<bodies.Count;slot++) {
    var input=inputs[slot];if(input.RoundId!=beforeCombat.Round)continue;
    if(input.Reload)Session.TryReload(slot,input.RoundId);
    if(input.Attack) {
     var state=Session.Observe();
     if(state.Players[slot].Faction==Faction.Human){if(Session.TryFire(slot,input.RoundId))FireBubble(slot,input);}
     else Melee(slot,input);
    }
    inputs[slot].Reload=false;
   }
   var after=Session.Observe();
   for(int slot=0;slot<bodies.Count;slot++)if(after.Players[slot].Faction!=beforeCombat.Players[slot].Faction)RefreshVisual(slot,after.Players[slot].Faction);
  }
  void FireBubble(int slot,PlayerInput input) {
   var rules=Session.ObserveSettings().Current;
   var direction=Quaternion.Euler(input.Pitch,input.Yaw,0)*Vector3.forward;
   var position=bodies[slot].transform.position+Vector3.up*.9f;
   var visual=GameObject.CreatePrimitive(PrimitiveType.Sphere);visual.name="Bubble";visual.transform.SetParent(transform);visual.transform.position=position;visual.transform.localScale=Vector3.one*rules.BubbleRadius*2;
   visual.GetComponent<Collider>().enabled=false;Destroy(visual.GetComponent<Collider>());
   if(bubbleMaterial==null){bubbleMaterial=PrototypeVillage.Material(new Color(.25f,.8f,1,.55f));bubbleMaterial.SetFloat("_Mode",3);bubbleMaterial.SetInt("_SrcBlend",5);bubbleMaterial.SetInt("_DstBlend",10);bubbleMaterial.SetInt("_ZWrite",0);bubbleMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");bubbleMaterial.renderQueue=3000;}
   visual.GetComponent<Renderer>().sharedMaterial=bubbleMaterial;
   bubbles.Add(new Bubble{Id=++nextBubbleId,Owner=slot,Round=input.RoundId,Position=position,Direction=direction,Life=rules.BubbleLifetime,Radius=rules.BubbleRadius,Speed=rules.BubbleSpeed,Range=rules.BubbleRange,PushForce=rules.PushForce,FriendlyPush=rules.FriendlyPush,Visual=visual});
  }
  void Pop(Bubble bubble){bubble.Visual.transform.position=bubble.Position;bursts.Add((bubble.Visual,.15f));}
  void Melee(int slot,PlayerInput input) {
   var direction=Quaternion.Euler(0,input.Yaw,0)*Vector3.forward;
   var origin=bodies[slot].transform.position+Vector3.up*.9f;
   foreach(var collider in Physics.OverlapSphere(origin,CombatRules.MeleeDistance-.1f,~0,QueryTriggerInteraction.Ignore).OrderBy(c=>(c.bounds.center-origin).sqrMagnitude)) {
    int victim=bodies.IndexOf(collider as CharacterController);if(victim<0||victim==slot)continue;
    var offset=bodies[victim].transform.position-bodies[slot].transform.position;
    if(offset.sqrMagnitude>.04f && Vector3.Dot(direction,offset.normalized)<.2f)continue;
    if(Physics.Linecast(origin,bodies[victim].transform.position+Vector3.up*.9f,out var obstruction,~0,QueryTriggerInteraction.Ignore) && obstruction.collider!=bodies[victim])continue;
    if(Session.TryMeleeHit(slot,victim,input.RoundId))break;
   }
  }
  void OnDestroy(){if(bubbleMaterial!=null)Destroy(bubbleMaterial);}
 }
}
