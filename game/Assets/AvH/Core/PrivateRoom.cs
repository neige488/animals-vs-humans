using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
namespace AvH {
 public static class RoomProtocol {
  public static readonly string Version=BuildVersion();
  static string Schema(Type t){if(t.IsArray)return "[]"+Schema(t.GetElementType());if(Nullable.GetUnderlyingType(t)!=null)return "?"+Schema(Nullable.GetUnderlyingType(t));if(t.IsPrimitive||t==typeof(string)||t.IsEnum)return t.FullName;return t.FullName+"{"+string.Join(";",t.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal).Select(f=>f.Name+":"+Schema(f.FieldType)))+"}";}
  static string BuildVersion(){using(var hash=System.Security.Cryptography.SHA256.Create())return "avh-private-2-"+Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(Schema(typeof(SessionState))+Schema(typeof(NetworkInput))+Schema(typeof(NetworkVisualState)))));}
 }
 [Serializable] public sealed class NetworkBubble {public int Id,OwnerSlot,Round;public WorldPosition Position,Direction;public float RemainingLife,Travelled;}
 [Serializable] public sealed class NetworkBurst {public int Id;public WorldPosition Position;public float Remaining;}
 [Serializable] public sealed class NetworkVisualState {
  public NetworkBurst[] Bursts=Array.Empty<NetworkBurst>();public float[] Yaws=new float[12];public NetworkBubble[] Bubbles=Array.Empty<NetworkBubble>();
  public PlaytestValues CurrentRules=new PlaytestValues(),PendingRules=new PlaytestValues();public bool HasPending;public int SettingsVersion;
 }
 public enum ConnectionStatus { Idle, Connecting, Loading, Waiting, Playing, Failed, Interrupted }
 [Serializable] public struct NetworkInput { public float Right,Forward,Yaw,Pitch; public bool Jump,Attack,Reload; public int RoundId; }
 // Discovery is independent of the authoritative world. Codes contain a reachable IPv4 endpoint and random capability.
 public static class PrivateRoomCode {
  public static string Create(string address,int port,string key)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(address+":"+port+":"+key));
  public static bool TryParse(string code,out string address,out int port,out string key) {
   address=key=null;port=0;
   try {var parts=Encoding.UTF8.GetString(Convert.FromBase64String(code.Trim())).Split(':');IPAddress ip;
    if(parts.Length!=3||!IPAddress.TryParse(parts[0],out ip)||ip.AddressFamily!=AddressFamily.InterNetwork||!int.TryParse(parts[1],out port)||port<1||port>65535||parts[2].Length!=32)return false;
    address=parts[0];key=parts[2];return true;
   }catch{return false;}
  }
 }
 // Bounded, typed wire encoding: no remote type names, object activation or BinaryFormatter.
 static class RoomWire {
  public static byte[] Encode(params object[] values){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){foreach(var value in values)Write(w,value.GetType(),value);return m.ToArray();}}
  static void Write(BinaryWriter w,Type t,object v) {
   if(t==typeof(string)){w.Write((string)v??"");return;} if(t==typeof(int)){w.Write((int)v);return;}if(t==typeof(float)){w.Write((float)v);return;}if(t==typeof(double)){w.Write((double)v);return;}if(t==typeof(bool)){w.Write((bool)v);return;}
   if(t.IsEnum){w.Write(Convert.ToInt32(v));return;}var nullable=Nullable.GetUnderlyingType(t);if(nullable!=null){w.Write(v!=null);if(v!=null)Write(w,nullable,v);return;}
   if(t.IsArray){var array=(Array)v;w.Write(array==null?0:array.Length);if(array!=null)foreach(var item in array)Write(w,t.GetElementType(),item);return;}
   foreach(var field in t.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal))Write(w,field.FieldType,field.GetValue(v));
  }
  public static T Read<T>(BinaryReader r)=>(T)Read(r,typeof(T));
  static object Read(BinaryReader r,Type t) {
   if(t==typeof(string)){var s=r.ReadString();if(s.Length>2048)throw new IOException("문자열 상한");return s;}if(t==typeof(int))return r.ReadInt32();if(t==typeof(float))return r.ReadSingle();if(t==typeof(double))return r.ReadDouble();if(t==typeof(bool))return r.ReadBoolean();
   if(t.IsEnum)return Enum.ToObject(t,r.ReadInt32());var nullable=Nullable.GetUnderlyingType(t);if(nullable!=null)return r.ReadBoolean()?Read(r,nullable):null;
   if(t.IsArray){int n=r.ReadInt32();if(n<0||n>512)throw new IOException("배열 상한");var a=Array.CreateInstance(t.GetElementType(),n);for(int i=0;i<n;i++)a.SetValue(Read(r,t.GetElementType()),i);return a;}
   var result=Activator.CreateInstance(t);foreach(var f in t.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal))f.SetValue(result,Read(r,f.FieldType));return result;
  }
 }
 sealed class RoomPeer : IDisposable {
  public readonly TcpClient Socket; readonly List<byte> pending=new List<byte>(); public DateTime Seen=DateTime.UtcNow; public string Identity,Nickname; public int Slot=-1; public bool Ready;public int SnapshotSerial,EffectsOffset;public NetworkBubble[] Effects;
  public RoomPeer(TcpClient socket){Socket=socket;Socket.NoDelay=true;Socket.SendTimeout=100;}
  public void Send(params object[] values){var bytes=RoomWire.Encode(values);if(bytes.Length>65536)throw new IOException("패킷 상한");var stream=Socket.GetStream();var size=BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length));stream.Write(size,0,4);stream.Write(bytes,0,bytes.Length);}
  public IEnumerable<BinaryReader> Receive(){
   if(Socket.Client.Poll(0,SelectMode.SelectRead)&&Socket.Available==0)throw new IOException("연결 종료");
   int available=Socket.Available;if(available>0){var b=new byte[Math.Min(available,65540)];int n=Socket.GetStream().Read(b,0,b.Length);pending.AddRange(b.Take(n));Seen=DateTime.UtcNow;}
   int count=0;while(pending.Count>=4&&count++<32){int n=IPAddress.NetworkToHostOrder(BitConverter.ToInt32(pending.GetRange(0,4).ToArray(),0));if(n<1||n>65536)throw new IOException("잘못된 패킷");if(pending.Count<4+n)break;var frame=pending.GetRange(4,n).ToArray();pending.RemoveRange(0,4+n);yield return new BinaryReader(new MemoryStream(frame));}
   if(pending.Count>131080)throw new IOException("버퍼 상한");
  }
  public void Dispose(){Socket.Close();}
 }
 public sealed class PrivateRoomHost : IDisposable {
  readonly PlaytestSession world;readonly TcpListener listener;readonly List<RoomPeer> peers=new List<RoomPeer>();readonly string key=Guid.NewGuid().ToString("N");
  int rememberedRound;
  readonly Dictionary<string,Tuple<int,int>> previous=new Dictionary<string,Tuple<int,int>>(); bool disposed;
  public string RoomCode {get;private set;}
  public Action<int,NetworkInput> InputReceived;
  public Func<NetworkVisualState> CaptureVisuals;
  public PrivateRoomHost(PlaytestSession world,string advertisedAddress,int port=0){this.world=world;listener=new TcpListener(IPAddress.Any,port);listener.Start(16);RoomCode=PrivateRoomCode.Create(advertisedAddress,((IPEndPoint)listener.LocalEndpoint).Port,key);}
  public void Pump(){if(disposed)return;
   int round=world.Observe().Round;if(round!=rememberedRound){previous.Clear();rememberedRound=round;}
   for(int accepts=0;accepts<32&&listener.Pending();accepts++){var p=new RoomPeer(listener.AcceptTcpClient());if(peers.Count>=32){p.Dispose();continue;}peers.Add(p);}
   foreach(var p in peers.ToArray())try {
    foreach(var reader in p.Receive())using(reader){var command=reader.ReadString();
     if(command=="hello") {var protocol=reader.ReadString();var room=reader.ReadString();var identity=reader.ReadString();var name=reader.ReadString();
      if(protocol!=RoomProtocol.Version||room!=key||identity.Length!=32||string.IsNullOrWhiteSpace(name)||name.Length>20||peers.Any(x=>x!=p&&x.Identity==identity)){p.Send("error","코드 또는 참가자 확인 실패");Remove(p);break;}
      if(p.Identity==null){p.Identity=identity;p.Nickname=name;}p.Send("loading");
     }else if(command=="ready"&&p.Identity!=null){p.Ready=true;}
     else if(command=="input"&&p.Slot>=0){var input=RoomWire.Read<NetworkInput>(reader);if(input.RoundId==world.Observe().Round&&Finite(input.Right)&&Finite(input.Forward)&&Finite(input.Yaw)&&Finite(input.Pitch))InputReceived?.Invoke(p.Slot,input);}
     else if(command=="ping"){} else throw new IOException("잘못된 명령");
    }
    if(!peers.Contains(p))continue;
    if(p.Socket.Client.Poll(0,SelectMode.SelectRead)&&p.Socket.Available==0){Remove(p);continue;}
    if((DateTime.UtcNow-p.Seen).TotalSeconds>10){Remove(p);continue;}
    if(p.Ready&&p.Slot<0&&world.Observe().Phase!=RoundPhase.Results){var state=world.Observe();Tuple<int,int> old;PlayerState chosen=null;
     if(previous.TryGetValue(p.Identity,out old)&&old.Item2==state.Round)chosen=state.Players.FirstOrDefault(x=>x.Slot==old.Item1&&x.IsBot);
     chosen=chosen??state.Players.Where(x=>x.IsBot).OrderBy(x=>x.Faction).ThenBy(x=>x.Slot).FirstOrDefault();
     if(chosen==null){p.Send("error","방이 가득 찼습니다");Remove(p);continue;}
     p.Slot=chosen.Slot;world.SetSlotOwner(p.Slot,p.Nickname,false);InputReceived?.Invoke(p.Slot,new NetworkInput());
    }
    if(p.Identity!=null){
     if(p.Effects==null){
      var visual=CaptureVisuals?.Invoke()??new NetworkVisualState();var settings=world.ObserveSettings();visual.CurrentRules=settings.Current;visual.PendingRules=settings.Pending??settings.Current;visual.HasPending=settings.Pending!=null;visual.SettingsVersion=settings.Version;
      p.Effects=visual.Bubbles;visual.Bubbles=Array.Empty<NetworkBubble>();p.EffectsOffset=0;
      if(p.Effects.Length>16384){p.Send("error","버블 표현 상한을 초과했습니다. 호스트 설정을 낮춘 뒤 재시도하세요.");Remove(p);continue;}
      p.Send("state",++p.SnapshotSerial,p.Slot,world.Observe(),visual);
      if(p.Effects.Length==0){p.Send("bubbles",p.SnapshotSerial,0,0,Array.Empty<NetworkBubble>());p.Effects=null;continue;}
     }
     // At most eight 128-item chunks per pump: bounded frames and no unbounded send queue.
     for(int sent=0;sent<8&&p.EffectsOffset<p.Effects.Length;sent++){
      var chunk=p.Effects.Skip(p.EffectsOffset).Take(128).ToArray();p.Send("bubbles",p.SnapshotSerial,p.EffectsOffset,p.Effects.Length,chunk);p.EffectsOffset+=chunk.Length;
     }
     if(p.EffectsOffset==p.Effects.Length)p.Effects=null;
    }
   }catch(Exception e)when(e is IOException||e is SocketException||e is ObjectDisposedException||e is ArgumentException){Remove(p);}
  }
  static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
  void Remove(RoomPeer p){if(p.Slot>=0){world.SetSlotOwner(p.Slot,"봇 "+p.Slot,true);if(previous.Count<1024||previous.ContainsKey(p.Identity))previous[p.Identity]=Tuple.Create(p.Slot,world.Observe().Round);InputReceived?.Invoke(p.Slot,new NetworkInput());}peers.Remove(p);p.Dispose();}
  public void Dispose(){if(disposed)return;disposed=true;foreach(var p in peers.ToArray()){try{p.Send("closed");}catch{}Remove(p);}listener.Stop();}
 }
 public sealed class PrivateRoomClient : IDisposable {
  readonly string identity,protocolVersion;RoomPeer peer;TcpClient pending;Task connect;string roomKey,nickname;DateTime started,lastPing,snapshotStarted;bool ready;int snapshotSerial,receivedBubbles;NetworkBubble[] pendingBubbles;
  public ConnectionStatus Status {get;private set;} public string Error {get;private set;} public SessionState Snapshot {get;private set;} public NetworkVisualState Visuals {get;private set;} public int Slot {get;private set;}=-1;
  public PrivateRoomClient(string reconnectIdentity=null,string protocolVersion=null){identity=reconnectIdentity??Guid.NewGuid().ToString("N");this.protocolVersion=protocolVersion??RoomProtocol.Version;}
  public void Connect(string roomCode,string name){Cancel();string address,key;int port;if(!PrivateRoomCode.TryParse(roomCode,out address,out port,out key)||string.IsNullOrWhiteSpace(name)||name.Length>20){Fail("닉네임 또는 방 코드 형식을 확인하세요");return;}
   nickname=name;roomKey=key;Status=ConnectionStatus.Connecting;started=DateTime.UtcNow;pending=new TcpClient();connect=pending.ConnectAsync(address,port);
  }
  public void Pump(){try {
   if(Status==ConnectionStatus.Connecting){if(!connect.IsCompleted){if((DateTime.UtcNow-started).TotalSeconds>8)Fail("연결 시간이 초과되었습니다");return;}if(connect.IsFaulted){Fail("방에 연결하지 못했습니다");return;}peer=new RoomPeer(pending);pending=null;peer.Send("hello",protocolVersion,roomKey,identity,nickname);Status=ConnectionStatus.Loading;}
   if(peer==null)return;
   foreach(var r in peer.Receive())using(r){string kind=r.ReadString();if(kind=="state"){snapshotSerial=r.ReadInt32();snapshotStarted=DateTime.UtcNow;pendingBubbles=null;receivedBubbles=0;Slot=r.ReadInt32();Snapshot=RoomWire.Read<SessionState>(r);var previousBubbles=Visuals?.Bubbles??Array.Empty<NetworkBubble>();Visuals=RoomWire.Read<NetworkVisualState>(r);Visuals.Bubbles=previousBubbles;if(ready)Status=Slot>=0?ConnectionStatus.Playing:ConnectionStatus.Waiting;}else if(kind=="bubbles"){
     int serial=r.ReadInt32(),offset=r.ReadInt32(),total=r.ReadInt32();var chunk=RoomWire.Read<NetworkBubble[]>(r);
     if(serial!=snapshotSerial)continue;
     if(total<0||total>16384||chunk.Length>128||offset!=receivedBubbles||offset+chunk.Length>total)throw new IOException("잘못된 표현 조각");
     if((DateTime.UtcNow-snapshotStarted).TotalSeconds>2){pendingBubbles=null;continue;}
     if(pendingBubbles==null)pendingBubbles=new NetworkBubble[total];if(pendingBubbles.Length!=total)throw new IOException("표현 크기 불일치");
     Array.Copy(chunk,0,pendingBubbles,offset,chunk.Length);receivedBubbles+=chunk.Length;
     if(receivedBubbles==total){Visuals.Bubbles=pendingBubbles;pendingBubbles=null;}
    }else if(kind=="error"){Fail(r.ReadString());return;}else if(kind=="closed"){Interrupt();return;}else if(kind!="loading")throw new IOException("프로토콜 불일치");}
   if((DateTime.UtcNow-peer.Seen).TotalSeconds>10){Interrupt();return;}
   if((DateTime.UtcNow-lastPing).TotalSeconds>1){peer.Send("ping");lastPing=DateTime.UtcNow;}
  }catch(Exception e)when(e is IOException||e is SocketException||e is ObjectDisposedException||e is ArgumentException){Interrupt();}}
  public void Ready(){if(peer==null)return;try{ready=true;peer.Send("ready");Status=ConnectionStatus.Waiting;}catch{Interrupt();}}
  public void SubmitInput(NetworkInput input){if(Status==ConnectionStatus.Playing)try{peer.Send("input",input);}catch{Interrupt();}}
  public void Cancel(){pending?.Close();pending=null;peer?.Dispose();peer=null;connect=null;ready=false;Snapshot=null;Visuals=null;pendingBubbles=null;receivedBubbles=0;Slot=-1;Status=ConnectionStatus.Idle;Error=null;}
  void Fail(string error){Cancel();Error=error;Status=ConnectionStatus.Failed;}
  void Interrupt(){Cancel();Status=ConnectionStatus.Interrupted;Error="호스트 연결이 종료되어 승패 없이 매치를 중단했습니다";}
  public void Dispose(){Cancel();}
 }
}
