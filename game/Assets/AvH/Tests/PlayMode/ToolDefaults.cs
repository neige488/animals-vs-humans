using NUnit.Framework;
namespace AvH.Tests {
 /// <summary>Existing shot tests assume the cork popper; the product default draws a random tool each round.
 /// Tool tests set PlaytestValues.Tool explicitly.
 /// Characters are handed out in roster order so a test knows who plays whom, and maps use the host settings without
 /// their per-map balance (MapBalanceTests and the balance harness turn it on).</summary>
 [SetUpFixture] public sealed class ToolDefaults {
  [OneTimeSetUp] public void PinCork(){PlaytestValues.DefaultTool=(int)ToolKind.Cork;PlaytestValues.DefaultRandomCharacters=false;GameMaps.PerMapBalance=false;}
  [OneTimeTearDown] public void Restore(){PlaytestValues.DefaultTool=Tools.Random;PlaytestValues.DefaultRandomCharacters=true;GameMaps.PerMapBalance=true;}
 }
}
