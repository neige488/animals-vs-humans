using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEngine;
namespace AvH {
 /// <summary>Development-only, opt-in two-process probe. Buffers samples in memory so logging cannot create the hitch being measured.</summary>
 [DefaultExecutionOrder(100)] public sealed class NetworkFrameProbe : MonoBehaviour {
  struct Sample {public double Time,FrameMs,PumpMs,SnapshotAge;public float X,Y,Z,VisualX,VisualY,VisualZ;public int Arrivals,GC0,Paused,Reconciles,Replays;}
  readonly List<Sample> samples=new List<Sample>(4096);
  NetworkPresentation network;UnityPlaytestSession world;GamePresentation presentation;
  string folder,role;double started,last;int frame;bool burst,settle;
  public static bool TryStart(GamePresentation presentation,NetworkPresentation network,UnityPlaytestSession world,string[] args){
   int at=Array.IndexOf(args,"-avhNetworkProbe");if(!Debug.isDebugBuild||at<0||at+2>=args.Length)return false;
   var probe=presentation.gameObject.AddComponent<NetworkFrameProbe>();probe.presentation=presentation;probe.network=network;probe.world=world;probe.role=args[at+1];probe.folder=args[at+2];
   probe.burst=Array.IndexOf(args,"-avhProbeBurst")>=0;probe.settle=Array.IndexOf(args,"-avhProbeSettle")>=0;
   if(Array.IndexOf(args,"-avhProbe120fps")>=0){QualitySettings.vSyncCount=0;Application.targetFrameRate=120;}
   Directory.CreateDirectory(probe.folder);presentation.AutomatedProbeInput=true;probe.started=probe.last=Time.realtimeSinceStartupAsDouble;
   presentation.UseDisplaySettings(new LocalDisplaySettings(Path.Combine(probe.folder,probe.role+"-display.xml")));
   int quality=Array.IndexOf(args,"-avhProbeQuality");if(quality>=0&&quality+1<args.Length&&Enum.TryParse(args[quality+1],out GraphicsQuality level))presentation.Display.SetQuality(level);
   if(probe.role=="host"){network.StartHost("probe-host","127.0.0.1");File.WriteAllText(Path.Combine(probe.folder,"room.txt"),network.RoomCode);}
   else {network.JoinRoom(File.ReadAllText(Path.Combine(probe.folder,"room.txt")),"probe-client");var sim=world.GetComponent<FishRoomBridge>().Manager.TransportManager.LatencySimulator;int latency=Array.IndexOf(args,"-avhProbeLatency");int loss=Array.IndexOf(args,"-avhProbeLoss");if(latency>=0&&latency+1<args.Length)sim.SetLatency(long.Parse(args[latency+1],CultureInfo.InvariantCulture));if(loss>=0&&loss+1<args.Length)sim.SetPacketLoss(double.Parse(args[loss+1],CultureInfo.InvariantCulture));sim.SetEnabled(latency>=0||loss>=0); }return true;
  }
  void LateUpdate(){
   double now=Time.realtimeSinceStartupAsDouble;if(role=="client"&&burst)network.PauseReceiveForProbe=(now-started)%.5>.025;var position=Vector3.zero;
   if(world.Session!=null&&network.CanPlay&&network.LocalSlot>=0){
    double age=now-started;float yaw=(float)(age*35);network.SubmitInput(new PlayerInput{Forward=settle&&age>=12?0:.6f,Yaw=yaw,Jump=(!settle||age<12)&&frame%180==0});position=world.PlayerTransform(network.LocalSlot).position;
   }
   var displayed=world.Session!=null&&network.LocalSlot>=0?world.CameraFollowTransform(network.LocalSlot).position:Vector3.zero;
   samples.Add(new Sample{VisualX=displayed.x,VisualY=displayed.y,VisualZ=displayed.z,Time=now-started,FrameMs=(now-last)*1000,PumpMs=network.LastPumpMilliseconds,SnapshotAge=network.SnapshotAge*1000,Arrivals=network.SnapshotArrivals,GC0=GC.CollectionCount(0),Paused=world.FishControls(network.LocalSlot)?0:1,Reconciles=world.GetComponent<FishRoomBridge>().LocalMotor?.ReconcileCount??0,Replays=world.GetComponent<FishRoomBridge>().LocalMotor?.ReplayCount??0,X=position.x,Y=position.y,Z=position.z});last=now;frame++;
   if(now-started<30)return;
   using(var writer=new StreamWriter(Path.Combine(folder,role+"-frames.csv"))){writer.WriteLine("seconds,frame_ms,pump_ms,snapshot_age_ms,arrivals,gc0,paused,reconciles,replays,x,y,z,visual_x,visual_y,visual_z");foreach(var s in samples)writer.WriteLine(string.Join(",",s.Time.ToString("F6",CultureInfo.InvariantCulture),s.FrameMs.ToString("F3",CultureInfo.InvariantCulture),s.PumpMs.ToString("F3",CultureInfo.InvariantCulture),s.SnapshotAge.ToString("F3",CultureInfo.InvariantCulture),s.Arrivals,s.GC0,s.Paused,s.Reconciles,s.Replays,s.X.ToString("F4",CultureInfo.InvariantCulture),s.Y.ToString("F4",CultureInfo.InvariantCulture),s.Z.ToString("F4",CultureInfo.InvariantCulture),s.VisualX.ToString("F4",CultureInfo.InvariantCulture),s.VisualY.ToString("F4",CultureInfo.InvariantCulture),s.VisualZ.ToString("F4",CultureInfo.InvariantCulture)));}
   Application.Quit();enabled=false;
  }
 }
}
