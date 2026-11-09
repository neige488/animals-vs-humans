using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AvH {
 [Serializable] public sealed class BubbleState {
  public int Id,OwnerSlot,Round;
  public WorldPosition Position,Direction;
  public float RemainingLife,Travelled;
  public ToolKind Kind;
 }
 public sealed partial class UnityPlaytestSession {
  sealed class Bubble {
   public int Id,Owner,Round,Bounces;public ToolKind Kind;public Vector3 Position,Direction;public float Life,Travelled,Radius,Speed,Range,PushForce;public bool FriendlyPush;
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
  public BubbleState[] ObserveBubbles()=>remoteVisuals!=null?remoteVisuals.Bubbles.Select(b=>new BubbleState{Id=b.Id,OwnerSlot=b.OwnerSlot,Round=b.Round,Position=b.Position,Direction=b.Direction,RemainingLife=b.RemainingLife,Travelled=b.Travelled,Kind=(ToolKind)b.Kind}).ToArray():bubbles.Select(b=>new BubbleState{Id=b.Id,OwnerSlot=b.Owner,Round=b.Round,Position=ToPosition(b.Position),Direction=ToPosition(b.Direction),RemainingLife=b.Life,Travelled=b.Travelled,Kind=b.Kind}).ToArray();
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
    bool popped=false,bounced=false;
    // Light props have no colliders; the nearest one the sweep meets takes the bubble when nothing solid comes first.
    int prop=-1;float propAt=float.MaxValue;bool propHit=props!=null&&props.Hit(bubble.Position,bubble.Direction,bubble.Radius,distance,out prop,out propAt);
    foreach(var hit in Physics.SphereCastAll(bubble.Position,bubble.Radius,bubble.Direction,distance,Layers.World,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance)) {
     var hitWorld=hit.collider.GetComponentInParent<UnityPlaytestSession>();if(hitWorld!=null&&hitWorld!=this)continue;
     int target=bodies.IndexOf(hit.collider as CharacterController);
     if(target==bubble.Owner)continue;
     if(target>=0 && beforeCombat.Players[target].Faction==Faction.Human && !bubble.FriendlyPush)continue;
     if(propHit&&propAt<=hit.distance)break;
     // A rubber disc bounces off walls and floors a couple of times before it stops; it can come back at a friend.
     if(target<0&&bubble.Kind==ToolKind.Disc&&bubble.Bounces<Tools.DiscBounces) {
      bubble.Position+=bubble.Direction*hit.distance+hit.normal*.03f;bubble.Travelled+=hit.distance;bubble.Bounces++;
      bubble.Direction=Vector3.Reflect(bubble.Direction,hit.normal).normalized;
      Effects.Emit(bubble.Position,DiscChips,3,.8f,false);Audio.Play("disc-bounce",bubble.Position);bounced=true;break;
     }
     bubble.Position+=bubble.Direction*hit.distance;
     if(target>=0){bubbleHits[target]++;pushVelocity[target]+=(bubble.Direction*bubble.PushForce+Vector3.up*Tools.Lift(bubble.Kind))*AnimalBalance.For(Session.ObserveSettings().Current,beforeCombat.Players[target]).Knockback;Session.RecordToolHit(target,bubble.Owner,bubble.Kind);}
     Pop(bubble);popped=true;break;
    }
    if(bounced)continue;
    if(!popped&&propHit){bubble.Position+=bubble.Direction*propAt;props.Push(prop,bubble.Direction*bubble.PushForce*PushProps.BubbleShare,bubble.Owner);Pop(bubble);popped=true;}
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
     if(state.Players[slot].Faction==Faction.Human){var tool=state.Players[slot].Tool;if(Session.TryFire(slot,input.RoundId))UseTool(slot,input,tool,beforeCombat);}
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
  void UseTool(int slot,PlayerInput input,ToolKind tool,SessionState state) {
   switch(tool) {
    case ToolKind.Glove:Strike(slot,input,tool,state,Tools.GloveReach,35,true);break;
    case ToolKind.Cushion:Strike(slot,input,tool,state,Tools.CushionReach,65,false);break;
    case ToolKind.Blower:Strike(slot,input,tool,state,Tools.BlowerReach,24,false);break;
    default:FireBubble(slot,input,tool);break;
   }
  }
  internal static readonly Color DiscChips=new Color(.95f,.25f,.2f,.9f);
  /// <summary>
  /// Close-range tools, judged at once on the host: everyone in front of the user inside the reach and the
  /// cone, with nothing solid in between, is pushed away from the user (the glove takes only the nearest).
  /// Light props in the same cone are shoved too.
  /// </summary>
  void Strike(int slot,PlayerInput input,ToolKind tool,SessionState state,float reach,float halfAngle,bool nearestOnly) {
   var rules=Session.ObserveSettings().Current;var body=bodies[slot].transform.position;var origin=body+Vector3.up*1.1f;
   var facing=Quaternion.Euler(0,input.Yaw,0)*Vector3.forward;
   var hits=new List<(int slot,float distance,Vector3 away)>();
   for(int i=0;i<bodies.Count;i++) {
    if(i==slot)continue;var other=bodies[i].transform.position;var offset=other-body;
    if(Mathf.Abs(offset.y)>1.6f)continue;offset.y=0;float distance=offset.magnitude;
    if(distance>reach+.38f)continue;
    if(distance>.05f&&Vector3.Angle(facing,offset)>halfAngle)continue;
    if(state.Players[i].Faction==Faction.Human&&!rules.FriendlyPush)continue;
    if(Physics.Linecast(origin,other+Vector3.up*1.1f,out var block,Layers.World,QueryTriggerInteraction.Ignore)&&block.collider.GetComponentInParent<CharacterController>()==null)continue;
    hits.Add((i,distance,distance>.05f?Vector3.Lerp(offset.normalized,facing,.35f).normalized:facing));
   }
   foreach(var hit in nearestOnly?hits.OrderBy(h=>h.distance).Take(1):hits) {
    var push=(hit.away*rules.PushForce*Tools.PushScale(tool)+Vector3.up*Tools.Lift(tool))*AnimalBalance.For(rules,state.Players[hit.slot]).Knockback;
    // A gust weakens with distance; a punch or a cushion does not.
    if(tool==ToolKind.Blower)push*=Mathf.Lerp(1,.35f,hit.distance/reach);
    pushVelocity[hit.slot]+=push;bubbleHits[hit.slot]++;Session.RecordToolHit(hit.slot,slot,tool);
   }
   if(props!=null){var view=props.Observe();for(int i=0;i<view.Length;i++){var offset=view[i].Position-body;if(Mathf.Abs(offset.y)>1.6f)continue;offset.y=0;
    if(offset.magnitude<=reach+view[i].Radius&&(offset.magnitude<.05f||Vector3.Angle(facing,offset)<=halfAngle))props.Push(i,(offset.normalized+facing).normalized*rules.PushForce*Tools.PushScale(tool)*PushProps.BubbleShare,slot);}}
  }
  void FireBubble(int slot,PlayerInput input,ToolKind kind=ToolKind.Cork) {
   var rules=Session.ObserveSettings().Current;
   var direction=Quaternion.Euler(input.Pitch,input.Yaw,0)*Vector3.forward;
   var position=bodies[slot].transform.position+Vector3.up*1.3f+Quaternion.Euler(0,input.Yaw,0)*Vector3.right*.1f;
   // Cork popper: a small cork leaves the tube with a puff. The judged radius, speed and push stay the shot settings.
   var visual=kind==ToolKind.Disc?Effects.Disc(transform):Effects.Cork(transform);visual.name="Bubble";visual.transform.position=position;
   Effects.Emit(position+direction*.6f,kind==ToolKind.Disc?DiscChips:MuzzlePuff,5,.9f,false);
   Audio.Play(kind==ToolKind.Disc?"disc":"fire",position);
   bubbles.Add(new Bubble{Id=++nextBubbleId,Owner=slot,Round=input.RoundId,Kind=kind,Position=position,Direction=direction,Life=rules.BubbleLifetime,Radius=rules.BubbleRadius,Speed=kind==ToolKind.Disc?rules.BubbleSpeed*.8f:rules.BubbleSpeed,Range=rules.BubbleRange,PushForce=rules.PushForce*Tools.PushScale(kind),FriendlyPush=rules.FriendlyPush,Visual=visual});
  }
  void Pop(Bubble bubble){Destroy(bubble.Visual);var marker=new GameObject("Bubble impact");marker.transform.SetParent(transform,false);marker.transform.position=bubble.Position;bursts.Add((marker,.15f,bubble.Id));CorkImpact(bubble.Position,bubble.Direction,bubble.Kind);}
  internal static readonly Color MuzzlePuff=new Color(1,.96f,.86f,.75f),ImpactChips=new Color(.86f,.7f,.48f,.95f);
  /// <summary>Where a shot lands: a few cork-coloured chips, the knock and the cork pinging back. Host and remote alike.</summary>
  void CorkImpact(Vector3 at,Vector3 direction,ToolKind kind=ToolKind.Cork){if(kind==ToolKind.Disc){Effects.Emit(at,DiscChips,8,1.6f);Audio.Play("disc-bounce",at,1,.8f);return;}Effects.Emit(at,ImpactChips,8,1.6f);Effects.Ricochet(at,direction);Audio.Play("pop",at);}
  void Melee(int slot,int round) {
   var input=inputs[slot];float yaw=input.RoundId==round?input.Yaw:bodies[slot].transform.eulerAngles.y;
   var direction=Quaternion.Euler(0,yaw,0)*Vector3.forward;
   var origin=bodies[slot].transform.position+Vector3.up*.9f;
   foreach(var collider in Physics.OverlapSphere(origin,CombatRules.MeleeDistance-.1f,Layers.World,QueryTriggerInteraction.Ignore).OrderBy(c=>(c.bounds.center-origin).sqrMagnitude)) {
    int victim=bodies.IndexOf(collider as CharacterController);if(victim<0||victim==slot)continue;
    var offset=bodies[victim].transform.position-bodies[slot].transform.position;
    if(offset.sqrMagnitude>.04f && Vector3.Dot(direction,offset.normalized)<.2f)continue;
    if(Physics.Linecast(origin,bodies[victim].transform.position+Vector3.up*.9f,out var obstruction,Layers.World,QueryTriggerInteraction.Ignore) && obstruction.collider!=bodies[victim])continue;
    if(Session.TryMeleeHit(slot,victim,round))return;
   }
   Session.ResolveAttackMiss(slot,round);
  }
 }
}
