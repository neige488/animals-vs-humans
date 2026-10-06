using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
namespace AvH {
 public enum SettingsFailure { None, Load, Save }
 [Serializable] public sealed class PlaytestValues {
  public float PreparationSeconds=20, RoundSeconds=180, ResultSeconds=5, InitialAttackGrace=2, TransformAttackGrace=1;
  public int InitialAnimals=2, Magazine=12;
  public float HumanSpeed=5, AnimalSpeed=5.6f, HumanJump=1.5f, AnimalJump=1.5f;
  // Seconds from standing to full speed on the ground; 0 restores instant start/stop.
  public float InertiaSeconds=.12f;
  // Share of ground control kept in the air (1 = same as ground).
  public float AirControl=.45f;
  public float ReloadSeconds=1.5f, BubbleRadius=.35f, BubbleSpeed=18, BubbleRange=25, BubbleLifetime=2, FireInterval=.3f, PushForce=8;
  public float FoxSpeedMultiplier=1.1f, FoxJumpMultiplier=1.1f, FoxKnockbackMultiplier=1.15f;
  public float WolfSpeedMultiplier=1.15f, WolfJumpMultiplier=1.0f, WolfKnockbackMultiplier=1.0f;
  public float BearSpeedMultiplier=0.8f, BearJumpMultiplier=0.65f, BearKnockbackMultiplier=0.45f;
  public float BoarSpeedMultiplier=1.05f, BoarJumpMultiplier=0.7f, BoarKnockbackMultiplier=0.65f;
  public float RabbitSpeedMultiplier=1.05f, RabbitJumpMultiplier=1.55f, RabbitKnockbackMultiplier=1.4f;
  public float PenguinSpeedMultiplier=0.9f, PenguinJumpMultiplier=0.9f, PenguinKnockbackMultiplier=0.8f;
  public bool FriendlyCollision=true, EnemyCollision=true, FriendlyPush=true;
  public PlaytestValues Copy() => (PlaytestValues)MemberwiseClone();
 }
 public struct AnimalModifiers {
  public float Speed,Jump,Knockback;
  public AnimalModifiers(float speed,float jump,float knockback){Speed=speed;Jump=jump;Knockback=knockback;}
 }
 public static class AnimalBalance {
  public const int Count=6;
  static readonly string[] ids={"animal-fox","animal-wolf","animal-bear_grizzly","animal-boar","animal-rabbit_brown","animal-penguin"};
  static readonly string[] names={"여우","늑대","불곰","멧돼지","토끼","펭귄"};
  static readonly string[] prefixes={"Fox","Wolf","Bear","Boar","Rabbit","Penguin"};
  public static string Id(int i)=>ids[i];
  public static string Name(int i)=>names[i];
  public static string[] Keys(int i)=>new[]{prefixes[i]+"SpeedMultiplier",prefixes[i]+"JumpMultiplier",prefixes[i]+"KnockbackMultiplier"};
  public static AnimalModifiers For(PlaytestValues rules,PlayerState player) {
   if(player.Faction!=Faction.Animal)return new AnimalModifiers(1,1,1);
   switch(player.CharacterId) {
    case "animal-fox":return new AnimalModifiers(rules.FoxSpeedMultiplier,rules.FoxJumpMultiplier,rules.FoxKnockbackMultiplier);
    case "animal-wolf":return new AnimalModifiers(rules.WolfSpeedMultiplier,rules.WolfJumpMultiplier,rules.WolfKnockbackMultiplier);
    case "animal-bear_grizzly":return new AnimalModifiers(rules.BearSpeedMultiplier,rules.BearJumpMultiplier,rules.BearKnockbackMultiplier);
    case "animal-boar":return new AnimalModifiers(rules.BoarSpeedMultiplier,rules.BoarJumpMultiplier,rules.BoarKnockbackMultiplier);
    case "animal-rabbit_brown":return new AnimalModifiers(rules.RabbitSpeedMultiplier,rules.RabbitJumpMultiplier,rules.RabbitKnockbackMultiplier);
    case "animal-penguin":return new AnimalModifiers(rules.PenguinSpeedMultiplier,rules.PenguinJumpMultiplier,rules.PenguinKnockbackMultiplier);
    default:return new AnimalModifiers(1,1,1);
   }
  }
 }
 [Serializable] public sealed class SettingsState {
  public PlaytestValues Current, Edit, Pending, Saved;
  public int Version;
  public string Error;
  public SettingsFailure Failure;
  [NonSerialized] public Dictionary<string,string> Errors;
 }
 sealed class PlaytestSettings {
  public PlaytestValues Current=new PlaytestValues(), Edit, Pending, Saved;
  public int Version=1;
  string path, error; bool corrupt; PlaytestValues unsaved; SettingsFailure failure;
  public void Load(string file) {
   path=file;if(path==null||!File.Exists(path))return;
   try { using(var reader=File.OpenRead(path)) {
    var value=(PlaytestValues)new XmlSerializer(typeof(PlaytestValues)).Deserialize(reader);
    if(value==null||Validate(value).Count>0)throw new InvalidDataException();
    Current=value.Copy();Saved=value.Copy();
   }}catch(Exception ex) when(ex is InvalidDataException||ex is IOException||ex is UnauthorizedAccessException||ex is InvalidOperationException) {
    corrupt=true;failure=SettingsFailure.Load;error="저장된 설정을 불러오지 못했습니다. 원본을 보존하고 기본 설정으로 시작합니다.";
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
    Saved=value.Copy();unsaved=null;error=null;failure=SettingsFailure.None;return true;
   }catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is InvalidOperationException) {
    failure=SettingsFailure.Save;error="설정을 저장하지 못했습니다. 이번 실행에는 적용되지만 재실행하면 유지되지 않습니다.";return false;
   }
  }
  public bool Retry()=>unsaved!=null&&Save(unsaved);
  public void Dismiss(){error=null;failure=SettingsFailure.None;}

