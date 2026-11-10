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
  public bool AutomatedProbeInput;
  public bool DebugPanelOpen=>debugOpen;
  public void SetDebugPanelOpen(bool open){debugOpen=open;if(open)menu=false;if(!open&&session.Session!=null&&!network.IsClient)session.Session.CancelSettingsEdit(0);SetCursor();}
  Vector3 cameraOffset=new Vector3(.6f,.3f,-5.5f);
  readonly SettingsPanel settingsPanel=new SettingsPanel();
  readonly FeelDirector feel=new FeelDirector();
  readonly HitFeed hitFeed=new HitFeed();
  /// <summary>Recent shot hits as this viewer reads them (who shoved whom).</summary>
  public HitFeed Hits=>hitFeed;
  readonly DisplaySettingsMenu displayMenu=new DisplaySettingsMenu();
  LocalDisplaySettings display;int appliedDisplay=-1;
  Vector3 follow,followVelocity;bool following;
  /// <summary>This PC's graphics quality and screen-shake preferences.</summary>
  public LocalDisplaySettings Display=>display;
  public FeelDirector Feel=>feel;
  /// <summary>Angle (degrees) the shake currently adds to the camera; 0 when this viewer is not involved.</summary>
  public float CameraShakeAngle {get;private set;}
  /// <summary>Spring-followed camera pivot.</summary>
  public Vector3 FollowPoint=>follow;
  /// <summary>Replaces the per-PC display preferences (tests and isolated profiles).</summary>
  public void UseDisplaySettings(LocalDisplaySettings settings){display=settings??new LocalDisplaySettings(null);appliedDisplay=-1;ApplyDisplay();}
  void ApplyDisplay(){if(display==null)return;appliedDisplay=display.Version;MapAir.Apply(view,TownLighting.Current,display.Quality);feel.Enabled=display.ScreenShake;feel.Scale=DisplayQuality.ShakeScale(display.Quality);DisplayQuality.Apply(display.Quality,view);}
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
   if(display==null)UseDisplaySettings(new LocalDisplaySettings(System.IO.Path.Combine(Application.persistentDataPath,"display-settings.xml")));
  }
  // Development-only, isolated startup harness for capturing the real IMGUI overlay.
  void Start() {
   if(!Debug.isDebugBuild||Application.isBatchMode)return;
   var args=System.Environment.GetCommandLineArgs();
   if(WarehouseScenario.TryStart(this,network,session,args))return;
   if(NetworkFrameProbe.TryStart(this,network,session,args))return;
   int menus=System.Array.IndexOf(args,"-avhMenuPreview");
   if(menus>=0&&menus+1<args.Length){StartCoroutine(CaptureMenuPreview(args[menus+1]));return;}
   int qualities=System.Array.IndexOf(args,"-avhQualityPreview");
   if(qualities>=0&&qualities+1<args.Length){StartCoroutine(CaptureQualityPreview(args[qualities+1]));return;}
   int capture=System.Array.IndexOf(args,"-avhDebugPreview");
   if(capture<0||capture+1>=args.Length)return;
   session.StartSolo("디버그",123,System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml"));
   SetDebugPanelOpen(true);settingsPanel.SelectTab(3);StartCoroutine(CaptureDebugPreview(args[capture+1]));
  }
  // Development-only: start screen, Esc menu (with a failed display save) and the impact tuning tab, in an isolated profile.
  System.Collections.IEnumerator CaptureMenuPreview(string folder) {
   System.IO.Directory.CreateDirectory(folder);var temp=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid().ToString("N"));
   UseDisplaySettings(new LocalDisplaySettings(System.IO.Path.Combine(temp,"display-settings.xml")));
   System.Collections.IEnumerator Shot(string name){for(int i=0;i<20;i++)yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,name+".png"));yield return new WaitForSecondsRealtime(.3f);}
   yield return Shot("start-first-run");
   display.SetQuality(GraphicsQuality.Medium);display.SetScreenShake(false);yield return Shot("start-medium-shake-off");
   session.StartSolo("미리보기",123,System.IO.Path.Combine(temp,"playtest.xml"));yield return null;yield return null;menu=true;SetCursor();yield return Shot("esc-menu");
   System.IO.Directory.CreateDirectory(System.IO.Path.Combine(temp,"display-settings.xml.tmp"));
   display.SetQuality(GraphicsQuality.Low);displayMenu.ShowNotice();menu=true;yield return Shot("esc-menu-save-failed");
   menu=false;SetDebugPanelOpen(true);settingsPanel.SelectTab(4);yield return Shot("debug-impact-tab");
   Application.Quit();
  }
  // Development-only: the same market view at high, medium and low quality, in an isolated profile.
  System.Collections.IEnumerator CaptureQualityPreview(string folder) {
   System.IO.Directory.CreateDirectory(folder);var temp=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid().ToString("N"));
   UseDisplaySettings(new LocalDisplaySettings(System.IO.Path.Combine(temp,"display-settings.xml")));
   session.StartSolo("품질",123,System.IO.Path.Combine(temp,"playtest.xml"));session.BotAutomationEnabled=false;yield return null;
   SetLookAngles(15,12);
   foreach(var quality in new[]{GraphicsQuality.High,GraphicsQuality.Medium,GraphicsQuality.Low}) {
    display.SetQuality(quality);for(int i=0;i<45;i++)yield return null;yield return new WaitForEndOfFrame();
    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"quality-"+quality.ToString().ToLowerInvariant()+".png"));yield return new WaitForSecondsRealtime(.5f);
   }
   Application.Quit();
  }
  System.Collections.IEnumerator CaptureDebugPreview(string folder) {
   System.IO.Directory.CreateDirectory(folder);
   for(int frame=0;frame<90;frame++) {
    if(frame==30||frame==60) {
     session.Session.BeginSettingsEdit(0);var values=session.Session.ObserveSettings().Edit;values.FoxSpeedMultiplier=frame==30?1.8f:1.1f;
     session.Session.UpdateSettingsEdit(0,values);session.Session.ApplySettingsNow(0,false);
    }
    yield return new WaitForEndOfFrame();
    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,$"frame-{frame:0000}.png"));
    yield return new WaitForSecondsRealtime(1f/30);
   }
  }
  // Each map has its own time of day; follow whichever map the session has built.
  int litMap=-1;
  void FollowMapLook(){if(session==null||session.BuiltMap==litMap||view==null)return;litMap=session.BuiltMap;TownLighting.Apply(view,Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional),litMap);appliedDisplay=-1;}
  void Update() {
   FollowMapLook();
   if(display!=null&&appliedDisplay!=display.Version)ApplyDisplay();
   if(session.Session==null||!network.CanPlay){rosterHud.Present(null,0);wasPlaying=false;debugOpen=false;hitFeed.Reset();return;}
   rosterHud.Present(session.Observe(),network.LocalSlot);hitFeed.Observe(session.Observe(),Time.deltaTime);
   if(!wasPlaying){menu=false;SetCursor();wasPlaying=true;}
   if(Input.GetKeyDown(KeyCode.F1))SetDebugPanelOpen(!debugOpen);
   if(Input.GetKeyDown(KeyCode.Escape)&&debugOpen)SetDebugPanelOpen(false);
   else if(Input.GetKeyDown(KeyCode.Escape)) {menu=!menu;if(!menu&&!network.IsClient)session.Session.CancelSettingsEdit(0);SetCursor();}
   if(!menu && !debugOpen && (network.IsClient||string.IsNullOrEmpty(session.Session.ObserveSettings().Error))) {
    if(Cursor.lockState!=CursorLockMode.Locked && Input.GetMouseButtonDown(0))SetCursor();
    SetLookAngles(yaw+Input.GetAxisRaw("Mouse X")*2,pitch-Input.GetAxisRaw("Mouse Y")*2);
   }
   var input=new PlayerInput {Yaw=yaw,Pitch=pitch};
   if(!menu && (network.IsClient||string.IsNullOrEmpty(session.Session.ObserveSettings().Error))) {input.Right=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);input.Forward=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);input.Jump=Input.GetKeyDown(KeyCode.Space);input.Attack=!debugOpen&&Input.GetMouseButton(0);input.Reload=Input.GetKeyDown(KeyCode.R);input.Ability=Input.GetKeyDown(KeyCode.Q);}
   if(!AutomatedProbeInput)network.SubmitInput(input);session.PresentTrigger(network.LocalSlot,input.Attack);
  }
  void LateUpdate() {
   if(session.Session==null||!network.CanPlay){following=false;feel.Reset();CameraShakeAngle=0;return;}
   if(appliedDisplay!=display.Version)ApplyDisplay();
   var target=session.CameraFollowTransform(network.LocalSlot).position+Vector3.up*1.35f;
   // Spring follow softens steps and landings; spawns and recovery teleports snap.
   if(!following||(target-follow).sqrMagnitude>9){follow=target;followVelocity=Vector3.zero;following=true;}
   else follow=Vector3.SmoothDamp(follow,target,ref followVelocity,.07f,Mathf.Infinity,Time.unscaledDeltaTime);
   // Landing dip springs back; a launch (our body tumbling) widens the view for a moment.
   landDip=Mathf.MoveTowards(landDip,0,Time.unscaledDeltaTime*.9f);target=follow+Vector3.down*Mathf.Sin(Mathf.Min(1,landDip/.32f)*Mathf.PI*.5f)*landDip;
   var anim=session.PlayerTransform(network.LocalSlot).GetComponent<CharacterAnimator>();bool launched=anim!=null&&anim.Observe().Tumbling;
   fovPunch=Mathf.MoveTowards(fovPunch,launched?1:0,Time.unscaledDeltaTime*(launched?6:2));view.fieldOfView=BaseFieldOfView+7*Mathf.SmoothStep(0,1,fovPunch);
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
   var look=Quaternion.LookRotation(target+rotation*Vector3.forward*8-view.transform.position);
   session.AbilityViewerSlot=network.LocalSlot;feel.Observe(session.Observe(),network.LocalSlot,Time.unscaledDeltaTime);
   view.transform.rotation=look*feel.Shake();CameraShakeAngle=feel.LastShakeAngle;
  }
  // ---------- hit marker: the shooter's own confirmation, as shooters do it ----------
  int markerSeen=-1;float markerAge=9;bool markerFriend;float landDip,fovPunch;
  /// <summary>Age (s) of the newest hit marker and whether it was a friend (for tests).</summary>
  public (float age,bool friend) HitMarker=>(markerAge,markerFriend);
  public const float HitMarkerSeconds=.22f,BaseFieldOfView=65;
  void DrawHitMarker(SessionState state,float w,float h) {
   if(Event.current.type==EventType.Layout){
    int newest=markerSeen;
    foreach(var e in state.Events??new FeelEvent[0]) {
     if(e.Id<=markerSeen)continue;newest=Mathf.Max(newest,e.Id);
     if(markerSeen>=0&&e.Actor==network.LocalSlot&&(e.Kind==FeelEventKind.Stagger||e.Kind==FeelEventKind.Shoved||e.Kind==FeelEventKind.AttackHit)){if(markerAge>.06f)session.Audio.Play("confirm",view.transform.position,markerFriend?.8f:1,e.Kind==FeelEventKind.Shoved?.85f:1);markerAge=0;markerFriend=e.Kind==FeelEventKind.Shoved;}
     // A hard landing of our own body dips the camera a little.
     if(markerSeen>=0&&e.Kind==FeelEventKind.Landing&&e.Actor==network.LocalSlot)landDip=Mathf.Max(landDip,.12f+.2f*e.Strength);
    }
    markerSeen=Mathf.Max(markerSeen<0?newest:markerSeen,newest);markerAge+=Time.unscaledDeltaTime;
   }
   if(markerAge>HitMarkerSeconds)return;
   float t=markerAge/HitMarkerSeconds,gap=7+5*t,len=8;var color=(markerFriend?new Color(1,.85f,.3f):new Color(1,.45f,.2f))*new Color(1,1,1,1-t*t);
   var old=GUI.matrix;var oldColor=GUI.color;GUI.color=color;var center=new Vector2(w/2,h/2);
   for(int k=0;k<4;k++){GUIUtility.RotateAroundPivot(45+90*k,center);GUI.DrawTexture(new Rect(center.x-1.5f,center.y-gap-len,3,len),Texture2D.whiteTexture);GUI.matrix=old;}
   GUI.color=oldColor;
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
    var layout=StartScreenLayout.For(w,h,true);float c=layout.Center;
    GUI.Box(layout.Title,"");
    GUI.Label(new Rect(w/2-210,c-125,420,45),"Animals vs Humans",title);
    GUI.Label(new Rect(w/2-190,c-65,380,30),"닉네임 (1~20자)",label);
    nickname=GUI.TextField(new Rect(w/2-160,c-25,320,35),nickname,20);
    if(string.IsNullOrWhiteSpace(nickname))GUI.Label(new Rect(w/2-210,c+15,420,30),"닉네임을 입력해주세요.",label);
    GUI.enabled=true;network.DrawStart(nickname,label,c,area=>displayMenu.Draw(area,display,label));return;
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
   if(state.Phase!=RoundPhase.Results)DrawHitMarker(state,w,h);
   // The tool drawn for this round, announced while everyone gets ready.
   var mine=state.Players[network.LocalSlot];
   if(state.Phase==RoundPhase.Preparation&&mine.Faction==Faction.Human)GUI.Label(new Rect(w/2-260,118,520,40),$"이번 도구: {Tools.Name(mine.Tool)}!",title);
   hitFeed.Draw(view,session.PlayerTransform,network.LocalSlot);
   // This round's character and what Q does, read aloud while everyone gets ready (and again when a human turns animal).
   var skill=Abilities.Of(mine.Ability);var tint=Abilities.Tint(mine.CharacterRarity);var gradeColor=new Color(tint.r,tint.g,tint.b);
   if(skill!=null&&session.ObserveActiveSettings().Current.Abilities) {
    if(shownCharacter!=mine.CharacterId||shownRound!=state.Round){shownCharacter=mine.CharacterId;shownRound=state.Round;introAge=0;}
    if(Event.current.type==EventType.Repaint)introAge+=Time.unscaledDeltaTime;
    if(state.Phase==RoundPhase.Preparation||introAge<4&&state.Phase==RoundPhase.Chase) {
     var old=GUI.color;GUI.color=gradeColor;GUI.Label(new Rect(w/2-320,mine.Faction==Faction.Human&&state.Phase==RoundPhase.Preparation?160:118,640,34),$"[{mine.CharacterRarity}] {mine.CharacterName} — Q {skill.Name}",label);GUI.color=old;
     GUI.Label(new Rect(w/2-320,mine.Faction==Faction.Human&&state.Phase==RoundPhase.Preparation?190:148,640,26),skill.Hint,new GUIStyle(label){fontSize=16});
    }
    GUI.Box(new Rect(w/2-280,h-126,560,34),"");
    bool ready=mine.AbilityCooldownRemaining<=0&&mine.AbilityActiveRemaining<=0&&(skill.Uses==0||mine.AbilityUsesRemaining>0)&&state.Phase==RoundPhase.Chase;
    var before=GUI.color;GUI.color=ready?gradeColor:new Color(.7f,.72f,.75f);GUI.Label(new Rect(w/2-275,h-125,550,32),Abilities.Status(mine),label);GUI.color=before;
   }
   GUI.Box(new Rect(w/2-280,h-90,560,70),"");
   GUI.Label(new Rect(w/2-275,h-85,550,35),$"{state.Players[network.LocalSlot].CharacterName} · {WeaponLabel(state.Players[network.LocalSlot],session.ObserveActiveSettings().Current.Magazine)} · {GameMaps.Name(state.Map)} · 라운드 {state.Round}",label);
   GUI.Label(new Rect(w/2-275,h-53,550,25),"WASD 이동 · 마우스 시점 · Space 점프 · 좌클릭 공격 · Q 능력 · R 재장전 · Esc 메뉴 · F1 디버그",new GUIStyle(label){fontSize=15});

   if(Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog")==null) GUI.Label(new Rect(15,110,450,35),"개발 블록아웃 · 보유 에셋 적용 전",new GUIStyle(label){fontSize=16,alignment=TextAnchor.MiddleLeft});
   network.DrawRoom(label);
   if(menu) {
    // Esc menu: the match keeps running underneath.
    GUI.Box(new Rect(w/2-230,h/2-(network.IsClient?45:70),460,(network.IsClient?45:70)+70+DisplaySettingsMenu.Height),"메뉴");
    if(!network.IsClient&&GUI.Button(new Rect(w/2-125,h/2-45,250,40),"테스트 설정 열기")){menu=false;SetDebugPanelOpen(true);return;}
    if(GUI.Button(new Rect(w/2-125,h/2+(network.IsClient?-20:5),250,40),"계속하기")){menu=false;SetCursor();}
    displayMenu.Draw(new Rect(w/2-215,h/2+60,430,DisplaySettingsMenu.Height),display,label);
    if(GUI.Button(new Rect(w-200,h-50,180,35),"방 나가기")){network.Leave();menu=false;return;}
   }
   if(!menu){
    if(debugOpen)settingsPanel.Draw(session.Session,()=>SetDebugPanelOpen(false),network.IsClient?1:0);
    else if(GUI.Button(new Rect(w-160,205,140,30),"디버그 · F1"))SetDebugPanelOpen(true);
   }
   if(!(network.IsClient||string.IsNullOrEmpty(session.Session.ObserveSettings().Error))){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;settingsPanel.DrawError(session.Session);if((network.IsClient||string.IsNullOrEmpty(session.Session.ObserveSettings().Error)))SetCursor();}

  }
  string shownCharacter;int shownRound=-1;float introAge=9;
  static string WeaponLabel(PlayerState player,int magazine) {
   if(player.Faction==Faction.Animal)return player.AttackGraceRemaining>0?$"공격 대기 {player.AttackGraceRemaining:F1}초":"근접 공격";
   var name=Tools.Name(player.Tool);
   if(player.ReloadRemaining>0)return $"{name} 재장전 중 ({player.ReloadRemaining:F1}초)";
   if(Tools.UsesAmmo(player.Tool))return $"{name} {player.Ammo} / {magazine} (예비 ∞)";
   return player.FireCooldownRemaining>0?$"{name} 충전 중":$"{name} 준비됨";
  }
  void OnDestroy(){RestoreLocalRenderers();if(ownsView&&view!=null)Destroy(view.gameObject);if(ownedSun!=null)Destroy(ownedSun.gameObject);if(rosterHud!=null)Destroy(rosterHud.gameObject);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
 }
}
