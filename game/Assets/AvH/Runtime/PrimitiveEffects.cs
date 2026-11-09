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
  /// <summary>Local graphics quality share of particles per burst (1 = all). Rings and at least one particle always show.</summary>
  public static float Density=1;
  public bool AutomaticUpdate=true;
  Material gunBody,gunTrim,gunWood,gunTape,cork;
  /// <summary>
  /// The cork popper: an orange workshop tube taped to a bike-pump chamber, with a cork waiting in the muzzle.
  /// The object keeps the original emitter name and pose so aiming and the hand grip are unchanged.
  /// </summary>
  public Transform Gun(Transform owner) {
   if(gunBody==null){gunBody=PrototypeVillage.Material(new Color(.93f,.45f,.12f));gunTrim=PrototypeVillage.Material(new Color(.2f,.55f,.35f));gunWood=PrototypeVillage.Material(new Color(.55f,.36f,.2f));gunTape=PrototypeVillage.Material(new Color(.62f,.64f,.66f));}
   var gun=new GameObject("Bubble emitter").transform;gun.SetParent(owner,false);gun.localPosition=new Vector3(.1f,1.3f,.3f);
   GunPart(gun,PrimitiveType.Cylinder,new Vector3(0,.01f,.24f),new Vector3(.085f,.2f,.085f),Quaternion.Euler(90,0,0),gunBody);   // tube
   GunPart(gun,PrimitiveType.Cylinder,new Vector3(0,.01f,.43f),new Vector3(.11f,.02f,.11f),Quaternion.Euler(90,0,0),gunBody);    // muzzle lip
   GunPart(gun,PrimitiveType.Cylinder,new Vector3(0,.01f,.455f),new Vector3(.07f,.025f,.07f),Quaternion.Euler(90,0,0),CorkMaterial()); // loaded cork
   GunPart(gun,PrimitiveType.Cylinder,new Vector3(0,.0f,.0f),new Vector3(.16f,.12f,.16f),Quaternion.Euler(90,0,0),gunTrim);      // pump chamber
   GunPart(gun,PrimitiveType.Cylinder,new Vector3(0,.01f,.16f),new Vector3(.1f,.02f,.1f),Quaternion.Euler(90,0,0),gunTape);       // tape band
   GunPart(gun,PrimitiveType.Cylinder,new Vector3(0,.0f,-.17f),new Vector3(.03f,.07f,.03f),Quaternion.Euler(90,0,0),gunTape);     // pump rod
   GunPart(gun,PrimitiveType.Cube,new Vector3(0,.0f,-.25f),new Vector3(.18f,.05f,.05f),Quaternion.identity,gunWood);                 // T handle
   GunPart(gun,PrimitiveType.Cube,new Vector3(0,-.12f,-.03f),new Vector3(.07f,.2f,.09f),Quaternion.Euler(-12,0,0),gunWood);         // grip
   gun.gameObject.AddComponent<BubbleGunPose>();
   return gun;
  }
  Material CorkMaterial(){if(cork==null)cork=PrototypeVillage.Material(new Color(.8f,.62f,.4f));return cork;}
  /// <summary>
  /// A fired cork: small and tumbling with a short white streak. The judged hit radius stays the shot's own
  /// setting; only the picture is small. The root carries the single body renderer (the trail is a child).
  /// </summary>
  public GameObject Cork(Transform parent) {
   Initialize();var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Cork";go.transform.SetParent(parent,false);go.transform.localScale=new Vector3(.11f,.06f,.11f);
   var collider=go.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
   var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=CorkMaterial();renderer.shadowCastingMode=ShadowCastingMode.Off;
   var streak=new GameObject("Cork trail");streak.transform.SetParent(go.transform,false);
   var trail=streak.AddComponent<TrailRenderer>();trail.sharedMaterial=material;trail.time=.12f;trail.minVertexDistance=.05f;trail.widthMultiplier=.06f;
   trail.widthCurve=AnimationCurve.Linear(0,1,1,0);trail.startColor=new Color(1,.97f,.88f,.85f);trail.endColor=new Color(1,.97f,.88f,0);trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;
   go.AddComponent<CorkFlight>();return go;
  }
  /// <summary>Presentation only: the cork that hit something pings back up, bounces off the floor and settles.</summary>
  public void Ricochet(Vector3 at,Vector3 incoming) {
   Initialize();var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Cork ricochet";go.transform.SetParent(transform,false);go.transform.position=at;go.transform.localScale=new Vector3(.11f,.06f,.11f);
   var collider=go.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
   var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=CorkMaterial();renderer.shadowCastingMode=ShadowCastingMode.Off;
   incoming.y=0;if(incoming.sqrMagnitude<.001f)incoming=Vector3.forward;incoming.Normalize();
   float side=((ricochets++*7)%5-2)*.6f;
   go.AddComponent<CorkBounce>().Velocity=-incoming*2.6f+Vector3.Cross(Vector3.up,incoming)*side+Vector3.up*4.2f;
  }
  int ricochets;
  static void GunPart(Transform parent,PrimitiveType type,Vector3 p,Vector3 size,Quaternion rotation,Material mat) {var go=GameObject.CreatePrimitive(type);var c=go.GetComponent<Collider>();c.enabled=false;Dispose(c);go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localRotation=rotation;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;}
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
   count=count<=0?0:Mathf.Max(1,Mathf.RoundToInt(count*Mathf.Clamp01(Density)));
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
  void OnDestroy(){if(material!=null)Dispose(material);if(film!=null)Dispose(film);if(ringMesh!=null)Dispose(ringMesh);foreach(var m in new[]{gunBody,gunTrim,gunWood,gunTape,cork})if(m!=null)Dispose(m);}
 }
 public sealed class BubbleMotion:MonoBehaviour {
  public float Radius=.3f;
  float age;
  void Update(){age+=Time.deltaTime;float wobble=Mathf.Sin(age*13)*.045f;transform.localScale=new Vector3(1+wobble,1-wobble,1)*Radius*2;}
 }
 /// <summary>Points the cork along its flight and tumbles it (presentation only).</summary>
 public sealed class CorkFlight:MonoBehaviour {
  Vector3 last;bool started;float spin;
  void LateUpdate() {
   var delta=transform.position-last;last=transform.position;
   if(!started||delta.sqrMagnitude<1e-6f){started=true;return;}
   spin+=Time.deltaTime*1500;transform.rotation=Quaternion.LookRotation(delta)*Quaternion.Euler(90,0,0)*Quaternion.Euler(0,spin,0)*Quaternion.Euler(18,0,0);
  }
 }
 /// <summary>A popped-off cork: gravity, two or three floor bounces and a quick shrink. No collider, never judged.</summary>
 public sealed class CorkBounce:MonoBehaviour {
  public Vector3 Velocity;float age;Vector3 spinAxis=new Vector3(1,.3f,.2f);
  const float Life=1.4f,Gravity=18;
  void Update() {
   float dt=Time.deltaTime;age+=dt;if(age>=Life){Destroy(gameObject);return;}
   Velocity+=Vector3.down*Gravity*dt;var next=transform.position+Velocity*dt;
   if(Velocity.y<0&&Physics.Raycast(transform.position+Vector3.up*.05f,Vector3.down,out var hit,.05f-Velocity.y*dt,~0,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<CharacterController>()==null)
    {next.y=hit.point.y+.03f;Velocity=new Vector3(Velocity.x*.6f,-Velocity.y*.45f,Velocity.z*.6f);}
   transform.position=next;transform.Rotate(spinAxis,720*dt*Mathf.Clamp01(Velocity.magnitude/3),Space.Self);
   if(age>Life-.25f)transform.localScale=new Vector3(.11f,.06f,.11f)*Mathf.Clamp01((Life-age)/.25f);
  }
 }
}