  public static Dictionary<string,string> Validate(PlaytestValues v) {
   var errors=new Dictionary<string,string>();
   foreach(var f in typeof(PlaytestValues).GetFields()) {
    if(f.FieldType==typeof(bool))continue;
    double value=Convert.ToDouble(f.GetValue(v));double min=.01,max=1000;
    if(f.Name.EndsWith("Multiplier",StringComparison.Ordinal)){min=f.Name.EndsWith("KnockbackMultiplier",StringComparison.Ordinal)?0:.05;max=3;}
    switch(f.Name) {
     case "InitialAnimals":min=1;max=11;break;
     case "Magazine":min=1;max=100;break;
     case "InitialAttackGrace":case "TransformAttackGrace":min=0;max=30;break;
     case "PreparationSeconds":min=1;max=120;break;
     case "RoundSeconds":min=1;max=3600;break;
     case "ResultSeconds":min=1;max=30;break;
     case "HumanSpeed":case "AnimalSpeed":max=20;break;
     case "InertiaSeconds":min=0;max=1;break;
     case "AirControl":min=.05;max=1;break;
     case "HumanJump":case "AnimalJump":max=5;break;
     case "BubbleRadius":max=2;break;
     case "ReloadSeconds":case "FireInterval":max=30;break;
     case "BubbleLifetime":max=10;break;
     case "BubbleSpeed":case "BubbleRange":max=100;break;
     case "PushForce":max=30;break;
    }
    // UI and stored float fields use the nearest representable float boundary.
    // Keep integer/double comparisons unchanged and reject even the adjacent outside float.
    double lower=f.FieldType==typeof(float)?(double)(float)min:min;
    double upper=f.FieldType==typeof(float)?(double)(float)max:max;
    if(double.IsNaN(value)||double.IsInfinity(value)||value<lower||value>upper)errors[f.Name]=$"{min}~{max} 범위의 값을 입력하세요.";
   }
   return errors;
  }
  public void BeginRound() { if(Pending==null)return; Current=Pending;Pending=null;Version++; }
  public SettingsState Observe()=>new SettingsState { Current=Current.Copy(),Edit=Edit?.Copy(),Pending=Pending?.Copy(),Saved=Saved?.Copy(),Version=Version,Error=error,Failure=failure,Errors=Edit==null?new Dictionary<string,string>():Validate(Edit) };
 }
 public sealed partial class PlaytestSession {
  readonly PlaytestSettings settings=new PlaytestSettings();
  public SettingsState ObserveSettings()=>settings.Observe();
  public bool BeginSettingsEdit(int actorSlot) { if(actorSlot!=0)return false;settings.Edit=(settings.Pending??settings.Current).Copy();return true; }
  public bool UpdateSettingsEdit(int actorSlot,PlaytestValues values) { if(actorSlot!=0||settings.Edit==null||values==null)return false;settings.Edit=values.Copy();return true; }
  public bool RestoreSettingsDefaults(int actorSlot) {if(actorSlot!=0||settings.Edit==null)return false;settings.Edit=new PlaytestValues();return true;}
  public bool RestoreAnimalDefaults(int actorSlot,int species) {
   if(actorSlot!=0||settings.Edit==null||species<0||species>=AnimalBalance.Count)return false;
   var defaults=new PlaytestValues();foreach(var key in AnimalBalance.Keys(species)){var field=typeof(PlaytestValues).GetField(key);field.SetValue(settings.Edit,field.GetValue(defaults));}return true;
  }
  public bool CancelSettingsEdit(int actorSlot) {if(actorSlot!=0)return false;settings.Edit=null;return true;}
  public bool RetrySettingsSave(int actorSlot)=>actorSlot==0&&settings.Retry();
  public void DismissSettingsError()=>settings.Dismiss();
  // Test-only live tuning. Existing births/projectiles retain their identity;
  // active phase time is recalculated from elapsed time, without resetting the round.
  public bool ApplySettingsNow(int actorSlot,bool persist=false) {
   if(actorSlot!=0||settings.Edit==null||PlaytestSettings.Validate(settings.Edit).Count>0)return false;
   var old=settings.Current;var value=settings.Edit.Copy();
   if(players.Length>0) {
    double duration=phase==RoundPhase.Preparation?value.PreparationSeconds:phase==RoundPhase.Chase?value.RoundSeconds:value.ResultSeconds;
    double elapsed=hostTime-phaseStartedAt;
    remaining=Math.Max(0,duration-elapsed);phaseDeadline=hostTime+remaining;
    foreach(var p in players) {
     p.Ammo=Math.Min(p.Ammo,value.Magazine);
     if(p.ReloadRemaining>0) {
      p.ReloadRemaining=Math.Max(0,value.ReloadSeconds-(old.ReloadSeconds-p.ReloadRemaining));
      if(p.ReloadRemaining==0)p.Ammo=value.Magazine;
     }
     if(p.Faction==Faction.Human&&p.FireCooldownRemaining>0)
      p.FireCooldownRemaining=Math.Max(0,value.FireInterval-(old.FireInterval-p.FireCooldownRemaining));
    }
   }
   settings.Current=value;settings.Pending=null;settings.Edit=null;settings.Version++;if(persist)settings.Save(value);return true;
  }
  public bool SaveCurrentSettings(int actorSlot)=>actorSlot==0&&settings.Save(settings.Current);
  public bool ApplySettings(int actorSlot) { if(actorSlot!=0||settings.Edit==null||PlaytestSettings.Validate(settings.Edit).Count>0)return false;settings.Pending=settings.Edit.Copy();settings.Edit=null;settings.Save(settings.Pending);return true; }
 }
}
