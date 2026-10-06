using UnityEngine;
namespace AvH {
 [System.Serializable] public sealed class OwnedCharacter {
  public string Id,DisplayName,Rarity="일반",Source;
  public GameObject Prefab;
  public Texture2D Portrait;
  public CharacterDefinition Definition()=>new CharacterDefinition(Id,DisplayName,Rarity);
 }
 [CreateAssetMenu(menuName="Animals vs Humans/Owned asset catalog")]
 public sealed class OwnedAssetCatalog : ScriptableObject {
  public GameObject Human, Animal, Village;
  public OwnedCharacter[] Humans=new OwnedCharacter[0],Animals=new OwnedCharacter[0];
  public GameObject Character(string id,Faction faction) {
   var roster=faction==Faction.Human?Humans:Animals;
   if(roster!=null)foreach(var entry in roster)if(entry.Id==id&&entry.Prefab!=null)return entry.Prefab;
   if(string.IsNullOrEmpty(id)||id=="human-default"||id=="animal-default")return faction==Faction.Human?Human:Animal;
   throw new System.InvalidOperationException("캐릭터 에셋이 없습니다: "+id);
  }
  public Vector3[] ShelterPoints;
  public string AnimalDisplayName = "동물 종류 입력 필요";
  public string Rarity = "일반";
  public Color RarityColor = Color.white;
  public string HumanSource, AnimalSource, VillageSource;
  /// <summary>Owned Polyperfect sounds, referenced by clip name from <see cref="AudioCatalog"/>.</summary>
  public AudioClip[] Sounds=new AudioClip[0];
  public AudioClip Sound(string name){if(Sounds!=null)foreach(var clip in Sounds)if(clip!=null&&clip.name==name)return clip;return null;}
 }
}
