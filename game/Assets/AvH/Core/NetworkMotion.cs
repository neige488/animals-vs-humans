using System;
namespace AvH {
 /// <summary>
 /// One host presentation frame: where every body is and how it is moving at one host time.
 /// Remote viewers buffer these frames and play them back through the same animator path as the host.
 /// Later presentation payloads that need the same timeline (for example pushed props) belong beside <see cref="Bodies"/>.
 /// </summary>
 [Serializable] public sealed class NetworkMotionFrame {
  public double HostTime;
  /// <summary>Host round the frame belongs to; a viewer drops frames from any other round than its snapshot's.</summary>
  public int Round;
  public NetworkBodyMotion[] Bodies=Array.Empty<NetworkBodyMotion>();
  /// <summary>Light props that are moving (or just settled) at this host time, plus a periodic refresh of every displaced prop.</summary>
  public NetworkPropMotion[] Props=Array.Empty<NetworkPropMotion>();
 }
 /// <summary>One pushed prop's pose for the wire (10 bytes): its index in the shared village order, centimetres and hundredths of a degree.</summary>
 [Serializable] public struct NetworkPropMotion {
  public short Index,X,Y,Z,Yaw;
  public static NetworkPropMotion Encode(int index,WorldPosition position,float yaw)=>default;
  public WorldPosition Position()=>default;
  public float AimYaw()=>0;
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
  /// <summary>Farther than this between two frames is a warp (respawn, map-edge recovery): shown at once, never slid.</summary>
  public const float TeleportDistance=6;
  /// <summary>The body between two host frames. A phase change shows on the later frame, never half-way.</summary>
  public static RemoteBodyPose Blend(NetworkBodyMotion from,NetworkBodyMotion to,float t) {
   t=Clamp01(Finite(t));var a=from.Locomotion();var b=to.Locomotion();var pa=from.Position();var pb=to.Position();
   float dx=pb.X-pa.X,dy=pb.Y-pa.Y,dz=pb.Z-pa.Z;bool warp=dx*dx+dy*dy+dz*dz>TeleportDistance*TeleportDistance;
   if(warp||t>=1)return new RemoteBodyPose{Position=pb,Yaw=to.AimYaw(),Pitch=to.AimPitch(),Motion=b};
   float yaw=NormalizeAngle(from.AimYaw()+NormalizeAngle(to.AimYaw()-from.AimYaw())*t);
   var motion=new LocomotionState {
    VelocityX=Lerp(a.VelocityX,b.VelocityX,t),VelocityZ=Lerp(a.VelocityZ,b.VelocityZ,t),VerticalSpeed=Lerp(a.VerticalSpeed,b.VerticalSpeed,t),TopSpeed=Lerp(a.TopSpeed,b.TopSpeed,t),
    AimYaw=yaw,Grounded=t<.5f?a.Grounded:b.Grounded,
    Action=a.Action,ActionProgress=a.Action==b.Action?Lerp(a.ActionProgress,b.ActionProgress,t):a.ActionProgress
   };
   return new RemoteBodyPose{Position=new WorldPosition(Lerp(pa.X,pb.X,t),Lerp(pa.Y,pb.Y,t),Lerp(pa.Z,pb.Z,t)),Yaw=yaw,Pitch=Lerp(from.AimPitch(),to.AimPitch(),t),Motion=motion};
  }
  static float Lerp(float a,float b,float t)=>a+(b-a)*t;
  static short Centi(float value)=>(short)Math.Max(short.MinValue,Math.Min(short.MaxValue,Math.Round(Finite(value)*100)));
  static float Finite(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:value;
  static float Clamp01(float value)=>Math.Max(0,Math.Min(1,value));
  internal static float NormalizeAngle(float degrees){degrees=Finite(degrees)%360;if(degrees>=180)degrees-=360;if(degrees<-180)degrees+=360;return degrees;}
 }
 /// <summary>A decoded body pose for presentation: position, facing/aim and the motion to animate.</summary>
 public struct RemoteBodyPose {public WorldPosition Position;public float Yaw,Pitch;public LocomotionState Motion;}
}
