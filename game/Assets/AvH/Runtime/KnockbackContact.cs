using UnityEngine;
namespace AvH {
 // Contact removes only momentum into an actual surface; tangential sliding and free-air knockback remain.
 public sealed class KnockbackContact : MonoBehaviour {
  internal UnityPlaytestSession World;
  internal int Slot;
  void OnControllerColliderHit(ControllerColliderHit hit) {World.ResolveKnockbackContact(Slot,hit.normal);}
 }
}
