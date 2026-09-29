using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
namespace AvH {
 // UI edits are copies; the world keeps stepping while this panel is visible.
 public sealed class SettingsPanel {
  readonly Dictionary<string,string> text=new Dictionary<string,string>();
  Vector2 scroll; int tab;
  readonly string[] tabs={"라운드","이동","버블건"};
  readonly string[][] keys={
   new[]{"PreparationSeconds","RoundSeconds","InitialAnimals","InitialAttackGrace","TransformAttackGrace","FriendlyCollision","EnemyCollision"},
   new[]{"HumanSpeed","AnimalSpeed","HumanJump","AnimalJump"},
   new[]{"Magazine","ReloadSeconds","BubbleRadius","BubbleSpeed","BubbleRange","BubbleLifetime","FireInterval","PushForce","FriendlyPush"}};
  readonly string[][] names={
   new[]{"준비 시간 (초)","추격 시간 (초)","최초 동물 수","최초 공격 유예 (초)","변신 공격 유예 (초)","아군 몸 충돌","진영 간 몸 충돌"},
   new[]{"인간 속도","동물 속도","인간 점프 높이","동물 점프 높이"},
   new[]{"탄창","재장전 (초)","버블 반지름","버블 속도","버블 사거리","버블 유지 (초)","발사 간격 (초)","밀치는 힘","버블 아군 밀치기"}};
  public void Draw(PlaytestSession session,Action close,int actorSlot=0) {
   var state=session.ObserveSettings();
   GUILayout.BeginArea(new Rect(Screen.width/2-280,110,560,Mathf.Max(200,Screen.height-220)),GUI.skin.box);
   GUILayout.Label("테스트 설정 · 라운드는 계속됩니다");
   GUILayout.Label("현재 버전 "+state.Version+(state.Pending!=null?" · 다음 라운드 적용 예정":""));
   if(actorSlot!=0){GUILayout.Label("호스트만 설정을 변경할 수 있습니다.");if(GUILayout.Button("닫기"))close();GUILayout.EndArea();return;}
   if(state.Edit==null) {session.BeginSettingsEdit(actorSlot);text.Clear();state=session.ObserveSettings();}
   tab=GUILayout.Toolbar(tab,tabs);
   scroll=GUILayout.BeginScrollView(scroll);
   var values=state.Edit;bool parseValid=true;
   for(int i=0;i<keys[tab].Length;i++) {
    var key=keys[tab][i];var field=typeof(PlaytestValues).GetField(key);
    GUILayout.BeginHorizontal();GUILayout.Label(names[tab][i],GUILayout.Width(230));
    if(field.FieldType==typeof(bool))field.SetValue(values,GUILayout.Toggle((bool)field.GetValue(values),"켜기"));
    else {if(!text.ContainsKey(key))text[key]=Convert.ToString(field.GetValue(values),CultureInfo.InvariantCulture);text[key]=GUILayout.TextField(text[key]);}
    GUILayout.EndHorizontal();
    if(field.FieldType!=typeof(bool)) {
     float number;int whole;
     bool valid=field.FieldType==typeof(int)?int.TryParse(text[key],out whole):float.TryParse(text[key],NumberStyles.Float,CultureInfo.InvariantCulture,out number);
     if(!valid)GUILayout.Label("⚠ "+names[tab][i]+"에 숫자를 입력하세요.");
    }
    if(state.Errors.ContainsKey(key))GUILayout.Label("⚠ "+state.Errors[key]);
   }
   // Parse every tab, including any invalid text in a hidden tab.
   foreach(var pair in text) {
    var field=typeof(PlaytestValues).GetField(pair.Key);
    if(field.FieldType==typeof(int)){int v;if(int.TryParse(pair.Value,out v))field.SetValue(values,v);else parseValid=false;}
    else {float v;if(float.TryParse(pair.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out v))field.SetValue(values,v);else parseValid=false;}
   }
   session.UpdateSettingsEdit(actorSlot,values);
   if(!parseValid)GUILayout.Label("숫자가 아닌 입력이 있습니다. 각 탭의 값을 확인하세요.");
   GUILayout.EndScrollView();
   if(GUILayout.Button("기본값으로 복원")){session.RestoreSettingsDefaults(actorSlot);text.Clear();}
   GUI.enabled=parseValid&&session.ObserveSettings().Errors.Count==0;
   if(GUILayout.Button("다음 라운드에 적용")){if(session.ApplySettings(actorSlot)){text.Clear();close();}}
   GUI.enabled=true;
   if(GUILayout.Button("취소")){session.CancelSettingsEdit(actorSlot);text.Clear();close();}
   GUILayout.EndArea();
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
