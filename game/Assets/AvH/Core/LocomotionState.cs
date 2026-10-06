using System;
namespace AvH {
 /// <summary>
 /// Public movement facts a character animator needs. The host fills it from its own simulation;
 /// remote views can fill it from snapshots so both use the same animation path.
 /// </summary>
 [Serializable] public struct LocomotionState {
  public float VelocityX, VelocityZ, VerticalSpeed;
  /// <summary>Camera/aim heading in degrees; humans keep their upper body on it.</summary>
  public float AimYaw;
  public bool Grounded;
 }
}
