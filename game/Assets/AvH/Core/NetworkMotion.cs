using System;
namespace AvH {
 /// <summary>
 /// One host presentation frame: where every body is and how it is moving at one host time.
 /// Remote viewers buffer these frames and play them back through the same animator path as the host.
 /// Later presentation payloads that need the same timeline (for example pushed props) belong beside <see cref="Bodies"/>.
 /// </summary>
 [Serializable] public sealed class NetworkMotionFrame {
  public double HostTime;
  public NetworkBodyMotion[] Bodies=Array.Empty<NetworkBodyMotion>();
 }
 /// <summary>
 /// One body's motion quantized for the wire (21 bytes): centimetres, centimetres per second, hundredths of a degree
 /// and a 0..255 action progress. 12 bodies × 20 Hz ≈ 5 KB/s per peer.
 /// </summary>
 [Serializable] public struct NetworkBodyMotion {
  public short X,Y,Z,VelocityX,VelocityZ,VerticalSpeed,Yaw,Pitch,TopSpeed;
  public byte Flags,Action,Progress;
  const byte GroundedFlag=1;
  public static NetworkBodyMotion Encode(WorldPosition position,float yaw,float pitch,LocomotionState motion)=>new NetworkBodyMotion {
   X=Centi(position.X),Y=Centi(position.Y),Z=Centi(position.Z),
   VelocityX=Centi(motion.VelocityX),VelocityZ=Centi(motion.VelocityZ),VerticalSpeed=Centi(motion.VerticalSpeed),TopSpeed=Centi(motion.TopSpeed),
   Yaw=Centi(NormalizeAngle(yaw)),Pitch=Centi(NormalizeAngle(pitch)),
   Flags=motion.Grounded?GroundedFlag:(byte)0,Action=(byte)motion.Action,
   Progress=(byte)Math.Round(Clamp01(Finite(motion.ActionProgress))*255)
  };
  public WorldPosition Position()=>new WorldPosition(X/100f,Y/100f,Z/100f);
  public float AimYaw()=>Yaw/100f;
  public float AimPitch()=>Pitch/100f;
  public LocomotionState Locomotion()=>new LocomotionState {
   VelocityX=VelocityX/100f,VelocityZ=VelocityZ/100f,VerticalSpeed=VerticalSpeed/100f,TopSpeed=TopSpeed/100f,AimYaw=AimYaw(),
   Grounded=(Flags&GroundedFlag)!=0,Action=Enum.IsDefined(typeof(ActionPhase),(int)Action)?(ActionPhase)Action:ActionPhase.None,ActionProgress=Progress/255f
  };
  static short Centi(float value)=>(short)Math.Max(short.MinValue,Math.Min(short.MaxValue,Math.Round(Finite(value)*100)));
  static float Finite(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:value;
  static float Clamp01(float value)=>Math.Max(0,Math.Min(1,value));
  internal static float NormalizeAngle(float degrees){degrees=Finite(degrees)%360;if(degrees>=180)degrees-=360;if(degrees<-180)degrees+=360;return degrees;}
 }
}
