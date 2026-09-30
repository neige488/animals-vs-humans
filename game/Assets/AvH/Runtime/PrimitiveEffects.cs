using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace AvH {
 // Presentation only: pooled, collider-free geometry never participates in combat or navigation.
 public sealed class PrimitiveEffects : MonoBehaviour {
  const int Capacity=256;
  sealed class Particle {public Transform Transform;public Renderer Renderer;public Vector3 Velocity;public float Age,Life,Size,Gravity;public Color Color;public bool Ring;}
  readonly List<Particle> active=new List<Particle>();
  readonly Stack<Particle> pool=new Stack<Particle>();
  Material material,film;Mesh ringMesh;MaterialPropertyBlock properties;
  public int ActiveCount=>active.Count;
  public bool AutomaticUpdate=true;
  AudioSource audioSource;AudioClip popSound,airSound;float nextSound;
  Material gunBody,gunTrim;
  public Transform Gun(Transform owner) {
   if(gunBody==null){gunBody=PrototypeVillage.Material(new Color(.08f,.38f,.43f));gunTrim=PrototypeVillage.Material(new Color(.92f,.66f,.23f));}
   var gun=new GameObject("Bubble emitter").transform;gun.SetParent(owner,false);gun.localPosition=new Vector3(.32f,.92f,.18f);
   GunPart(gun,PrimitiveType.Capsule,new Vector3(0,0,.15f),new Vector3(.18f,.25f,.18f),Quaternion.Euler(90,0,0),gunBody);
   GunPart(gun,PrimitiveType.Cylinder,new Vector3(0,0,.4f),new Vector3(.24f,.07f,.24f),Quaternion.Euler(90,0,0),gunTrim);
   GunPart(gun,PrimitiveType.Sphere,new Vector3(0,-.15f,.04f),new Vector3(.25f,.3f,.25f),Quaternion.identity,gunBody);
   return gun;
  }
  static void GunPart(Transform parent,PrimitiveType type,Vector3 p,Vector3 size,Quaternion rotation,Material mat) {var go=GameObject.CreatePrimitive(type);var c=go.GetComponent<Collider>();c.enabled=false;Dispose(c);go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localRotation=rotation;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;}
  public void Sound(Vector3 position,bool shot) {
   if(Application.isBatchMode||!Application.isPlaying||Time.time<nextSound)return;
   var camera=Camera.main;if(camera==null)return;float distance=Vector3.Distance(camera.transform.position,position);if(distance>28)return;
   if(audioSource==null){audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;popSound=MakeSound(false);airSound=MakeSound(true);}
   nextSound=Time.time+.04f;audioSource.PlayOneShot(shot?airSound:popSound,(shot?.09f:.17f)/(1+distance*.12f));
  }
  static AudioClip MakeSound(bool air) {
   const int rate=22050;int count=air?2205:3307;var samples=new float[count];var random=new System.Random(19);float phase=0;
   for(int i=0;i<count;i++){float t=(float)i/count;phase+=(air?900:1600)*(1-t*.85f)*Mathf.PI*2/rate;float noise=(float)random.NextDouble()*2-1;samples[i]=(air?noise*.5f:Mathf.Sin(phase)*.6f+noise*.15f)*Mathf.Sin(Mathf.PI*Mathf.Min(1,t*8))*Mathf.Exp(-t*7);}
   var clip=AudioClip.Create(air?"Air puff":"Bubble pop",count,1,rate,false);clip.SetData(samples,0);return clip;
  }
  public void Initialize() {
   if(material!=null)return;
   properties=new MaterialPropertyBlock();
   material=new Material(Shader.Find("Sprites/Default"));
   film=new Material(Resources.Load<Shader>("SoapFilm"));
   ringMesh=new Mesh{name="Primitive ripple"};var vertices=new Vector3[96];var triangles=new int[288];
   for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;vertices[i*2]=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));vertices[i*2+1]=vertices[i*2]*.91f;int j=(i+1)%48;int t=i*6;triangles[t]=i*2;triangles[t+1]=j*2;triangles[t+2]=i*2+1;triangles[t+3]=j*2;triangles[t+4]=j*2+1;triangles[t+5]=i*2+1;}
   ringMesh.vertices=vertices;ringMesh.triangles=triangles;ringMesh.RecalculateNormals();
  }
  public GameObject Bubble(Transform parent,float radius) {
   Initialize();var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Soap bubble";go.transform.SetParent(parent,false);go.transform.localScale=Vector3.one*radius*2;
   var collider=go.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
   var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=film;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
   go.AddComponent<BubbleMotion>().Radius=radius;return go;
  }
  public void Emit(Vector3 p,Color color,int count,float power=.8f,bool ring=true) {
   Initialize();if(ring)Spawn(p+Vector3.up*.08f,Vector3.zero,color,.55f,.3f,0,true);
   for(int i=0;i<count;i++) {
    float angle=(i*2.39996f+Time.time)*1.7f;var direction=new Vector3(Mathf.Cos(angle),.3f+(i%4)*.25f,Mathf.Sin(angle));
    Spawn(p,direction*power,color,.3f+(i%4)*.09f,.045f+(i%3)*.022f,1.6f,false);
   }
  }
  void Spawn(Vector3 p,Vector3 velocity,Color color,float life,float size,float gravity,bool ring) {
   if(active.Count>=Capacity)return;
   Particle particle;
   if(pool.Count>0)particle=pool.Pop();
   else {var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);var c=go.GetComponent<Collider>();c.enabled=false;Dispose(c);go.name="Effect particle";go.transform.SetParent(transform,false);particle=new Particle{Transform=go.transform,Renderer=go.GetComponent<Renderer>()};particle.Renderer.shadowCastingMode=ShadowCastingMode.Off;particle.Renderer.receiveShadows=false;}
   if(sphereMesh==null){var primitive=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphereMesh=primitive.GetComponent<MeshFilter>().sharedMesh;primitive.SetActive(false);Dispose(primitive);}
   particle.Transform.GetComponent<MeshFilter>().sharedMesh=ring?ringMesh:sphereMesh;
   particle.Renderer.sharedMaterial=material;particle.Transform.gameObject.SetActive(true);particle.Transform.position=p;particle.Transform.rotation=Quaternion.identity;
   particle.Velocity=velocity;particle.Color=color;particle.Age=0;particle.Life=life;particle.Size=size;particle.Gravity=gravity;particle.Ring=ring;particle.Transform.localScale=Vector3.one*size;active.Add(particle);
  }
  Mesh sphereMesh;
  void Update(){if(AutomaticUpdate)Advance(Time.deltaTime);}
  public void Advance(float seconds) {
   for(int i=active.Count-1;i>=0;i--){var p=active[i];p.Age+=seconds;if(p.Age>=p.Life){p.Transform.gameObject.SetActive(false);pool.Push(p);active.RemoveAt(i);continue;}
    float t=p.Age/p.Life;p.Velocity+=Vector3.down*p.Gravity*seconds;p.Transform.position+=p.Velocity*seconds;
    float size=p.Ring?p.Size+t*1.25f:p.Size*(1-t*.8f);p.Transform.localScale=Vector3.one*size;
    properties.SetColor("_Color",new Color(p.Color.r,p.Color.g,p.Color.b,p.Color.a*(1-t)*(1-t)));p.Renderer.SetPropertyBlock(properties);
   }
  }
  static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
  void OnDestroy(){if(material!=null)Dispose(material);if(film!=null)Dispose(film);if(ringMesh!=null)Dispose(ringMesh);if(popSound!=null)Dispose(popSound);if(airSound!=null)Dispose(airSound);if(gunBody!=null)Dispose(gunBody);if(gunTrim!=null)Dispose(gunTrim);}
 }
 public sealed class BubbleMotion:MonoBehaviour {
  public float Radius=.3f;
  float age;
  void Update(){age+=Time.deltaTime;float wobble=Mathf.Sin(age*13)*.045f;transform.localScale=new Vector3(1+wobble,1-wobble,1)*Radius*2;}
 }
}
