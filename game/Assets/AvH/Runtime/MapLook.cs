using UnityEngine;
namespace AvH {
 /// <summary>
 /// One map's time of day: where the sun stands and its colour, the three ambient colours that tint the shadow side,
 /// the sky and haze, and the colour grade on top. Each map gets its own so six maps do not share one flat noon.
 /// Presentation only.
 /// </summary>
 public sealed class MapLook {
  /// <summary>Sun height above the horizon and compass heading it shines toward (degrees).</summary>
  public float SunHeight=40,SunHeading=-35,SunIntensity=1.25f,ShadowStrength=.8f;
  public Color Sun=new Color(1,.91f,.73f);
  public Color AmbientSky,AmbientEquator,AmbientGround;
  public Color Horizon,Zenith,Cloud=new Color(1,.98f,.94f),CloudShade=new Color(.8f,.84f,.92f);
  public float CloudAmount=.45f,FogStart=70,FogEnd=185;
  /// <summary>Colour grade: white balance (−cool … +warm), tint, saturation, contrast, exposure stops, bloom strength.</summary>
  public float Temperature,Tint,Saturation=20,Contrast=14,Exposure=.15f,Bloom=.4f;
  /// <summary>Colour pushed into the darkest and the brightest tones (neutral = grey .5).</summary>
  public Color ShadowTint=new Color(.5f,.5f,.5f),HighlightTint=new Color(.5f,.5f,.5f);
  /// <summary>Colour of the light that outlines a body from behind, and the airborne specks of this place.</summary>
  public Color Rim=new Color(1,.95f,.85f),Motes=new Color(1,.97f,.85f,.5f);
  /// <summary>What drifts in the air: 0 dust motes, 1 falling snow, 2 falling leaves.</summary>
  public int Air;

  public Vector3 ToSun=>-(Quaternion.Euler(SunHeight,SunHeading,0)*Vector3.forward);

  public static MapLook For(int map) {
   switch(map) {
    // A bright market afternoon: warm sun from the side, blue in the shade.
    default:return new MapLook{SunHeight=36,SunHeading=-62,SunIntensity=1.3f,Sun=new Color(1,.9f,.7f),
     AmbientSky=new Color(.52f,.68f,.92f),AmbientEquator=new Color(.62f,.6f,.52f),AmbientGround=new Color(.36f,.28f,.2f),
     Horizon=new Color(.84f,.88f,.86f),Zenith=new Color(.22f,.5f,.82f),CloudAmount=.42f,
     Temperature=4,Saturation=24,Contrast=16,ShadowTint=new Color(.46f,.49f,.56f),HighlightTint=new Color(.54f,.51f,.46f)};
    // Late afternoon in the yard: a low orange sun rakes through the roof trusses, violet shade.
    case GameMaps.Warehouse:return new MapLook{SunHeight=24,SunHeading=-118,SunIntensity=1.45f,Sun=new Color(1,.78f,.5f),
     ShadowStrength=.66f,AmbientSky=new Color(.62f,.68f,.98f),AmbientEquator=new Color(.8f,.66f,.6f),AmbientGround=new Color(.5f,.38f,.32f),
     Horizon=new Color(.98f,.8f,.6f),Zenith=new Color(.26f,.42f,.78f),Cloud=new Color(1,.92f,.8f),CloudShade=new Color(.86f,.74f,.74f),CloudAmount=.36f,
     FogStart=55,FogEnd=170,Temperature=6,Tint=2,Saturation=20,Contrast=10,Exposure=.15f,Bloom=.55f,
     ShadowTint=new Color(.47f,.46f,.58f),HighlightTint=new Color(.56f,.51f,.44f),Rim=new Color(1,.8f,.55f),Motes=new Color(1,.85f,.6f,.6f)};
    // Desert noon: a hard white sun, teal sky, shade that keeps the sky's colour.
    case GameMaps.Crossing:return new MapLook{SunHeight=47,SunHeading=-128,SunIntensity=1.45f,Sun=new Color(1,.94f,.8f),
     AmbientSky=new Color(.44f,.72f,.9f),AmbientEquator=new Color(.74f,.66f,.5f),AmbientGround=new Color(.46f,.36f,.24f),
     Horizon=new Color(.9f,.87f,.74f),Zenith=new Color(.1f,.5f,.78f),CloudAmount=.22f,
     FogStart=80,FogEnd=200,Temperature=6,Saturation=26,Contrast=20,Exposure=.05f,Bloom=.45f,
     ShadowTint=new Color(.44f,.5f,.56f),HighlightTint=new Color(.55f,.52f,.45f),Motes=new Color(1,.92f,.7f,.55f)};
    // Above the clouds: thin clear air, deep blue overhead, white haze below.
    case GameMaps.Sky:return new MapLook{SunHeight=42,SunHeading=140,SunIntensity=1.35f,Sun=new Color(1,.96f,.86f),
     AmbientSky=new Color(.46f,.66f,1),AmbientEquator=new Color(.7f,.76f,.84f),AmbientGround=new Color(.5f,.56f,.66f),
     Horizon=new Color(.86f,.93f,1),Zenith=new Color(.1f,.36f,.84f),CloudAmount=.6f,
     FogStart=60,FogEnd=210,Temperature=-2,Saturation=28,Contrast=16,Bloom=.5f,
     ShadowTint=new Color(.45f,.49f,.58f),HighlightTint=new Color(.52f,.52f,.5f),Rim=new Color(.9f,.96f,1),Motes=new Color(1,1,1,.4f)};
    // A cold clear morning: low rose-gold sun, long blue shadows on the snow, falling flakes.
    case GameMaps.Snow:return new MapLook{SunHeight=19,SunHeading=58,SunIntensity=1.3f,Sun=new Color(1,.84f,.7f),
     AmbientSky=new Color(.5f,.64f,.98f),AmbientEquator=new Color(.6f,.66f,.8f),AmbientGround=new Color(.5f,.56f,.7f),
     Horizon=new Color(.98f,.86f,.82f),Zenith=new Color(.3f,.5f,.86f),Cloud=new Color(1,.95f,.93f),CloudShade=new Color(.8f,.8f,.92f),CloudAmount=.4f,
     FogStart=40,FogEnd=150,Temperature=-8,Tint=2,Saturation=18,Contrast=14,Exposure=0,Bloom=.6f,
     ShadowTint=new Color(.44f,.48f,.6f),HighlightTint=new Color(.55f,.51f,.49f),Rim=new Color(1,.86f,.78f),Motes=new Color(1,1,1,.85f),Air=1};
    // Golden hour in the forest: the sun almost down, amber light, purple shade, leaves coming down.
    case GameMaps.Ruins:return new MapLook{SunHeight=15,SunHeading=-150,SunIntensity=1.5f,Sun=new Color(1,.68f,.4f),
     ShadowStrength=.62f,AmbientSky=new Color(.62f,.6f,.95f),AmbientEquator=new Color(.82f,.64f,.6f),AmbientGround=new Color(.48f,.4f,.38f),
     Horizon=new Color(1,.7f,.46f),Zenith=new Color(.24f,.3f,.66f),Cloud=new Color(1,.84f,.66f),CloudShade=new Color(.8f,.58f,.62f),CloudAmount=.4f,
     FogStart=35,FogEnd=150,Temperature=7,Tint=3,Saturation=20,Contrast=10,Exposure=.25f,Bloom=.6f,
     ShadowTint=new Color(.48f,.45f,.58f),HighlightTint=new Color(.57f,.51f,.43f),Rim=new Color(1,.72f,.45f),Motes=new Color(1,.75f,.35f,.8f),Air=2};
   }
  }
 }
}
