using System;
namespace AvH {
 [Serializable] public sealed class PlaytestValues {
  public float PreparationSeconds=20, RoundSeconds=180, InitialAttackGrace=3, TransformAttackGrace=1;
  public int InitialAnimals=2, Magazine=12;
  public float HumanSpeed=5, AnimalSpeed=5.6f, HumanJump=1.5f, AnimalJump=1.5f;
  public float ReloadSeconds=1.5f, BubbleRadius=.35f, BubbleSpeed=18, BubbleRange=25, BubbleLifetime=2, FireInterval=.3f, PushForce=8;
  public bool FriendlyCollision=true, EnemyCollision=true, FriendlyPush=true;
  public PlaytestValues Copy() => (PlaytestValues)MemberwiseClone();
 }
 public sealed class SettingsState {
  public PlaytestValues Current, Edit, Pending, Saved;
  public int Version;
  public string Error;
 }
 sealed class PlaytestSettings {
  public PlaytestValues Current=new PlaytestValues(), Edit, Pending, Saved;
  public int Version=1;
  public void BeginRound() { if(Pending==null)return; Current=Pending;Pending=null;Version++; }
  public SettingsState Observe()=>new SettingsState { Current=Current.Copy(),Edit=Edit?.Copy(),Pending=Pending?.Copy(),Saved=Saved?.Copy(),Version=Version };
 }
 public sealed partial class PlaytestSession {
  readonly PlaytestSettings settings=new PlaytestSettings();
  public SettingsState ObserveSettings()=>settings.Observe();
  public bool BeginSettingsEdit(int actorSlot) { if(actorSlot!=0)return false;settings.Edit=(settings.Pending??settings.Current).Copy();return true; }
  public bool UpdateSettingsEdit(int actorSlot,PlaytestValues values) { if(actorSlot!=0||settings.Edit==null||values==null)return false;settings.Edit=values.Copy();return true; }
  public bool ApplySettings(int actorSlot) { if(actorSlot!=0||settings.Edit==null)return false;settings.Pending=settings.Edit.Copy();settings.Edit=null;return true; }
 }
}
