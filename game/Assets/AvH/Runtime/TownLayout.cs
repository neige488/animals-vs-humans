using UnityEngine;
namespace AvH {
 // Metres. Shared by geometry, navigation and route acceptance checks.
 public static class TownLayout {
  public const float HalfExtent=44;
  public static readonly Vector3 Roof=new Vector3(-20,4,21);
  public static readonly Vector3 Courtyard=new Vector3(25,0,22);
  public static readonly Vector3 Workshop=new Vector3(22,0,-23);
  public static readonly Vector3 RoofApproach=new Vector3(-20,0,8);
 }
}
