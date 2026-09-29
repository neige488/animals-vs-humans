using UnityEngine;
namespace AvH {
 [CreateAssetMenu(menuName="Animals vs Humans/Owned asset catalog")]
 public sealed class OwnedAssetCatalog : ScriptableObject {
  public GameObject Human, Animal, Village;
  public Vector3[] ShelterPoints;
  public string AnimalDisplayName = "동물 종류 입력 필요";
  public string Rarity = "일반";
  public Color RarityColor = Color.white;
  public string HumanSource, AnimalSource, VillageSource;
 }
}
