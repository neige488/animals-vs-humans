using System;
namespace AvH {
 public sealed class CharacterDefinition {
  public string Id {get;}
  public string Name {get;}
  public string Rarity {get;}
  public CharacterDefinition(string id,string name,string rarity="일반") {
   if(string.IsNullOrWhiteSpace(id)||id.Length>64||string.IsNullOrWhiteSpace(name)||name.Length>40||string.IsNullOrWhiteSpace(rarity)||rarity.Length>20)throw new ArgumentException("캐릭터 정의가 올바르지 않습니다.");
   Id=id;Name=name;Rarity=rarity;
  }
 }
}
