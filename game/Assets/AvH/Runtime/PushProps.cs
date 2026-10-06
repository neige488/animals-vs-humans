using System.Collections.Generic;
using UnityEngine;
namespace AvH {
 /// <summary>One light market prop as every viewer sees it.</summary>
 public struct PropView {
  public int Index;
  /// <summary>crate, barrel or pot.</summary>
  public string Kind;
  /// <summary>Footprint centre at floor height, and facing.</summary>
  public Vector3 Position,Home;
  public float Yaw,HomeYaw,Radius,Height;
  public bool Moving;
 }
 /// <summary>
 /// Light crates, barrels and pots that bodies and bubbles shove. The host judges them; remote viewers only show
 /// the poses it sends.
 /// </summary>
 public sealed class PushProps {
  /// <summary>Village child holding the pushable props, in a fixed order every build shares.</summary>
  public const string Container="Pushable props";
  readonly List<PropView> props=new List<PropView>();
  public int Count=>props.Count;
  public static PushProps Adopt(Transform village)=>new PushProps();
  public PropView[] Observe()=>props.ToArray();
 }
}
