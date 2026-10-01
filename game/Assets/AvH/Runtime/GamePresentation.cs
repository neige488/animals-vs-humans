using System.Linq;
using UnityEngine;

namespace AvH {
 public sealed class GamePresentation : MonoBehaviour {
  UnityPlaytestSession session;
  Camera view;
  bool ownsView;Light ownedSun;
  float cameraDistance=5.54f;
  readonly System.Collections.Generic.Dictionary<Renderer,UnityEngine.Rendering.ShadowCastingMode> hiddenLocal=new System.Collections.Generic.Dictionary<Renderer,UnityEngine.Rendering.ShadowCastingMode>();
  RosterHud rosterHud;
  NetworkPresentation network;
  bool wasPlaying;
  string nickname="플레이어";
  float yaw, pitch=20;
  bool menu,debugOpen;
  public bool DebugPanelOpen=>debugOpen;
  public void SetDebugPanelOpen(bool open){debugOpen=open;if(open)menu=false;if(!open&&session.Session!=null&&!network.IsClient)session.Session.CancelSettingsEdit(0);SetCursor();}
  Vector3 cameraOffset=new Vector3(.6f,.3f,-5.5f);
  readonly SettingsPanel settingsPanel=new SettingsPanel();
  GUIStyle label, title;
  // Override the fullscreen preference saved by older playtest builds on every desktop launch.
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
  static void ConfigureWindow() {
   if(!Application.isEditor&&!Application.isBatchMode)Screen.SetResolution(1280,720,FullScreenMode.Windowed);
  }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch() {
   if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="SampleScene" && Object.FindAnyObjectByType<GamePresentation>()==null && !Application.isBatchMode)
    new GameObject("Animals vs Humans").AddComponent<GamePresentation>();
  }
  void Awake() {
   session=gameObject.AddComponent<UnityPlaytestSession>();
   network=gameObject.AddComponent<NetworkPresentation>();network.Initialize(session);
   view=Camera.main;
   if(view==null) {var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";view=cameraObject.AddComponent<Camera>();ownsView=true;cameraObject.AddComponent<AudioListener>();}
   rosterHud=new GameObject("Match roster HUD").AddComponent<RosterHud>();rosterHud.Initialize(view);rosterHud.Present(null,0);
   view.fieldOfView=65;view.farClipPlane=180;view.backgroundColor=new Color(.45f,.7f,.85f);view.clearFlags=CameraClearFlags.Skybox;
   if(Object.FindAnyObjectByType<Light>()==null) {var light=new GameObject("Sun").AddComponent<Light>();ownedSun=light;light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-35,0);}
   TownLighting.Apply(view,Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional));
  }
  // Development-only, isolated startup harness for capturing the real IMGUI overlay.
  void Start() {
   if(!Debug.isDebugBuild||Application.isBatchMode)return;
   var args=System.Environment.GetCommandLineArgs();int capture=System.Array.IndexOf(args,"-avhDebugPreview");
   if(capture<0||capture+1>=args.Length)return;
   session.StartSolo("디버그",123,System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml"));
   SetDebugPanelOpen(true);settingsPanel.SelectTab(1);StartCoroutine(CaptureDebugPreview(args[capture+1]));
  }
  System.Collections.IEnumerator CaptureDebugPreview(string folder) {
   System.IO.Directory.CreateDirectory(folder);
   for(int frame=0;frame<90;frame++) {
    if(frame==30||frame==60) {
     session.Session.BeginSettingsEdit(0);var values=session.Session.ObserveSettings().Edit;values.HumanSpeed=frame==30?8:3;
     session.Session.UpdateSettingsEdit(0,values);session.Session.ApplySettingsNow(0,false);
    }
    yield return new WaitForEndOfFrame();
    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,$"frame-{frame:0000}.png"));
    yield return new WaitForSecondsRealtime(1f/30);
   }
  }
  void Update() {
   if(session.Session==null||!network.CanPlay){rosterHud.Present(null,0);wasPlaying=false;debugOpen=false;return;}
   rosterHud.Present(session.Observe(),network.LocalSlot);
   if(!wasPlaying){menu=false;SetCursor();wasPlaying=true;}
   if(Input.GetKeyDown(KeyCode.F1))SetDebugPanelOpen(!debugOpen);
   if(Input.GetKeyDown(KeyCode.Escape)&&debugOpen)SetDebugPanelOpen(false);
   else if(Input.GetKeyDown(KeyCode.Escape)) {menu=!menu;if(!menu&&!network.IsClient)session.Session.CancelSettingsEdit(0);SetCursor();}
   if(!menu && !debugOpen && (network.IsClient||string.IsNullOrEmpty(session.Session.ObserveSettings().Error))) {
    if(Cursor.lockState!=CursorLockMode.Locked && Input.GetMouseButtonDown(0))SetCursor();
    SetLookAngles(yaw+Input.GetAxisRaw("Mouse X")*2,pitch-Input.GetAxisRaw("Mouse Y")*2);
   }
   var input=new PlayerInput {Yaw=yaw,Pitch=pitch};
   if(!menu && (network.IsClient||string.IsNullOrEmpty(session.Session.ObserveSettings().Error))) {input.Right=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);input.Forward=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);input.Jump=Input.GetKeyDown(KeyCode.Space);input.Attack=!debugOpen&&Input.GetMouseButton(0);input.Reload=Input.GetKeyDown(KeyCode.R);}
   network.SubmitInput(input);
  }
  void LateUpdate() {
   if(session.Session==null||!network.CanPlay)return;
   var target=session.PlayerTransform(network.LocalSlot).position+Vector3.up*1.35f;
   var rotation=Quaternion.Euler(pitch,yaw,0);
   var preferred=new Vector3(.6f,.3f,-5.5f);float length=preferred.magnitude;
   var chosen=preferred;float preferredClear=ClearDistance(target,rotation*preferred);
   if(preferredClear/length<.85f) {
    float retained=ClearDistance(target,rotation*cameraOffset)/length;
    if(retained>=.6f)chosen=cameraOffset;
    else {
     var candidates=new[]{new Vector3(-3.2f,.3f,-5.5f),new Vector3(3.2f,.3f,-5.5f),new Vector3(.6f,2.5f,-5.5f)};
     float best=preferredClear/length;
     foreach(var candidate in candidates){var normalized=candidate.normalized*length;float ratio=ClearDistance(target,rotation*normalized)/length;if(ratio>best+.15f){chosen=normalized;best=ratio;}if(best>=.8f)break;}
    }
   }
   cameraOffset=Vector3.Lerp(cameraOffset,chosen,1-Mathf.Exp(-12*Time.unscaledDeltaTime)).normalized*length;
   var offset=rotation*cameraOffset;float clear=ClearDistance(target,offset);
   // Collision compression is immediate; recovery and shoulder changes are gradual.
   cameraDistance=Mathf.Min(clear,Mathf.Lerp(cameraDistance,clear,1-Mathf.Exp(-12*Time.unscaledDeltaTime)));
   view.transform.position=target+offset.normalized*cameraDistance;
   bool hide=cameraDistance<1.2f;
   if(hide){foreach(var renderer in session.PlayerTransform(network.LocalSlot).GetComponentsInChildren<Renderer>()){if(!hiddenLocal.ContainsKey(renderer))hiddenLocal[renderer]=renderer.shadowCastingMode;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;}}
   else RestoreLocalRenderers();
   view.transform.rotation=Quaternion.LookRotation(target+rotation*Vector3.forward*8-view.transform.position);
  }
  static float ClearDistance(Vector3 target,Vector3 offset) {
   float distance=offset.magnitude;
   foreach(var hit in Physics.SphereCastAll(target,.18f,offset.normalized,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) {
    if(hit.collider.GetComponentInParent<CharacterController>()!=null)continue;
    distance=Mathf.Min(distance,Mathf.Max(0,hit.distance-.15f));
   }
   // In a fully enclosed space there may be no third person angle; never put the camera
   // through a wall merely to preserve distance.
   return distance;
  }
  void RestoreLocalRenderers(){foreach(var pair in hiddenLocal)if(pair.Key!=null)pair.Key.shadowCastingMode=pair.Value;hiddenLocal.Clear();}
  public void SetLookAngles(float horizontal,float vertical){yaw=horizontal;pitch=Mathf.Clamp(vertical,-20,65);}
  void OnApplicationFocus(bool focused) {
   if(focused&&wasPlaying&&!menu&&!debugOpen&&session!=null&&session.Session!=null&&network.CanPlay)SetCursor();
  }

  void SetCursor(){Cursor.lockState=menu||debugOpen?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=menu||debugOpen;}
  void OnGUI() {
   if(label==null){GUI.skin.font=Font.CreateDynamicFontFromOSFont(new[]{"Apple SD Gothic Neo","Malgun Gothic","Arial"},20);label=new GUIStyle(GUI.skin.label){fontSize=20,alignment=TextAnchor.MiddleCenter};label.normal.textColor=Color.white;title=new GUIStyle(label){fontSize=30};}
   var w=Screen.width;var h=Screen.height;
   if(session.Session==null||!network.CanPlay||menu) {
    GUI.enabled=true;
    if(GUI.Button(new Rect(w-160,16,140,36),"게임 종료")){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Application.Quit();return;}
   }
   if(session.Session==null||!network.CanPlay) {
    GUI.Box(new Rect(w/2-220,h/2-145,440,290),"");
    GUI.Label(new Rect(w/2-210,h/2-125,420,45),"Animals vs Humans",title);
    GUI.Label(new Rect(w/2-190,h/2-65,380,30),"닉네임 (1~20자)",label);
    nickname=GUI.TextField(new Rect(w/2-160,h/2-25,320,35),nickname,20);
    if(string.IsNullOrWhiteSpace(nickname))GUI.Label(new Rect(w/2-210,h/2+15,420,30),"닉네임을 입력해주세요.",label);
    GUI.enabled=true;network.DrawStart(nickname,label);return;
   }
   var state=session.Observe();
   if(state.Phase==RoundPhase.Results) {
    GUI.Box(new Rect(w/2-260,h/2-120,520,240),"");
    bool humansWon=state.Winner==Faction.Human;
    string result=state.Winner.HasValue?(humansWon?"인간 팀 승리!":"동물 팀 승리!"):"라운드 종료";
    GUI.Label(new Rect(w/2-250,h/2-100,500,55),result,title);
    GUI.Label(new Rect(w/2-250,h/2-35,500,45),state.Winner.HasValue?(humansWon?"마지막까지 살아남았습니다":"모두 동물로 변신했습니다!"):"",label);
    int seconds=Mathf.Max(0,Mathf.CeilToInt((float)state.SecondsRemaining));
    GUI.Label(new Rect(w/2-250,h/2+35,500,45),$"다음 라운드까지 {seconds/60:00}:{seconds%60:00}",label);
   }
   if(state.Phase!=RoundPhase.Results)GUI.Label(new Rect(w/2-15,h/2-20,30,40),"+",title);
   GUI.Box(new Rect(w/2-280,h-90,560,70),"");
   GUI.Label(new Rect(w/2-275,h-85,550,35),$"{state.Players[network.LocalSlot].CharacterName} · {WeaponLabel(state.Players[network.LocalSlot],session.ObserveActiveSettings().Current.Magazine)} · 라운드 {state.Round}",label);
   GUI.Label(new Rect(w/2-275,h-53,550,25),"WASD 이동 · 마우스 시점 · Space 점프 · 좌클릭 공격 · R 재장전 · Esc 메뉴 · F1 디버그",new GUIStyle(label){fontSize=15});

   if(Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog")==null) GUI.Label(new Rect(15,110,450,35),"개발 블록아웃 · 보유 에셋 적용 전",new GUIStyle(label){fontSize=16,alignment=TextAnchor.MiddleLeft});
   network.DrawRoom(label);
   if(menu) {
    if(!network.IsClient&&GUI.Button(new Rect(w/2-125,h/2-30,250,40),"테스트 설정 열기")){menu=false;SetDebugPanelOpen(true);return;}
    else if(GUI.Button(new Rect(w/2-125,h/2-30,250,40),"계속하기")){menu=false;SetCursor();}
    if(GUI.Button(new Rect(w-200,h-50,180,35),"방 나가기")){network.Leave();menu=false;return;}
   }
   if(!menu){
    if(debugOpen)settingsPanel.Draw(session.Session,()=>SetDebugPanelOpen(false),network.IsClient?1:0);
    else if(GUI.Button(new Rect(w-160,205,140,30),"디버그 · F1"))SetDebugPanelOpen(true);
   }
   if(!(network.IsClient||string.IsNullOrEmpty(session.Session.ObserveSettings().Error))){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;settingsPanel.DrawError(session.Session);if((network.IsClient||string.IsNullOrEmpty(session.Session.ObserveSettings().Error)))SetCursor();}

  }
  static string WeaponLabel(PlayerState player,int magazine) {
   if(player.Faction==Faction.Animal)return player.AttackGraceRemaining>0?$"공격 대기 {player.AttackGraceRemaining:F1}초":"근접 공격";
   return player.ReloadRemaining>0?$"재장전 중 ({player.ReloadRemaining:F1}초)":$"버블 {player.Ammo} / {magazine} (예비 ∞)";
  }
  void OnDestroy(){RestoreLocalRenderers();if(ownsView&&view!=null)Destroy(view.gameObject);if(ownedSun!=null)Destroy(ownedSun.gameObject);if(rosterHud!=null)Destroy(rosterHud.gameObject);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
 }
}
