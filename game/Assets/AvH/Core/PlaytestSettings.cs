using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
namespace AvH {
 [Serializable] public sealed class PlaytestValues {
  public float PreparationSeconds=20, RoundSeconds=180, InitialAttackGrace=2, TransformAttackGrace=1;
  public int InitialAnimals=2, Magazine=12;
  public float HumanSpeed=5, AnimalSpeed=5.6f, HumanJump=1.5f, AnimalJump=1.5f;
  public float ReloadSeconds=1.5f, BubbleRadius=.35f, BubbleSpeed=18, BubbleRange=25, BubbleLifetime=2, FireInterval=.3f, PushForce=8;
  public bool FriendlyCollision=true, EnemyCollision=true, FriendlyPush=true;
  public PlaytestValues Copy() => (PlaytestValues)MemberwiseClone();
 }
 [Serializable] public sealed class SettingsState {
  public PlaytestValues Current, Edit, Pending, Saved;
  public int Version;
  public string Error;
  public Dictionary<string,string> Errors;
 }
 sealed class PlaytestSettings {
  public PlaytestValues Current=new PlaytestValues(), Edit, Pending, Saved;
  public int Version=1;
  string path, error; bool corrupt; PlaytestValues unsaved;
  public void Load(string file) {
   path=file;if(path==null||!File.Exists(path))return;
   try { using(var reader=File.OpenRead(path)) {
    var value=(PlaytestValues)new XmlSerializer(typeof(PlaytestValues)).Deserialize(reader);
    if(value==null||Validate(value).Count>0)throw new InvalidDataException();
    Current=value.Copy();Saved=value.Copy();
   }}catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is InvalidOperationException) {
    corrupt=true;error="저장된 설정을 불러오지 못했습니다. 원본을 보존하고 기본 설정으로 시작합니다.";
   }
  }
  public bool Save(PlaytestValues value) {
   unsaved=value.Copy();if(path==null)return true;
   try {
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
    using(var stream=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None)) {
     new XmlSerializer(typeof(PlaytestValues)).Serialize(stream,value);stream.Flush(true);
    }
    if(corrupt&&File.Exists(path)){File.Copy(path,path+".corrupt-"+Guid.NewGuid().ToString("N"));corrupt=false;}
    if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
    Saved=value.Copy();unsaved=null;error=null;return true;
   }catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is InvalidOperationException) {
    error="설정을 저장하지 못했습니다. 이번 실행에는 적용되지만 재실행하면 유지되지 않습니다.";return false;
   }
  }
  public bool Retry()=>unsaved==null||Save(unsaved);
  public void Dismiss(){error=null;}

  public static Dictionary<string,string> Validate(PlaytestValues v) {
   var errors=new Dictionary<string,string>();
   foreach(var f in typeof(PlaytestValues).GetFields()) {
    if(f.FieldType==typeof(bool))continue;
    double value=Convert.ToDouble(f.GetValue(v));double min=.01,max=1000;
    switch(f.Name) {
     case "InitialAnimals":min=1;max=11;break;
     case "Magazine":min=1;max=100;break;
     case "InitialAttackGrace":case "TransformAttackGrace":min=0;max=30;break;
     case "PreparationSeconds":min=1;max=120;break;
     case "RoundSeconds":min=1;max=3600;break;
     case "HumanSpeed":case "AnimalSpeed":max=20;break;
     case "HumanJump":case "AnimalJump":max=5;break;
     case "BubbleRadius":max=2;break;
     case "ReloadSeconds":case "FireInterval":max=30;break;
     case "BubbleLifetime":max=10;break;
     case "BubbleSpeed":case "BubbleRange":max=100;break;
     case "PushForce":max=30;break;
    }
    if(double.IsNaN(value)||double.IsInfinity(value)||value<min||value>max)errors[f.Name]=$"{min}~{max} 범위의 값을 입력하세요.";
   }
   return errors;
  }
  public void BeginRound() { if(Pending==null)return; Current=Pending;Pending=null;Version++; }
  public SettingsState Observe()=>new SettingsState { Current=Current.Copy(),Edit=Edit?.Copy(),Pending=Pending?.Copy(),Saved=Saved?.Copy(),Version=Version,Error=error,Errors=Edit==null?new Dictionary<string,string>():Validate(Edit) };
 }
 public sealed partial class PlaytestSession {
  readonly PlaytestSettings settings=new PlaytestSettings();
  public SettingsState ObserveSettings()=>settings.Observe();
  public bool BeginSettingsEdit(int actorSlot) { if(actorSlot!=0)return false;settings.Edit=(settings.Pending??settings.Current).Copy();return true; }
  public bool UpdateSettingsEdit(int actorSlot,PlaytestValues values) { if(actorSlot!=0||settings.Edit==null||values==null)return false;settings.Edit=values.Copy();return true; }
  public bool RestoreSettingsDefaults(int actorSlot) {if(actorSlot!=0||settings.Edit==null)return false;settings.Edit=new PlaytestValues();return true;}
  public bool CancelSettingsEdit(int actorSlot) {if(actorSlot!=0)return false;settings.Edit=null;return true;}
  public bool RetrySettingsSave(int actorSlot)=>actorSlot==0&&settings.Retry();
  public void DismissSettingsError()=>settings.Dismiss();
  public bool ApplySettings(int actorSlot) { if(actorSlot!=0||settings.Edit==null||PlaytestSettings.Validate(settings.Edit).Count>0)return false;settings.Pending=settings.Edit.Copy();settings.Edit=null;settings.Save(settings.Pending);return true; }
 }
}
