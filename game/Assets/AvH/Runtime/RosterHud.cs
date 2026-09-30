using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace AvH {
 // Slot identity survives role changes and interrupted transitions. This view never alters match rules.
 public sealed class RosterHud : MonoBehaviour {
  sealed class Face {
   public RectTransform Root;public Image Border;public RawImage Portrait,Previous;public Text Fallback;public GameObject Local;
   public string Character;public Faction Faction;public Vector2 From,To;public float Started=-10;public bool Changed;
  }
  const float Step=46,Gap=38,Duration=.3f;
  static readonly Color Human=new Color(.38f,.82f,1),Animal=new Color(1,.65f,.35f),Ink=new Color(.055f,.085f,.105f,.94f);
  readonly Dictionary<int,Face> faces=new Dictionary<int,Face>();readonly Dictionary<string,Texture2D> portraits=new Dictionary<string,Texture2D>();
  RectTransform rail,divider;Text left,right,timer,birth;Font font;Camera overlay,gameCamera;int previousMask;float dividerFrom,dividerTo,dividerStart=-10;int round=-1;
  public bool AnimateTransitions=true;
  public void Initialize(Camera camera) {
   gameObject.layer=5;
   font=Font.CreateDynamicFontFromOSFont(new[]{"Apple SD Gothic Neo","Malgun Gothic","Arial"},24);
   gameCamera=camera;previousMask=camera.cullingMask;camera.cullingMask&=~(1<<5);overlay=new GameObject("Roster overlay camera").AddComponent<Camera>();overlay.clearFlags=CameraClearFlags.Depth;overlay.cullingMask=1<<5;overlay.depth=camera.depth+1;overlay.nearClipPlane=.1f;overlay.farClipPlane=2;overlay.orthographic=true;overlay.orthographicSize=1;overlay.allowHDR=false;overlay.allowMSAA=false;
   var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=overlay;canvas.planeDistance=1;canvas.sortingOrder=100;
   var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
   rail=Rect("Roster",transform,new Vector2(708,64));rail.anchorMin=rail.anchorMax=new Vector2(.5f,1);rail.pivot=new Vector2(.5f,1);rail.anchoredPosition=new Vector2(0,-14);
   var backing=rail.gameObject.AddComponent<Image>();backing.color=Ink;backing.raycastTarget=false;
   left=Label("Human count",rail,new Vector2(-326,0),new Vector2(40,44),28,Human);right=Label("Animal count",rail,new Vector2(326,0),new Vector2(40,44),28,Animal);
   divider=Rect("VS",rail,new Vector2(34,40));var vs=divider.gameObject.AddComponent<Text>();vs.font=font;vs.fontSize=17;vs.fontStyle=FontStyle.Bold;vs.alignment=TextAnchor.MiddleCenter;vs.text="VS";vs.color=Color.white;vs.raycastTarget=false;
   var clock=Rect("Clock backdrop",rail,new Vector2(206,30));clock.anchoredPosition=new Vector2(0,-50);var clockFill=clock.gameObject.AddComponent<Image>();clockFill.color=Ink;clockFill.raycastTarget=false;timer=Label("Round timer",clock,Vector2.zero,new Vector2(202,28),20,Color.white);
   birth=Label("Animal births",rail,new Vector2(0,-118),new Vector2(650,84),18,Color.white);birth.horizontalOverflow=HorizontalWrapMode.Wrap;birth.gameObject.AddComponent<Shadow>().effectColor=new Color(0,0,0,.9f);
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");if(catalog!=null)foreach(var entry in catalog.Humans.Concat(catalog.Animals))portraits[entry.Id]=entry.Portrait;
  }
  public void Present(SessionState state,int localSlot) {
   if(state==null){gameObject.SetActive(false);round=-1;return;}gameObject.SetActive(true);
   int humans=state.Players.Count(p=>p.Faction==Faction.Human);left.text=humans.ToString();right.text=(state.Players.Length-humans).ToString();
   int seconds=Mathf.Max(0,Mathf.CeilToInt((float)state.SecondsRemaining));timer.text=$"{(state.Phase==RoundPhase.Preparation?"준비":state.Phase==RoundPhase.Results?"다음 라운드":"추격")}  {seconds/60:00}:{seconds%60:00}";
   if(state.Phase!=RoundPhase.Results&&state.BirthSecondsRemaining>0&&state.Births!=null&&state.Births.Length>0) {
    var notices=state.Births;birth.text=notices.Sum(b=>b.Count)==1?$"[{notices[0].Rarity}] {notices[0].Kind} 탄생!":"새로운 동물들이 탄생했습니다!\n"+string.Join(" · ",notices.Select(b=>$"[{b.Rarity}] {b.Kind} ×{b.Count}"));
   }else birth.text="";
   bool reset=round!=state.Round;round=state.Round;float now=Time.unscaledTime;float boundary=-276+humans*Step;
   if(reset){dividerFrom=dividerTo=boundary;dividerStart=now-Duration;}
   else if(!Mathf.Approximately(dividerTo,boundary)){dividerFrom=divider.anchoredPosition.x;dividerTo=boundary;dividerStart=now;}
   divider.anchoredPosition=new Vector2(Mathf.Lerp(dividerFrom,dividerTo,Ease(now-dividerStart)),0);
   var ordered=state.Players.OrderBy(p=>p.Faction).ThenBy(p=>p.Slot).ToArray();
   for(int rank=0;rank<ordered.Length;rank++) {
    var player=ordered[rank];Face face;if(!faces.TryGetValue(player.Slot,out face)){face=CreateFace(player.Slot);faces.Add(player.Slot,face);}
    var destination=new Vector2(-272+rank*Step+(rank>=humans?Gap:0),0);
    bool identity=face.Character!=player.CharacterId||face.Faction!=player.Faction;
    if(reset||face.Character==null){face.From=face.To=destination;face.Started=now-Duration;face.Changed=false;}
    else if(identity||face.To!=destination){face.From=face.Root.anchoredPosition;face.To=destination;face.Started=now;face.Changed=identity;}
    if(identity){face.Previous.texture=face.Portrait.texture;Texture2D texture;portraits.TryGetValue(player.CharacterId??"",out texture);face.Portrait.texture=texture;face.Character=player.CharacterId;face.Faction=player.Faction;face.Fallback.text=texture==null?(player.Faction==Faction.Human?"H":"A"):"";}
    float t=AnimateTransitions?Mathf.Clamp01((now-face.Started)/Duration):1;
    face.Root.anchoredPosition=Vector2.Lerp(face.From,face.To,Ease(now-face.Started))+Vector2.up*(face.Changed?Mathf.Sin(t*Mathf.PI)*5:0);
    face.Root.localScale=Vector3.one*(1+(face.Changed?Mathf.Sin(t*Mathf.PI)*.07f:0));face.Border.color=player.Faction==Faction.Human?Human:Animal;
    face.Previous.color=new Color(1,1,1,face.Changed?1-t:0);face.Portrait.color=new Color(1,1,1,face.Changed?t:1);face.Local.SetActive(player.Slot==localSlot);
    if(identity||reset)face.Root.gameObject.name=$"Portrait {player.Slot}: {player.CharacterName} [{player.CharacterRarity}]";
   }
   foreach(var entry in faces)entry.Value.Root.gameObject.SetActive(state.Players.Any(p=>p.Slot==entry.Key));
  }
  float Ease(float elapsed){float t=AnimateTransitions?Mathf.Clamp01(elapsed/Duration):1;return 1-Mathf.Pow(1-t,3);}
  Face CreateFace(int slot) {
   var root=Rect("Portrait "+slot,rail,new Vector2(42,46));var border=root.gameObject.AddComponent<Image>();border.raycastTarget=false;
   var fill=Rect("Face",root,new Vector2(40,44));var background=fill.gameObject.AddComponent<Image>();background.color=new Color(.88f,.9f,.87f);background.raycastTarget=false;
   var before=Rect("Previous portrait",fill,new Vector2(40,44)).gameObject.AddComponent<RawImage>();before.raycastTarget=false;
   var portrait=Rect("Current portrait",fill,new Vector2(40,44)).gameObject.AddComponent<RawImage>();portrait.raycastTarget=false;
   var fallback=Label("Missing portrait",fill,Vector2.zero,new Vector2(40,44),20,Ink);
   var local=Rect("You",root,new Vector2(18,3));local.anchoredPosition=new Vector2(0,-22);var marker=local.gameObject.AddComponent<Image>();marker.color=Color.white;marker.raycastTarget=false;
   return new Face{Root=root,Border=border,Portrait=portrait,Previous=before,Fallback=fallback,Local=local.gameObject};
  }
  static RectTransform Rect(string name,Transform parent,Vector2 size) {
   var go=new GameObject(name,typeof(RectTransform));go.layer=5;var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=size;return rect;
  }
  Text Label(string name,Transform parent,Vector2 position,Vector2 size,int fontSize,Color color) {
   var rect=Rect(name,parent,size);rect.anchoredPosition=position;var text=rect.gameObject.AddComponent<Text>();text.font=font;text.fontSize=fontSize;text.fontStyle=FontStyle.Bold;text.color=color;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
  }
  public void SetCaptureTarget(RenderTexture target){overlay.targetTexture=target;}
  public void RenderOverlay(){overlay.Render();}
  void OnDestroy(){if(gameCamera!=null)gameCamera.cullingMask=previousMask;if(overlay!=null)Destroy(overlay.gameObject);if(font!=null)Destroy(font);}
 }
}
