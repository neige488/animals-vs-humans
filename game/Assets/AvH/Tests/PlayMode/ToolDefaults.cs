using NUnit.Framework;
namespace AvH.Tests {
 /// <summary>Existing shot tests assume the cork popper; the product default draws a random tool each round.
 /// Tool tests set PlaytestValues.Tool explicitly.</summary>
 [SetUpFixture] public sealed class ToolDefaults {
  [OneTimeSetUp] public void PinCork(){PlaytestValues.DefaultTool=(int)ToolKind.Cork;}
  [OneTimeTearDown] public void Restore(){PlaytestValues.DefaultTool=Tools.Random;}
 }
}
