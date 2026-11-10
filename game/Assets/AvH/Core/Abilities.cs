using System;
using System.Collections.Generic;
using System.Linq;
namespace AvH {
 /// <summary>One special ability per character. Animals first, then humans.</summary>
 public enum AbilityKind { None,
  Disguise, DoubleJump, BellySlide, CatSpring, Flap, Acorn, Howl, Charge, Leap, Headbutt, Bite, Roar, Dive, Slam, Stampede, Hunt,
  Kickboard, Umbrella, Helmet, Banana, Scarecrow, SpringPad, Hook, Whistle, Lasso, BigBubble, WaterCannon, Smoke, IceRay, ZeroG, Balloon, Vaccine }
 public sealed class AbilityDefinition {
  public AbilityKind Kind;public string CharacterId,Name,Rarity,Hint;
  /// <summary>Seconds before the next use, and how long the effect on its user lasts (0 = instant).</summary>
  public float Cooldown,Duration;
  /// <summary>Uses per round; 0 = limited only by the cooldown.</summary>
  public int Uses;
 }
 /// <summary>
 /// The ability table. A higher grade is showier with a longer wait or fewer uses, not stronger: every grade is tuned to
 /// the same strength. Grades also set how often a character is drawn (<see cref="Odds"/>).
 /// </summary>
 public static class Abilities {
  public const string Common="일반",Rare="레어",Epic="에픽",Legend="전설";
  public static readonly string[] Grades={Common,Rare,Epic,Legend};
  /// <summary>Draw weight of each grade, in <see cref="Grades"/> order.</summary>
  public static readonly int[] Odds={50,30,15,5};
  /// <summary>The HUD line for a player's ability: name, grade and whether Q does anything right now.</summary>
  public static string Status(PlayerState p) {
   var a=p==null?null:Of(p.Ability);if(a==null)return "";
   string state=p.AbilityActiveRemaining>0?$"사용 중 {p.AbilityActiveRemaining:F1}초":a.Uses>0&&p.AbilityUsesRemaining<=0?"다 썼음":p.AbilityCooldownRemaining>0?$"{p.AbilityCooldownRemaining:F0}초 뒤":"준비됨";
   return $"Q {a.Name} [{a.Rarity}] · {state}"+(a.Uses>0&&p.AbilityUsesRemaining>0?$" · 남은 {p.AbilityUsesRemaining}번":"");
  }
  /// <summary>Grade colours: plain, blue, purple, gold.</summary>
  public static (float r,float g,float b) Tint(string rarity){switch(Grade(rarity)){case 1:return (.45f,.75f,1);case 2:return (.8f,.55f,1);case 3:return (1,.8f,.25f);default:return (.92f,.95f,1);}}
  static AbilityDefinition A(AbilityKind kind,string id,string name,string rarity,float cooldown,float duration,int uses,string hint)=>new AbilityDefinition{Kind=kind,CharacterId=id,Name=name,Rarity=rarity,Cooldown=cooldown,Duration=duration,Uses=uses,Hint=hint};
  static readonly AbilityDefinition[] all={
   A(AbilityKind.Disguise,"animal-fox","변장",Common,14,5,0,"상자로 변해 사람 눈을 속인다. 움직이면 풀린다."),
   A(AbilityKind.DoubleJump,"animal-rabbit_brown","이단 점프",Common,6,0,0,"공중에서 한 번 더 뛴다."),
   A(AbilityKind.BellySlide,"animal-penguin","배 미끄럼",Common,12,3,0,"배로 미끄러져 빨라진다. 방향 전환은 둔하다."),
   A(AbilityKind.CatSpring,"animal-cat_orange","용수철 다리",Common,12,4,0,"잠깐 훨씬 높이 뛴다."),
   A(AbilityKind.Flap,"animal-hen","날갯짓",Common,10,3,0,"퍼덕여 떠오른 뒤 천천히 내려온다."),
   A(AbilityKind.Acorn,"animal-squirrel","도토리 던지기",Common,12,0,0,"맞은 사람은 화면이 흔들린다."),
   A(AbilityKind.Howl,"animal-wolf","울음",Rare,22,6,0,"가장 가까운 사람이 있는 쪽을 알아낸다."),
   A(AbilityKind.Charge,"animal-boar","돌진",Rare,16,1.2f,0,"곧장 달려 사람을 밀친다."),
   A(AbilityKind.Leap,"animal-deer","도약",Rare,14,0,0,"멀리 뛰어 넘는다."),
   A(AbilityKind.Headbutt,"animal-goat","박치기",Rare,15,0,0,"앞의 사람과 상자를 세게 밀친다."),
   A(AbilityKind.Bite,"animal-crocodile","물기",Rare,20,8,0,"다음 공격이 맞으면 주변 사람이 느려진다."),
   A(AbilityKind.Roar,"animal-bear_grizzly","포효",Epic,30,0,0,"가까운 사람이 잠깐 멈춘다."),
   A(AbilityKind.Dive,"animal-eagle","급강하",Epic,28,4,0,"날아올라 내리꽂으며 주변을 밀친다."),
   A(AbilityKind.Slam,"animal-gorilla_silver","땅 내려치기",Epic,28,0,0,"주변 사람과 상자를 밀어낸다."),
   A(AbilityKind.Stampede,"animal-rhino","대돌진",Epic,32,2,0,"아무것도 못 막는 돌진."),
   A(AbilityKind.Hunt,"animal-tiger","사냥 본능",Legend,8,5,2,"잠깐 빨라지고 공격에 준비 시간이 없다."),
   A(AbilityKind.Kickboard,"human-man_casual","킥보드",Common,14,3,0,"빠르게 직진한다. 벽에 부딪히면 넘어진다."),
   A(AbilityKind.Umbrella,"human-woman_casual","우산",Common,12,4,0,"폴짝 뛰어 천천히 내려온다."),
   A(AbilityKind.Helmet,"human-man_construction_worker","안전모",Common,18,10,0,"다음 밀침이나 멈춤을 한 번 막는다."),
   A(AbilityKind.Banana,"human-man_chef","바나나 껍질",Common,3,0,3,"밟은 동물이 미끄러진다."),
   A(AbilityKind.Scarecrow,"human-man_farm","허수아비",Common,18,0,0,"동물을 속이는 미끼를 세운다."),
   A(AbilityKind.SpringPad,"human-woman_mechanic","스프링 발판",Common,4,0,2,"밟으면 누구든 튕겨 오른다."),
   A(AbilityKind.Hook,"human-woman_explorer","갈고리 줄",Rare,18,0,0,"보는 쪽으로 날아간다."),
   A(AbilityKind.Whistle,"human-woman_police","호루라기",Rare,20,0,0,"가까운 동물이 느려진다."),
   A(AbilityKind.Lasso,"human-man_cowboy","올가미",Rare,20,0,0,"동물 한 마리를 묶는다."),
   A(AbilityKind.BigBubble,"human-woman_soldier","대형 비눗방울",Rare,22,0,0,"동물 한 마리를 띄운다."),
   A(AbilityKind.WaterCannon,"human-man_fire","물대포",Epic,28,2,0,"물줄기로 동물을 밀어낸다."),
   A(AbilityKind.Smoke,"human-man_ninja","연막",Epic,28,0,0,"연기 속에 숨는다."),
   A(AbilityKind.IceRay,"human-woman_scientist","얼음 광선",Epic,30,0,0,"동물 한 마리를 얼린다."),
   A(AbilityKind.ZeroG,"human-man_astronaut","무중력",Epic,30,0,0,"주변이 둥실 떠오른다."),
   A(AbilityKind.Balloon,"human-man_clown","풍선",Epic,28,5,0,"풍선을 타고 떠올라 흘러간다. 동물이 닿으면 터진다."),
   A(AbilityKind.Vaccine,"human-woman_doctor","백신",Legend,0,8,1,"감염 공격을 한 번 막는다."),
  };
  public static IReadOnlyList<AbilityDefinition> All=>all;
  public static AbilityDefinition For(string characterId){foreach(var a in all)if(a.CharacterId==characterId)return a;return null;}
  public static AbilityDefinition Of(AbilityKind kind){foreach(var a in all)if(a.Kind==kind)return a;return null;}
  /// <summary>Grade of a character: its ability's grade, 일반 for a character without one.</summary>
  public static string Rarity(string characterId)=>For(characterId)?.Rarity??Common;
  public static int Grade(string rarity){int i=Array.IndexOf(Grades,rarity);return i<0?0:i;}
  /// <summary>Human-readable name of a hold effect or ability for the HUD.</summary>
  public static string Name(AbilityKind kind)=>Of(kind)?.Name??"";
  // Effect sizes shared by rules, bots and presentation.
  public const float RoarRadius=6,SlamRadius=5,WhistleRadius=5,SmokeRadius=4,ZeroGRadius=5,BiteRadius=5,DiveRadius=4.5f;
  public const float AcornRange=14,LassoRange=9,BubbleRange=8,IceRange=10,CannonRange=7,HeadbuttRange=3;
  /// <summary>An animal bot that is not this close cannot make out a human inside smoke.</summary>
  public const float SmokeSightDistance=2.5f;
  public const float BalloonRiseSeconds=2.2f;
 }
 [Serializable] public sealed class GadgetState {
  public int Id,Owner;public AbilityKind Kind;public WorldPosition Position;public double Remaining;
  public GadgetState Copy()=>(GadgetState)MemberwiseClone();
 }
 /// <summary>A shove the rules decided; the physics adapter adds it to the body.</summary>
 public struct Impulse {
  public int Slot;public float X,Y,Z;
  /// <summary>When set, the body's own vertical speed becomes this (a jump, a bounce) instead of adding to its knockback.</summary>
  public bool SetVertical;public float Vertical;
 }
 /// <summary>How an ability or effect bends gravity for one body.</summary>
 public struct AirRule { public float Gravity,FallCap,Rise; }
}
