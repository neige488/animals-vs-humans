using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
namespace AvH {
 public struct RoomHello : IBroadcast {public string Protocol,Key,Identity,Name;}
 public struct RoomReady : IBroadcast {}
 public struct RoomStatePacket : IBroadcast {public int Slot;public byte[] Data;}
 public struct RoomError : IBroadcast {public string Message;}
 /// <summary>FishNet UDP room transport. Core match, slot takeover and reconnect rules remain authoritative.</summary>
 public sealed class FishRoomBridge : MonoBehaviour,IDisposable {
  public const string Protocol="avh-fishnet-8";
  public UnityPlaytestSession World {get;private set;}
  public NetworkManager Manager {get;private set;}
  public ConnectionStatus Status {get;private set;}
  public SessionState Snapshot {get;private set;}
  public NetworkVisualState Visuals {get;private set;}
  public bool VisualsDelayed=>false;
  public string Error {get;private set;}
  public string RoomCode {get;private set;}
  string endpoint="",connectionStage="idle";
  public string ConnectionDiagnostics=>$"AVH-FISHNET 3 connection: endpoint={endpoint} stage={connectionStage} status={Status} slot={Slot} snapshot-bytes={PacketBytes} error={Error}";
  public int Slot {get;private set;}=-1;
  public int PacketBytes {get;private set;}public int DecodedBytes {get;private set;}
  public FishPredictedMotor LocalMotor=>motors.TryGetValue(Slot,out var value)?value:null;
  public Func<NetworkVisualState> CaptureVisuals;
  readonly string identity=Guid.NewGuid().ToString("N");
  string roomKey,name;bool hosting,ready,disposed;
  double started;
  class Peer {public string Identity,Name;public int Slot=-1;public bool Ready;public FishPredictedMotor Motor;}
  readonly Dictionary<NetworkConnection,Peer> peers=new Dictionary<NetworkConnection,Peer>();
  readonly Dictionary<int,FishPredictedMotor> motors=new Dictionary<int,FishPredictedMotor>();
  readonly Queue<NetworkMotionFrame> frames=new Queue<NetworkMotionFrame>();
  readonly PlayerInput[] pending=new PlayerInput[12];
  NetworkObject motorPrefab;
  public void Initialize(UnityPlaytestSession world){World=world;world.FishBridge=this;}
  void CreateManager(){
   if(Manager!=null)return;
   var prefab=Resources.Load<GameObject>("FishNetRoomManager");
   if(prefab==null)throw new InvalidOperationException("FishNet room prefab is missing. Run ConfigureFishNet.Configure.");
   var root=Instantiate(prefab);root.name="FishNet room";Manager=root.GetComponent<NetworkManager>();
   motorPrefab=Resources.Load<GameObject>("FishNetPlayer").GetComponent<NetworkObject>();
   Manager.TimeManager.SetTickRate(60);
   // Wi-Fi can deliver half a second of 60 Hz inputs together. FishNet's default
   // 15-entry queue discarded older movement before it could be simulated.
   // Keep a bounded two-second queue; retain FishNet's excessive-input protection.
   Manager.GetComponent<FishNet.Managing.Predicting.PredictionManager>().SetMaximumServerReplicates(120);
   Manager.TimeManager.OnPreTick+=BeforeTick;
   Manager.ServerManager.RegisterBroadcast<RoomHello>(Hello);
   Manager.ServerManager.RegisterBroadcast<RoomReady>(ReadyPeer);
   Manager.ServerManager.OnRemoteConnectionState+=Disconnected;
   Manager.ClientManager.RegisterBroadcast<RoomStatePacket>(ReceiveState);
   Manager.ClientManager.RegisterBroadcast<RoomError>((packet,channel)=>Fail(packet.Message));
   Manager.ClientManager.OnAuthenticated+=Authenticated;
   Manager.ClientManager.OnClientConnectionState+=ClientConnection;
  }
  public void StartRoom(string address){
   CreateManager();hosting=true;disposed=false;roomKey=Guid.NewGuid().ToString("N");
   int port;using(var socket=new UdpClient(0))port=((IPEndPoint)socket.Client.LocalEndPoint).Port;
   RoomCode=PrivateRoomCode.Create(address,port,roomKey);World.AutomaticStep=false;
   if(!Manager.ServerManager.StartConnection((ushort)port))throw new IOException("UDP host failed to start");
  }
  void BeforeTick(){if(hosting&&Manager.ServerManager.Started&&World.Session!=null){World.Step((float)Manager.TimeManager.TickDelta);if(!motors.ContainsKey(0))SpawnMotor(0,null);}}
  void SpawnMotor(int slot,NetworkConnection owner){
   var obj=Instantiate(motorPrefab);var motor=obj.GetComponent<FishPredictedMotor>();motor.SetSlot(slot);motor.Attach(this);
   motors[slot]=motor;Manager.ServerManager.Spawn(obj,owner);
  }
  void Hello(NetworkConnection conn,RoomHello data,Channel channel){
   if(!hosting)return;
   if(data.Protocol!=Protocol||data.Key!=roomKey||data.Identity==null||data.Identity.Length!=32||string.IsNullOrWhiteSpace(data.Name)||data.Name.Length>20||peers.Any(p=>p.Key!=conn&&p.Value.Identity==data.Identity)){
    Manager.ServerManager.Broadcast(conn,new RoomError{Message="코드 또는 빌드 버전이 맞지 않습니다"});conn.Disconnect(false);return;
   }
   if(!peers.ContainsKey(conn))peers.Add(conn,new Peer{Identity=data.Identity,Name=data.Name});
   Publish(conn,peers[conn],CapturePacket(true));
  }
  void ReadyPeer(NetworkConnection conn,RoomReady data,Channel channel){if(peers.TryGetValue(conn,out var peer))peer.Ready=true;}
  void Disconnected(NetworkConnection conn,RemoteConnectionStateArgs args){
   if(args.ConnectionState!=RemoteConnectionState.Stopped||!peers.TryGetValue(conn,out var peer))return;
   if(peer.Slot>=0){World.Session?.ReleaseSlotToBot(peer.Slot,peer.Identity);if(peer.Motor!=null)Manager.ServerManager.Despawn(peer.Motor.NetworkObject);motors.Remove(peer.Slot);World.ClearFishMotor(peer.Slot);}
   peers.Remove(conn);
  }
  public void Pump(bool publishStates=true){
   if(!hosting){if(Status==ConnectionStatus.Connecting&&Time.realtimeSinceStartupAsDouble-started>10)Fail("방에 연결하지 못했습니다. UDP 방화벽과 방 코드를 확인하세요.");return;}
   var shared=publishStates?CapturePacket(false):null;
   foreach(var pair in peers.ToArray()){
    var peer=pair.Value;
    if(peer.Ready&&peer.Slot<0){var outcome=World.Session.ClaimSlot(peer.Identity,peer.Name,out int assigned);
     if(outcome==SlotClaimOutcome.Claimed){peer.Slot=assigned;SpawnMotor(assigned,pair.Key);peer.Motor=motors[assigned];}
     else if(outcome!=SlotClaimOutcome.WaitingForRound){Manager.ServerManager.Broadcast(pair.Key,new RoomError{Message="방이 가득 찼습니다"});pair.Key.Disconnect(false);continue;}
    }
    if(publishStates)Publish(pair.Key,peer,shared);
   }
  }
  byte[] CapturePacket(bool fullMotion){
   var state=World.Observe();var visual=CaptureVisuals?.Invoke()??new NetworkVisualState();var settings=World.ObserveActiveSettings();visual.CurrentRules=settings.Current;visual.PendingRules=settings.Pending??settings.Current;visual.HasPending=settings.Pending!=null;visual.SettingsVersion=settings.Version;
   if(fullMotion)visual.Motion=World.CaptureMotion(true);
   var raw=RoomWire.Encode(state,visual);DecodedBytes=raw.Length;
   using(var output=new MemoryStream()){using(var zip=new DeflateStream(output,System.IO.Compression.CompressionLevel.Fastest,true))zip.Write(raw,0,raw.Length);var packet=output.ToArray();PacketBytes=packet.Length;return packet;}
  }
  void Publish(NetworkConnection conn,Peer peer,byte[] data){
   Manager.ServerManager.Broadcast(conn,new RoomStatePacket{Slot=peer.Slot,Data=data});
  }
  public void Connect(string code,string nickname){
   Cancel();if(!PrivateRoomCode.TryParse(code,out string address,out int port,out roomKey)||string.IsNullOrWhiteSpace(nickname)||nickname.Length>20){Fail("닉네임 또는 방 코드 형식을 확인하세요");return;}
   CreateManager();disposed=false;name=nickname;endpoint=address+":"+port;connectionStage="udp-starting";Status=ConnectionStatus.Connecting;started=Time.realtimeSinceStartupAsDouble;
   if(!Manager.ClientManager.StartConnection(address,(ushort)port))Fail("UDP 접속을 시작하지 못했습니다. 게임을 다시 실행해 주세요.");
  }
  void Authenticated(){if(!hosting){connectionStage="room-admission";Manager.ClientManager.Broadcast(new RoomHello{Protocol=Protocol,Key=roomKey,Identity=identity,Name=name});}}
  void ClientConnection(ClientConnectionStateArgs args){
   if(hosting)return;
   if(args.ConnectionState==LocalConnectionState.Started){connectionStage="fishnet-authentication";return;}
   if(args.ConnectionState!=LocalConnectionState.Stopped||Status==ConnectionStatus.Idle||Status==ConnectionStatus.Failed)return;
   if(Status==ConnectionStatus.Playing){Status=ConnectionStatus.Interrupted;Error="호스트와의 연결이 종료되었습니다";}
   else {Status=ConnectionStatus.Failed;Error=connectionStage=="udp-starting"?"방에 연결하지 못했습니다. 게스트의 로컬 네트워크 권한과 호스트의 UDP 통신을 확인하세요.":"방 참가 승인 전에 연결이 종료되었습니다. 접속 진단을 복사해 주세요.";}
   Debug.Log(ConnectionDiagnostics);
  }
  void ReceiveState(RoomStatePacket packet,Channel channel){
   try{if(packet.Data==null||packet.Data.Length>1024*1024)throw new IOException("snapshot size");
    PacketBytes=packet.Data.Length;
    using(var compressed=new MemoryStream(packet.Data))using(var zip=new DeflateStream(compressed,CompressionMode.Decompress))using(var decoded=new MemoryStream()){
     var buffer=new byte[4096];int read;while((read=zip.Read(buffer,0,buffer.Length))>0){if(decoded.Length+read>1024*1024)throw new IOException("decoded snapshot size");decoded.Write(buffer,0,read);}DecodedBytes=(int)decoded.Length;decoded.Position=0;
     using(var reader=new BinaryReader(decoded)){Snapshot=RoomWire.Read<SessionState>(reader);Visuals=RoomWire.Read<NetworkVisualState>(reader);}
    }
    connectionStage="room-state-received";Slot=packet.Slot;Status=ready?(Slot>=0?ConnectionStatus.Playing:ConnectionStatus.Waiting):ConnectionStatus.Loading;
    if(Visuals.Motion!=null){frames.Enqueue(Visuals.Motion);while(frames.Count>32)frames.Dequeue();}
   }catch(Exception e)when(e is IOException||e is ArgumentException){Fail("호스트 상태를 읽지 못했습니다");}
  }
  public void Ready(){connectionStage="slot-assignment";ready=true;Manager.ClientManager.Broadcast(new RoomReady());}
  public NetworkMotionFrame[] TakeMotionFrames(){var result=frames.ToArray();frames.Clear();return result;}
  public void SubmitInput(NetworkInput input){
   var p=new PlayerInput{Right=input.Right,Forward=input.Forward,Yaw=input.Yaw,Pitch=input.Pitch,Jump=input.Jump,Attack=input.Attack,Reload=input.Reload,Ability=input.Ability,RoundId=input.RoundId};
   QueueInput(Slot,p);
  }
  public void QueueInput(int slot,PlayerInput input){if(slot<0||slot>=12)return;input.Jump|=pending[slot].Jump;input.Attack|=pending[slot].Attack;input.Reload|=pending[slot].Reload;input.Ability|=pending[slot].Ability;pending[slot]=input;}
  internal PlayerInput TakeInput(int slot){var value=pending[slot];pending[slot].Jump=false;pending[slot].Attack=false;pending[slot].Reload=false;pending[slot].Ability=false;return value;}
  internal void RegisterMotor(int slot,FishPredictedMotor motor){motors[slot]=motor;}
  public bool Controls(int slot)=>motors.TryGetValue(slot,out var motor)&&motor!=null&&motor.Attached&&(hosting||motor.IsOwner);
  void Fail(string text){Error=text;Status=ConnectionStatus.Failed;Debug.Log(ConnectionDiagnostics);Manager?.ClientManager.StopConnection();}
  public void Cancel(){Status=ConnectionStatus.Idle;Manager?.ClientManager.StopConnection();ready=false;Snapshot=null;Visuals=null;Slot=-1;Error=null;frames.Clear();Array.Clear(pending,0,pending.Length);}
  public void Dispose(){if(disposed)return;disposed=true;Cancel();hosting=false;Manager?.ServerManager.StopConnection(true);peers.Clear();motors.Clear();if(Manager!=null){Manager.TimeManager.OnPreTick-=BeforeTick;Destroy(Manager.gameObject);Manager=null;}}
 }
}
