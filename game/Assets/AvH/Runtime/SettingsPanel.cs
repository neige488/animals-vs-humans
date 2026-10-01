using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
namespace AvH {
 // UI edits are copies; the world keeps stepping while this panel is visible.
 public sealed class SettingsPanel {
  Vector2 scroll; int tab;
  readonly string[] tabs={"라운드","이동","버블건"};
  readonly string[][] keys={
   new[]{"PreparationSeconds","RoundSeconds","ResultSeconds","InitialAnimals","InitialAttackGrace","TransformAttackGrace","FriendlyCollision","EnemyCollision"},
   new[]{"HumanSpeed","AnimalSpeed","HumanJump","AnimalJump"},
   new[]{"Magazine","ReloadSeconds","BubbleRadius","BubbleSpeed","BubbleRange","BubbleLifetime","FireInterval","PushForce","FriendlyPush"}};
  readonly string[][] names={
   new[]{"준비 시간 (초)","추격 시간 (초)","결과 표시 시간 (초)","최초 동물 수","최초 공격 유예 (초)","변신 공격 유예 (초)","아군 몸 충돌","진영 간 몸 충돌"},
   new[]{"인간 속도","동물 속도","인간 점프 높이","동물 점프 높이"},
   new[]{"탄창","재장전 (초)","버블 반지름","버블 속도","버블 사거리","버블 유지 (초)","발사 간격 (초)","밀치는 힘","버블 아군 밀치기"}};
  public static Rect PanelRect(float width,float height)=>new Rect(Mathf.Max(8,width-352),Mathf.Min(205,height*.29f),Mathf.Min(344,width-16),Mathf.Max(180,height-Mathf.Min(205,height*.29f)-100));
  string applyError;
  internal void SelectTab(int index){tab=Mathf.Clamp(index,0,tabs.Length-1);}
  GUIStyle heading,muted,row,valueLabel;
  void Styles() {
   if(heading!=null)return;
   heading=new GUIStyle(GUI.skin.label){fontSize=19,fontStyle=FontStyle.Bold};heading.normal.textColor=Color.white;
   row=new GUIStyle(GUI.skin.label){fontSize=14};row.normal.textColor=new Color(.9f,.95f,.98f);
   muted=new GUIStyle(row){fontSize=12,wordWrap=true};muted.normal.textColor=new Color(.7f,.81f,.86f);
   valueLabel=new GUIStyle(row){alignment=TextAnchor.MiddleRight};
  }
  static Vector3 Range(string key) {
   switch(key) {
    case "PreparationSeconds":return new Vector3(1,120,1);
    case "RoundSeconds":return new Vector3(1,3600,1);
    case "ResultSeconds":return new Vector3(1,30,1);
    case "InitialAnimals":return new Vector3(1,11,1);
    case "Magazine":return new Vector3(1,100,1);
    case "InitialAttackGrace":case "TransformAttackGrace":return new Vector3(0,30,.1f);
    case "HumanSpeed":case "AnimalSpeed":return new Vector3(.01f,20,.1f);
    case "HumanJump":case "AnimalJump":return new Vector3(.01f,5,.1f);
    case "BubbleRadius":return new Vector3(.01f,2,.01f);
    case "BubbleLifetime":return new Vector3(.01f,10,.1f);
    case "BubbleSpeed":case "BubbleRange":return new Vector3(.01f,100,.5f);
    default:return new Vector3(.01f,30,.1f);
   }
  }
  public void Draw(PlaytestSession session,Action close,int actorSlot=0) {
   Styles();var state=session.ObserveSettings();var area=PanelRect(Screen.width,Screen.height);
   var oldColor=GUI.color;GUI.color=new Color(.06f,.1f,.13f,.97f);GUI.DrawTexture(area,Texture2D.whiteTexture);GUI.color=oldColor;
   GUILayout.BeginArea(new Rect(area.x+14,area.y+12,area.width-28,area.height-24));
   GUILayout.BeginHorizontal();GUILayout.Label("플레이 디버그",heading);if(GUILayout.Button("접기",GUILayout.Width(52),GUILayout.Height(26))){close();GUILayout.EndHorizontal();GUILayout.EndArea();return;}GUILayout.EndHorizontal();
   if(actorSlot!=0){GUILayout.Label("호스트만 설정을 변경할 수 있습니다. · F1 접기",muted);GUILayout.EndArea();return;}
   GUILayout.Label("실시간 적용 · 설정 v"+state.Version+" · F1 접기",muted);
   if(state.Edit==null){session.BeginSettingsEdit(0);state=session.ObserveSettings();}
   GUILayout.Space(10);tab=GUILayout.Toolbar(tab,tabs,GUILayout.Height(28));GUILayout.Space(8);
   scroll=GUILayout.BeginScrollView(scroll);
   var values=state.Edit;bool changed=false;
   for(int i=0;i<keys[tab].Length;i++) {
    var key=keys[tab][i];var field=typeof(PlaytestValues).GetField(key);
    if(field.FieldType==typeof(bool)) {
     bool previous=(bool)field.GetValue(values);bool next=GUILayout.Toggle(previous,names[tab][i],GUILayout.Height(30));
     if(previous!=next){field.SetValue(values,next);changed=true;}
    } else {
     float previous=Convert.ToSingle(field.GetValue(values));var range=Range(key);
     GUILayout.BeginHorizontal();GUILayout.Label(names[tab][i],row);GUILayout.Label(previous.ToString(field.FieldType==typeof(int)?"0":"0.##",CultureInfo.InvariantCulture),valueLabel,GUILayout.Width(58));GUILayout.EndHorizontal();
     GUILayout.BeginHorizontal();
     float next=GUILayout.HorizontalSlider(previous,range.x,range.y,GUILayout.Height(20));
     if(GUILayout.Button("−",GUILayout.Width(26)))next=previous-range.z;
     if(GUILayout.Button("+",GUILayout.Width(26)))next=previous+range.z;
     GUILayout.EndHorizontal();
     if(next!=previous){next=Mathf.Clamp(Mathf.Round(next/range.z)*range.z,range.x,range.y);if(next!=previous){if(field.FieldType==typeof(int))field.SetValue(values,Mathf.RoundToInt(next));else field.SetValue(values,next);changed=true;}}
     GUILayout.Space(10);
    }
   }
   GUILayout.EndScrollView();
   if(changed){session.UpdateSettingsEdit(0,values);bool applied=session.ApplySettingsNow(0,false);applyError=applied?null:"적용되지 않았습니다. 값의 범위를 확인하거나 기본값으로 복원하세요.";}

   GUILayout.Space(8);GUILayout.Label("최초 동물 수·유예: 다음 탄생부터\n버블 속성: 새 발사부터 · 시간: 경과 유지",muted);
   GUILayout.Label(applyError??"변경은 이번 실행에 적용됩니다.",muted,GUILayout.Height(32));
   GUILayout.BeginHorizontal();
   if(GUILayout.Button("기본값",GUILayout.Height(28))){session.BeginSettingsEdit(0);session.RestoreSettingsDefaults(0);session.ApplySettingsNow(0,false);applyError=null;}
   if(GUILayout.Button("현재 설정 저장",GUILayout.Height(28)))session.SaveCurrentSettings(0);
   GUILayout.EndHorizontal();GUILayout.EndArea();
  }
  public void DrawError(PlaytestSession session) {
   var state=session.ObserveSettings();if(string.IsNullOrEmpty(state.Error))return;
   GUILayout.BeginArea(new Rect(Screen.width/2-280,Screen.height/2-90,560,180),GUI.skin.box);
   GUILayout.Label(state.Error);
   if(state.Failure==SettingsFailure.Save && GUILayout.Button("저장 재시도"))session.RetrySettingsSave(0);
   if(GUILayout.Button(state.Failure==SettingsFailure.Load?"확인":"닫기"))session.DismissSettingsError();
   GUILayout.EndArea();
  }
 }
}
