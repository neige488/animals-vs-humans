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
  readonly List<(GameObject visual,float remaining,int id)> bursts=new List<(GameObject,float,int)>();
  readonly Vector3[] pushVelocity=new Vector3[12];
  readonly int[] bubbleHits=new int[12];
  // Actual authoritative physics hits, for combat feedback and session diagnostics.
  public int[] ObserveBubbleHits()=>(int[])bubbleHits.Clone();
  int nextBubbleId;
  PrimitiveEffects effects;
  public PrimitiveEffects Effects {get {if(effects==null){var root=new GameObject("Primitive effects");root.transform.SetParent(transform,false);effects=root.AddComponent<PrimitiveEffects>();effects.Initialize();}return effects;}}
  public BubbleState[] ObserveBubbles()=>remoteVisuals!=null?remoteVisuals.Bubbles.Select(b=>new BubbleState{Id=b.Id,OwnerSlot=b.OwnerSlot,Round=b.Round,Position=b.Position,Direction=b.Direction,RemainingLife=b.RemainingLife,Travelled=b.Travelled}).ToArray():bubbles.Select(b=>new BubbleState{Id=b.Id,OwnerSlot=b.Owner,Round=b.Round,Position=ToPosition(b.Position),Direction=ToPosition(b.Direction),RemainingLife=b.Life,Travelled=b.Travelled}).ToArray();
  public NetworkBurst[] ObserveBursts()=>bursts.Select(b=>new NetworkBurst{Id=b.id,Position=ToPosition(b.visual.transform.position),Remaining=b.remaining}).ToArray();
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
   for(int i=bursts.Count-1;i>=0;i--){var burst=bursts[i];burst.remaining-=seconds;if(burst.remaining<=0){Destroy(burst.visual);bursts.RemoveAt(i);}else bursts[i]=burst;}
   for(int i=bubbles.Count-1;i>=0;i--) {
    var bubble=bubbles[i];float distance=Mathf.Min(bubble.Speed*seconds,bubble.Range-bubble.Travelled,bubble.Speed*bubble.Life);
    bool popped=false;
    // Light props have no colliders; the nearest one the sweep meets takes the bubble when nothing solid comes first.
    int prop=-1;float propAt=float.MaxValue;bool propHit=props!=null&&props.Hit(bubble.Position,bubble.Direction,bubble.Radius,distance,out prop,out propAt);
    foreach(var hit in Physics.SphereCastAll(bubble.Position,bubble.Radius,bubble.Direction,distance,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance)) {
     int target=bodies.IndexOf(hit.collider as CharacterController);
     if(target==bubble.Owner)continue;
     if(target>=0 && beforeCombat.Players[target].Faction==Faction.Human && !bubble.FriendlyPush)continue;
     if(propHit&&propAt<=hit.distance)break;
     bubble.Position+=bubble.Direction*hit.distance;
     if(target>=0){bubbleHits[target]++;pushVelocity[target]+=(bubble.Direction*bubble.PushForce+Vector3.up*1.5f)*AnimalBalance.For(Session.ObserveSettings().Current,beforeCombat.Players[target]).Knockback;Session.RecordBubbleHit(target,bubble.Owner);}
     Pop(bubble);popped=true;break;
    }
    if(!popped&&propHit){bubble.Position+=bubble.Direction*propAt;props.Push(prop,bubble.Direction*bubble.PushForce*PushProps.BubbleShare);Pop(bubble);popped=true;}
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
     // A zero-windup swing is judged here, in slot order, exactly like the original click-frame rule.
     else if(Session.TryStartAttack(slot,input.RoundId)&&Session.AttackDue(slot))Melee(slot,beforeCombat.Round);
    }
    inputs[slot].Reload=false;
   }
   // Tells that reached their strike moment during this step's Advance are judged now (already-judged swings are no longer due).
   for(int slot=0;slot<bodies.Count;slot++)if(Session.AttackDue(slot))Melee(slot,beforeCombat.Round);
   var after=Session.Observe();
   for(int slot=0;slot<bodies.Count;slot++)if(after.Players[slot].Faction!=beforeCombat.Players[slot].Faction)ChangeVisual(beforeCombat,after,slot,bodies[slot].transform.position);
   if(after.Phase==RoundPhase.Results&&beforeCombat.Phase!=RoundPhase.Results)CutTransformations();
  }
  void FireBubble(int slot,PlayerInput input) {
   var rules=Session.ObserveSettings().Current;
   var direction=Quaternion.Euler(input.Pitch,input.Yaw,0)*Vector3.forward;
   var position=bodies[slot].transform.position+Vector3.up*1.3f+Quaternion.Euler(0,input.Yaw,0)*Vector3.right*.1f;
   var visual=Effects.Bubble(transform,rules.BubbleRadius);visual.name="Bubble";visual.transform.position=position;
   Effects.Emit(position+direction*.6f,new Color(.63f,.93f,1,.7f),4,.65f,false);
   Audio.Play("fire",position);
   bubbles.Add(new Bubble{Id=++nextBubbleId,Owner=slot,Round=input.RoundId,Position=position,Direction=direction,Life=rules.BubbleLifetime,Radius=rules.BubbleRadius,Speed=rules.BubbleSpeed,Range=rules.BubbleRange,PushForce=rules.PushForce,FriendlyPush=rules.FriendlyPush,Visual=visual});
  }
  void Pop(Bubble bubble){Destroy(bubble.Visual);var marker=new GameObject("Bubble impact");marker.transform.SetParent(transform,false);marker.transform.position=bubble.Position;bursts.Add((marker,.15f,bubble.Id));Effects.Emit(bubble.Position,new Color(.66f,.92f,1,.9f),12,1.9f);Audio.Play("pop",bubble.Position);}
  void Melee(int slot,int round) {
   var input=inputs[slot];float yaw=input.RoundId==round?input.Yaw:bodies[slot].transform.eulerAngles.y;
   var direction=Quaternion.Euler(0,yaw,0)*Vector3.forward;
   var origin=bodies[slot].transform.position+Vector3.up*.9f;
   foreach(var collider in Physics.OverlapSphere(origin,CombatRules.MeleeDistance-.1f,~0,QueryTriggerInteraction.Ignore).OrderBy(c=>(c.bounds.center-origin).sqrMagnitude)) {
    int victim=bodies.IndexOf(collider as CharacterController);if(victim<0||victim==slot)continue;
    var offset=bodies[victim].transform.position-bodies[slot].transform.position;
    if(offset.sqrMagnitude>.04f && Vector3.Dot(direction,offset.normalized)<.2f)continue;
    if(Physics.Linecast(origin,bodies[victim].transform.position+Vector3.up*.9f,out var obstruction,~0,QueryTriggerInteraction.Ignore) && obstruction.collider!=bodies[victim])continue;
    if(Session.TryMeleeHit(slot,victim,round))return;
   }
   Session.ResolveAttackMiss(slot,round);
  }
 }
}
