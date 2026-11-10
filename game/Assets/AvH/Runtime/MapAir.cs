using UnityEngine;
namespace AvH {
 /// <summary>
 /// What drifts in a map's air around the camera: dust motes in the sun, falling snow, or falling leaves
 /// (<see cref="MapLook.Air"/>). Presentation only; off at low quality.
 /// </summary>
 public static class MapAir {
  static ParticleSystem system;static Material dot,leaf;
  public static ParticleSystem Current=>system;
  static Material Sprite(bool round) {
   var tex=new Texture2D(32,32,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
   for(int y=0;y<32;y++)for(int x=0;x<32;x++) {
    float dx=(x-15.5f)/15.5f,dy=(y-15.5f)/15.5f;
    // A soft disc for motes and flakes; a pointed leaf shape for leaves.
    float a=round?Mathf.Clamp01((1-Mathf.Sqrt(dx*dx+dy*dy))*2.2f):Mathf.Clamp01((1-(Mathf.Abs(dx)*2.1f+dy*dy))*4);
    tex.SetPixel(x,y,new Color(1,1,1,a));
   }
   tex.Apply();
   return new Material(Shader.Find("Sprites/Default")){mainTexture=tex,hideFlags=HideFlags.HideAndDontSave};
  }
  public static void Apply(Camera camera,MapLook look,GraphicsQuality quality) {
   if(system!=null){if(Application.isPlaying)Object.Destroy(system.gameObject);else Object.DestroyImmediate(system.gameObject);system=null;}
   if(camera==null||look==null||quality==GraphicsQuality.Low)return;
   float density=quality==GraphicsQuality.Medium?.5f:1;
   var go=new GameObject("Map air");go.transform.SetParent(camera.transform,false);go.transform.rotation=Quaternion.identity;
   system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var main=system.main;main.loop=true;main.prewarm=true;main.playOnAwake=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0;main.startColor=look.Motes;
   var emission=system.emission;var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Box;
   var velocity=system.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
   var noise=system.noise;noise.enabled=true;noise.frequency=.25f;noise.scrollSpeed=.2f;
   var fade=system.colorOverLifetime;fade.enabled=true;var g=new Gradient();
   g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(1,.8f),new GradientAlphaKey(0,1)});fade.color=g;
   var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
   if(dot==null)dot=Sprite(true);if(leaf==null)leaf=Sprite(false);renderer.sharedMaterial=dot;
   switch(look.Air) {
    case 1: // snow: flakes fall slowly from above and sway
     go.transform.localPosition=Vector3.zero;shape.position=new Vector3(0,9,6);shape.scale=new Vector3(44,1,44);
     main.startLifetime=9;main.startSize=new ParticleSystem.MinMaxCurve(.05f,.11f);main.maxParticles=900;emission.rateOverTime=90*density;
     velocity.x=new ParticleSystem.MinMaxCurve(-.5f,.1f);velocity.y=new ParticleSystem.MinMaxCurve(-2.1f,-1.5f);velocity.z=new ParticleSystem.MinMaxCurve(-.2f,.2f);noise.strength=.35f;break;
    case 2: // leaves: a few tumble down across the view
     shape.position=new Vector3(0,8,8);shape.scale=new Vector3(40,1,40);renderer.sharedMaterial=leaf;
     main.startLifetime=10;main.startSize=new ParticleSystem.MinMaxCurve(.14f,.24f);main.maxParticles=120;emission.rateOverTime=9*density;
     main.startRotation=new ParticleSystem.MinMaxCurve(0,6.28f);var spin=system.rotationOverLifetime;spin.enabled=true;spin.z=new ParticleSystem.MinMaxCurve(-2.5f,2.5f);
     velocity.x=new ParticleSystem.MinMaxCurve(.2f,.9f);velocity.y=new ParticleSystem.MinMaxCurve(-1.5f,-.9f);velocity.z=new ParticleSystem.MinMaxCurve(-.3f,.3f);noise.strength=.8f;break;
    default: // dust motes hanging in the light
     shape.position=new Vector3(0,1,7);shape.scale=new Vector3(26,9,26);
     main.startLifetime=7;main.startSize=new ParticleSystem.MinMaxCurve(.02f,.04f);main.maxParticles=260;emission.rateOverTime=32*density;
     velocity.x=new ParticleSystem.MinMaxCurve(-.12f,.2f);velocity.y=new ParticleSystem.MinMaxCurve(-.05f,.12f);velocity.z=new ParticleSystem.MinMaxCurve(-.12f,.12f);noise.strength=.18f;break;
   }
   system.Play();
  }
 }
}
