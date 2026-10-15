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
  /// <summary>Current top ground speed for this character (m/s). 0 = unknown; gait thresholds fall back to fixed values.</summary>
  public float TopSpeed;
  /// <summary>Host action phase (tell, swing, stun, hit-stop) and its 0..1 progress.</summary>
  public ActionPhase Action;
  public float ActionProgress;
 }
}
