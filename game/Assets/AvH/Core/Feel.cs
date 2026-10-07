using System;
namespace AvH {
 /// <summary>What a character is doing beyond locomotion, derived by the host from its rule timers.</summary>
 public enum ActionPhase { None, Windup, Swing, Stunned, HitStop }
 public enum FeelEventKind { AttackWindup, AttackHit, AttackMiss, Transform, Stagger, Landing, PropPushed }
 /// <summary>
 /// One public fact the host judged (tell, hit, miss, transformation, bubble stagger, hard landing, shoved prop).
 /// A shoved prop's <see cref="Target"/> is the prop index, not a slot.
 /// Ids grow within a session so every viewer can present each event exactly once.
 /// </summary>
 [Serializable] public sealed class FeelEvent {
  public int Id, Round, Actor=-1, Target=-1;
  public FeelEventKind Kind;
  public WorldPosition Position;
  public double HostTime;
  /// <summary>0..1 intensity hint (landing impact); 1 for discrete combat events.</summary>
  public float Strength=1;
  public FeelEvent Copy()=>(FeelEvent)MemberwiseClone();
 }
}
