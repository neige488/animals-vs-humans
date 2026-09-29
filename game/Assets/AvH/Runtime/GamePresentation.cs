using System.Linq;
using UnityEngine;

namespace AvH {
 public sealed class GamePresentation : MonoBehaviour {
  UnityPlaytestSession session;
  Camera view;
  string nickname="플레이어";
  float yaw, pitch=20;
  bool menu;
  GUIStyle label, title;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch() {
   if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="SampleScene" && Object.FindAnyObjectByType<GamePresentation>()==null && !Application.isBatchMode)
    new GameObject("Animals vs Humans").AddComponent<GamePresentation>();
  }
  void Awake() {
   session=gameObject.AddComponent<UnityPlaytestSession>();
   view=Camera.main;
   if(view==null) {var cameraObject=new GameObject("Main Camera");view=cameraObject.AddComponent<Camera>();cameraObject.AddComponent<AudioListener>();}
   view.fieldOfView=65;view.farClipPlane=180;view.backgroundColor=new Color(.45f,.7f,.85f);view.clearFlags=CameraClearFlags.Skybox;
   if(Object.FindAnyObjectByType<Light>()==null) {var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-35,0);}
  }
  void Update() {
   if(session.Session==null)return;
   if(Input.GetKeyDown(KeyCode.Escape)) {menu=!menu;SetCursor();}
   if(!menu) {yaw+=Input.GetAxisRaw("Mouse X")*2;pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*2,-20,65);}
   var input=new PlayerInput {Yaw=yaw};
   if(!menu) {input.Right=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);input.Forward=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);input.Jump=Input.GetKeyDown(KeyCode.Space);}
   session.SubmitInput(0,input);
  }
  void LateUpdate() {
   if(session.Session==null)return;
   var target=session.PlayerTransform(0).position+Vector3.up*1.35f;
   var rotation=Quaternion.Euler(pitch,yaw,0);
   var offset=rotation*new Vector3(.6f,.3f,-5.5f);
   float distance=offset.magnitude;
   // Ignore player colliders so the camera does not collapse into its own character.
   foreach(var hit in Physics.SphereCastAll(target,.18f,offset.normalized,distance)) {
    if(hit.collider.GetComponentInParent<CharacterController>()!=null)continue;
    distance=Mathf.Min(distance,Mathf.Max(.5f,hit.distance-.15f));
   }
   view.transform.position=target+offset.normalized*distance;
   view.transform.rotation=Quaternion.LookRotation(target+rotation*Vector3.forward*8-view.transform.position);
  }
  void SetCursor(){Cursor.lockState=menu?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=menu;}
  void OnGUI() {
   if(label==null){GUI.skin.font=Font.CreateDynamicFontFromOSFont(new[]{"Apple SD Gothic Neo","Malgun Gothic","Arial"},20);label=new GUIStyle(GUI.skin.label){fontSize=20,alignment=TextAnchor.MiddleCenter};label.normal.textColor=Color.white;title=new GUIStyle(label){fontSize=30};}
   var w=Screen.width;var h=Screen.height;
   if(session.Session==null) {
    GUI.Box(new Rect(w/2-220,h/2-145,440,290),"");
    GUI.Label(new Rect(w/2-210,h/2-125,420,45),"Animals vs Humans",title);
    GUI.Label(new Rect(w/2-190,h/2-65,380,30),"닉네임 (1~20자)",label);
    nickname=GUI.TextField(new Rect(w/2-160,h/2-25,320,35),nickname,20);
    GUI.enabled=!string.IsNullOrWhiteSpace(nickname);
    if(GUI.Button(new Rect(w/2-160,h/2+25,320,45),"혼자 시작 · 11봇")){session.StartSolo(nickname.Trim());SetCursor();}
    GUI.enabled=true;return;
   }
   var state=session.Observe();var animals=state.Players.Count(p=>p.Faction==Faction.Animal);
   string phase=state.Phase==RoundPhase.Preparation?"준비":"추격";
   if(state.Phase==RoundPhase.Results) {
    GUI.Box(new Rect(w/2-260,h/2-120,520,240),"");
    bool humansWon=state.Winner==Faction.Human;
    string result=state.Winner.HasValue?(humansWon?"인간 팀 승리!":"동물 팀 승리!"):"라운드 종료";
    GUI.Label(new Rect(w/2-250,h/2-100,500,55),result,title);
    GUI.Label(new Rect(w/2-250,h/2-35,500,45),state.Winner.HasValue?(humansWon?"마지막까지 살아남았습니다":"모두 동물이 되었습니다"):"",label);
    int seconds=Mathf.Max(0,Mathf.CeilToInt((float)state.SecondsRemaining));
    GUI.Label(new Rect(w/2-250,h/2+35,500,45),$"다음 라운드까지 {seconds/60:00}:{seconds%60:00}",label);
   } else {
   GUI.Box(new Rect(w/2-300,12,600,90),"");
   GUI.Label(new Rect(w/2-295,15,590,40),$"인간 {state.Players.Length-animals}   |   {phase} {Mathf.CeilToInt((float)state.SecondsRemaining)}초   |   동물 {animals}",label);
   }
   if(state.Phase==RoundPhase.Chase && state.SecondsRemaining>174) {
    var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
    GUI.color=catalog==null?Color.white:catalog.RarityColor;
    GUI.Label(new Rect(w/2-290,55,580,35),string.Join(" / ",state.Births.Select(b=>$"{b.Rarity} {b.Kind} ×{b.Count} 탄생!")),label);GUI.color=Color.white;
   }
   GUI.Label(new Rect(w/2-15,h/2-20,30,40),"+",title);
   GUI.Box(new Rect(w/2-280,h-90,560,70),"");
   GUI.Label(new Rect(w/2-275,h-85,550,35),$"{(state.Players[0].Faction==Faction.Human?"인간":"동물")}  ·  라운드 {state.Round}",label);
   GUI.Label(new Rect(w/2-275,h-53,550,25),"WASD 이동 · 마우스 시점 · Space 점프 · Esc 메뉴",new GUIStyle(label){fontSize=15});
   if(Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog")==null) GUI.Label(new Rect(15,110,450,35),"개발 블록아웃 · 보유 에셋 적용 전",new GUIStyle(label){fontSize=16,alignment=TextAnchor.MiddleLeft});
   if(menu) {GUI.Box(new Rect(w/2-150,h/2-80,300,150),"메뉴 · 라운드는 계속됩니다");if(GUI.Button(new Rect(w/2-125,h/2-30,250,40),"계속하기")){menu=false;SetCursor();}}
  }
  void OnDestroy(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
 }
}
