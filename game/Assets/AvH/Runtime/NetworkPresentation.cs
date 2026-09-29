using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
namespace AvH {
 /// <summary>Unity bridge: only hosts execute physics; peers render snapshots and submit inputs.</summary>
 [DefaultExecutionOrder(-100)] public sealed class NetworkPresentation : MonoBehaviour {
  SessionState lastAppliedState;NetworkVisualState lastAppliedVisual;
  UnityPlaytestSession world;PrivateRoomHost host;readonly PrivateRoomClient client=new PrivateRoomClient();
  readonly NetworkInput[] buffered=new NetworkInput[12];readonly bool[] hasInput=new bool[12];
  string code="",address,lastNickname="",message="";bool clientStarted;float nextPump;
  public bool IsClient=>clientStarted;
  public int LocalSlot=>IsClient?client.Slot:0;
  public bool CanPlay=>!IsClient||client.Status==ConnectionStatus.Playing;
  public string RoomCode=>host?.RoomCode;
  public NetworkVisualState RemoteVisuals=>client.Visuals;
  public void Initialize(UnityPlaytestSession session){world=session;address=FindAddress();}
  static string FindAddress(){try{return NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Select(a=>a.Address).FirstOrDefault(a=>a.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a))?.ToString()??"127.0.0.1";}catch{return "127.0.0.1";}}
  public void StartHost(string nickname,string advertisedAddress=null){try{world.StartSolo(nickname);host=new PrivateRoomHost(world.Session,advertisedAddress??address);host.InputReceived=(slot,input)=>{if(input.RoundId!=0){input.Jump|=buffered[slot].Jump;input.Attack|=buffered[slot].Attack;input.Reload|=buffered[slot].Reload;}buffered[slot]=input;hasInput[slot]=true;};host.CaptureVisuals=()=>new NetworkVisualState{Bursts=world.ObserveBursts(),Yaws=Enumerable.Range(0,12).Select(i=>world.PlayerTransform(i).eulerAngles.y).ToArray(),Bubbles=world.ObserveBubbles().Select(b=>new NetworkBubble{Id=b.Id,OwnerSlot=b.OwnerSlot,Round=b.Round,Position=b.Position,Direction=b.Direction,RemainingLife=b.RemainingLife,Travelled=b.Travelled}).ToArray()};message="";}catch(Exception){host?.Dispose();host=null;world.ResetSession();message="방을 열지 못했습니다. 호스트 주소와 네트워크 상태를 확인하세요.";}}
  public void JoinRoom(string roomCode,string nickname){code=roomCode;Join(nickname);}
  public void Join(string nickname){lastNickname=nickname;clientStarted=true;message="";client.Connect(code,nickname);}
  void Update(){
   if(host!=null&&Time.unscaledTime>=nextPump){host.Pump();for(int slot=1;slot<12;slot++)if(hasInput[slot]){var input=buffered[slot];world.SubmitInput(slot,new PlayerInput{Right=input.Right,Forward=input.Forward,Yaw=input.Yaw,Pitch=input.Pitch,Jump=input.Jump,Attack=input.Attack,Reload=input.Reload,RoundId=input.RoundId});hasInput[slot]=false;buffered[slot]=default(NetworkInput);}nextPump=Time.unscaledTime+.05f;}
   if(!clientStarted)return;client.Pump();
   if(client.Snapshot!=null){
    if(!ReferenceEquals(lastAppliedState,client.Snapshot)){if(world.Session==null){world.StartRemote(client.Snapshot);client.Ready();}else world.ApplyRemoteSnapshot(client.Snapshot);lastAppliedState=client.Snapshot;}
    if(!ReferenceEquals(lastAppliedVisual,client.Visuals)){world.ApplyRemoteVisuals(client.Visuals);lastAppliedVisual=client.Visuals;}
   }
   if(client.Status==ConnectionStatus.Interrupted||client.Status==ConnectionStatus.Failed){if(world.Session!=null)world.ResetSession();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
  }
  public void SubmitInput(PlayerInput input){if(!CanPlay)return;if(!IsClient){world.SubmitInput(0,input);return;}input.RoundId=client.Snapshot.Round;client.SubmitInput(new NetworkInput{Right=input.Right,Forward=input.Forward,Yaw=input.Yaw,Pitch=input.Pitch,Jump=input.Jump,Attack=input.Attack,Reload=input.Reload,RoundId=input.RoundId});}
  public void Leave(){host?.Dispose();host=null;client.Cancel();clientStarted=false;lastAppliedState=null;lastAppliedVisual=null;world.ResetSession();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
  public void DrawStart(string nickname,GUIStyle label){
   var w=Screen.width;var h=Screen.height;
   if(clientStarted){
    GUI.Box(new Rect(w/2-280,h/2-70,560,230),"");
    if(client.Status==ConnectionStatus.Interrupted){
     GUI.Label(new Rect(w/2-270,h/2-50,540,75),"호스트와의 연결이 종료되었습니다.\n이번 라운드는 중단되었습니다.",new GUIStyle(label){wordWrap=true});
     if(GUI.Button(new Rect(w/2-125,h/2+40,250,42),"시작 화면으로"))Leave();return;
    }
    GUI.Label(new Rect(w/2-270,h/2-50,540,75),client.Error??(client.Status==ConnectionStatus.Waiting?"다음 준비 구간을 기다립니다 · 봇은 계속 플레이합니다":"방에 연결 중입니다 · 봇은 계속 플레이합니다"),new GUIStyle(label){wordWrap=true});
    if(client.Status==ConnectionStatus.Failed)if(GUI.Button(new Rect(w/2-245,h/2+40,230,42),"재시도")){world.ResetSession();client.Connect(code,lastNickname);}
    if(GUI.Button(new Rect(w/2+15,h/2+40,230,42),"취소 · 시작 화면"))Leave();return;
   }
   GUI.Box(new Rect(w/2-280,h/2+65,560,275),"");
   GUI.Label(new Rect(w/2-270,h/2+70,540,30),"같은 LAN 또는 기존 VPN에서 연결",label);
   GUI.Label(new Rect(w/2-270,h/2+101,175,25),"호스트 IPv4 주소");address=GUI.TextField(new Rect(w/2-85,h/2+100,345,28),address,45);
   IPAddress ip;bool validAddress=IPAddress.TryParse(address,out ip)&&ip.AddressFamily==AddressFamily.InterNetwork;GUI.enabled=!string.IsNullOrWhiteSpace(nickname)&&validAddress;
   GUI.Label(new Rect(w/2-260,h/2+128,520,22),validAddress?"":"호스트 IPv4 주소를 확인하세요");
   if(GUI.Button(new Rect(w/2-260,h/2+153,520,36),"비공개 방 만들기 · 11봇"))StartHost(nickname.Trim());GUI.enabled=true;
   GUI.Label(new Rect(w/2-270,h/2+196,90,25),"방 코드");code=GUI.TextField(new Rect(w/2-175,h/2+194,435,28),code,256);
   string endpoint,key;int port;GUI.enabled=!string.IsNullOrWhiteSpace(nickname)&&PrivateRoomCode.TryParse(code,out endpoint,out port,out key);
   GUI.Label(new Rect(w/2-260,h/2+224,520,22),string.IsNullOrWhiteSpace(code)?"방 코드를 입력해주세요.":PrivateRoomCode.TryParse(code,out endpoint,out port,out key)?"":"방 코드 형식을 확인하세요");
   if(GUI.Button(new Rect(w/2-260,h/2+248,520,36),"코드로 참가"))Join(nickname.Trim());GUI.enabled=true;
   GUI.Label(new Rect(w/2-270,h/2+287,540,48),message,new GUIStyle(label){fontSize=16,wordWrap=true});
  }
  public void DrawRoom(GUIStyle label){if(IsClient&&client.VisualsDelayed)GUI.Label(new Rect(15,218,500,32),"버블 표현이 지연되고 있습니다",label);if(IsClient&&client.Visuals!=null)GUI.Label(new Rect(15,150,400,28),"호스트 설정 버전 "+client.Visuals.SettingsVersion+(client.Visuals.HasPending?" · 다음 라운드 적용 예정":""),label);if(host!=null){GUI.Label(new Rect(15,150,400,28),"비공개 방 · LAN/기존 VPN",label);if(GUI.Button(new Rect(20,184,170,30),"방 코드 복사"))GUIUtility.systemCopyBuffer=host.RoomCode;} }
  void OnDestroy(){host?.Dispose();client.Dispose();}
 }
}
