using UnityEngine;
namespace AvH {
 // Metres. Shared by geometry, navigation and route acceptance checks.
 public static class TownLayout {
  public const float HalfExtent=44;
  public static readonly Vector3 Roof=new Vector3(-20,4,21);
  public static readonly Vector3 Courtyard=new Vector3(25,0,22);
  public static readonly Vector3 Workshop=new Vector3(22,0,-23);
  public static readonly Vector3 RoofApproach=new Vector3(-20,0,8);
  /// <summary>Pushable props are never placed this close (XZ) to a shelter: not inside, not at its entrance.</summary>
  public const float PropClearance=8;
  /// <summary>Clearance either side of the roof staircase, from its foot to the rooftop.</summary>
  public const float StairClearance=4;
  /// <summary>Inside a shelter, at its entrance or on the roof staircase: only fixed props may stand here.</summary>
  public static bool NearShelter(Vector3 p) {
   foreach(var shelter in new[]{Roof,Courtyard,Workshop})if(Flat(p-shelter).magnitude<PropClearance)return true;
   var a=Flat(RoofApproach);var ab=Flat(Roof)-a;float t=Mathf.Clamp01(Vector2.Dot(Flat(p)-a,ab)/ab.sqrMagnitude);
   return (Flat(p)-(a+ab*t)).magnitude<StairClearance;
  }
  static Vector2 Flat(Vector3 v)=>new Vector2(v.x,v.z);
 }
}
