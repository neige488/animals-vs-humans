using System.Collections.Generic;
using UnityEngine;
namespace AvH {
 // What abilities look and sound like, read from public state and public events only.
 // Hosts and guests run the same code, so both see the same balloon, banana peel and smoke. Presentation only.
 public sealed partial class UnityPlaytestSession {
  static readonly Color Red=new Color(.86f,.14f,.12f),Yellow=new Color(.98f,.8f,.15f),Blue=new Color(.25f,.55f,.95f),Ice=new Color(.7f,.9f,1),Wood=new Color(.55f,.36f,.2f),Straw=new Color(.86f,.72f,.35f),Steel=new Color(.66f,.68f,.7f),Smoke=new Color(.42f,.44f,.47f),Leaf=new Color(.3f,.75f,.35f),Violet=new Color(.62f,.45f,.95f),Dust=new Color(.9f,.83f,.64f,.7f);
  readonly Dictionary<int,GameObject> gadgetViews=new Dictionary<int,GameObject>();
  readonly GameObject[] abilityProps=new GameObject[12];readonly int[] abilityPropKeys=new int[12];readonly float[] abilityTick=new float[12];
  GameObject howlMark;float abilityClock;
  /// <summary>Ability props currently shown on a body (0 = none); the key is the ability kind, or 100 + the hold kind.</summary>
  public int AbilityPropKey(int slot)=>slot>=0&&slot<abilityPropKeys.Length&&abilityProps[slot]!=null?abilityPropKeys[slot]:0;
  /// <summary>Gadgets (banana peel, spring pad, scarecrow, smoke, zero-g field) drawn in the world right now.</summary>
  public int GadgetViewCount=>gadgetViews.Count;
  /// <summary>Whose screen this is (host and guest alike); the wolf's howl mark shows only to animals.</summary>
  public int AbilityViewerSlot=-1;
  public bool HowlMarkShown=>howlMark!=null&&howlMark.activeSelf;
  Transform Group(string name,Transform parent,Vector3 at){var t=new GameObject(name).transform;t.SetParent(parent,false);if(parent==null||parent==transform)t.position=at;else t.localPosition=at;return t;}
  void Part(Transform parent,PrimitiveType type,Vector3 p,Vector3 size,Color color,Quaternion? rotation=null)=>Effects.Part(parent,type,p,size,rotation??Quaternion.identity,color);
  /// <summary>Per frame: gadgets on the ground, what each body carries or is stuck in, and running effects.</summary>
  void PresentAbilities(SessionState state,float seconds) {
   if(state?.Players==null){ForgetAbilityViews();return;}
   abilityClock+=seconds;
   // ----- gadgets -----
   var live=new HashSet<int>();
   foreach(var g in state.Gadgets??new GadgetState[0]) {
    live.Add(g.Id);
    if(!gadgetViews.TryGetValue(g.Id,out var view)||view==null)gadgetViews[g.Id]=view=BuildGadget(g);
    if(g.Kind==AbilityKind.Smoke&&Tick(ref smokeTick,.12f))Effects.Puff(ToVector(g.Position)+new Vector3(Mathf.Sin(abilityClock*7)*2.5f,.6f,Mathf.Cos(abilityClock*5)*2.5f),new Color(.6f,.62f,.65f,.8f),1.4f,1.3f);
    if(g.Kind==AbilityKind.ZeroG&&Tick(ref fieldTick,.2f))Effects.Puff(ToVector(g.Position)+new Vector3(Mathf.Sin(abilityClock*3)*3,.2f,Mathf.Cos(abilityClock*4)*3),new Color(.75f,.6f,1,.8f),.18f,1.4f);
   }
   var gone=new List<int>();foreach(var pair in gadgetViews)if(!live.Contains(pair.Key))gone.Add(pair.Key);
   foreach(int id in gone){if(gadgetViews[id]!=null){Effects.Emit(gadgetViews[id].transform.position+Vector3.up*.3f,Dust,6,1,false);Destroy(gadgetViews[id]);}gadgetViews.Remove(id);}
   // ----- bodies -----
   PlayerState howler=null;
   for(int i=0;i<state.Players.Length&&i<bodies.Count&&i<abilityProps.Length;i++) {
    var p=state.Players[i];var body=bodies[i].transform;bool active=p.AbilityActiveRemaining>0,held=p.RootRemaining>0||p.LiftRemaining>0;
    if(active&&p.Ability==AbilityKind.Howl)howler=p;
    int key=held&&HoldHasProp(p.Hold)?100+(int)p.Hold:active&&HasProp(p.Ability)?(int)p.Ability:0;
    if(abilityProps[i]==null||abilityPropKeys[i]!=key){if(abilityProps[i]!=null)Destroy(abilityProps[i]);abilityProps[i]=key==0?null:BuildProp(body,key>=100?(AbilityKind)(key-100):p.Ability,key>=100,p.Faction);abilityPropKeys[i]=key;}
    if(abilityProps[i]!=null&&key==(int)AbilityKind.Balloon)abilityProps[i].transform.localRotation=Quaternion.Euler(Mathf.Sin(abilityClock*2.3f+i)*6,0,Mathf.Cos(abilityClock*1.9f+i)*6);
    abilityTick[i]-=seconds;if(abilityTick[i]>0)continue;abilityTick[i]=.14f;
    var forward=Quaternion.Euler(0,PresentationYaw(i),0)*Vector3.forward;var head=body.position+Vector3.up*(p.Faction==Faction.Human?1.95f:1.3f);
    if(active)switch(p.Ability) {
     case AbilityKind.WaterCannon:Effects.Spray(body.position+Vector3.up*1.25f+forward*.5f,forward+Vector3.up*.08f,new Color(.45f,.75f,1,.9f),7,Abilities.CannonRange*1.7f,.12f,.9f);break;
     case AbilityKind.Hunt:Effects.Emit(body.position+Vector3.up*.6f,new Color(1,.55f,.1f,.9f),3,.9f,false);break;
     case AbilityKind.BellySlide:Effects.Spray(body.position+Vector3.up*.1f,-forward+Vector3.up*.3f,new Color(.85f,.95f,1,.9f),3,3,.4f,1.2f);break;
     case AbilityKind.Charge:case AbilityKind.Stampede:case AbilityKind.Kickboard:Effects.Spray(body.position+Vector3.up*.1f,-forward+Vector3.up*.25f,Dust,p.Ability==AbilityKind.Stampede?5:3,3,.5f,.8f);break;
     case AbilityKind.Bite:Effects.Emit(head,new Color(.55f,.95f,.4f,.8f),2,.5f,false);break;
     case AbilityKind.Vaccine:Effects.Emit(head,new Color(.4f,1,.55f,.8f),2,.5f,false);break;
    }
    // Things done to this body: dizzy stars, slow drips, the after-vaccine glow.
    if(p.ShakeRemaining>0)Effects.Emit(head+new Vector3(Mathf.Sin(abilityClock*9)*.3f,.1f,Mathf.Cos(abilityClock*9)*.3f),Yellow,1,.3f,false);
    if(p.SlowRemaining>0)Effects.Emit(body.position+Vector3.up*.25f,new Color(.55f,.75f,1,.8f),2,.4f,false);
    if(p.InvulnerableRemaining>0)Effects.Emit(body.position+Vector3.up*1,new Color(.4f,1,.55f,.9f),3,.9f,false);
   }
   // ----- the wolf's howl: every animal sees a beam over the human nearest to the wolf -----
   PlayerState marked=null;
   if(howler!=null&&AbilityViewerSlot>=0&&AbilityViewerSlot<state.Players.Length&&state.Players[AbilityViewerSlot].Faction==Faction.Animal){double best=double.MaxValue;foreach(var h in state.Players){if(h.Faction!=Faction.Human)continue;double d=(ToVector(h.Position)-ToVector(howler.Position)).sqrMagnitude;if(d<best){best=d;marked=h;}}}
   if(marked==null){if(howlMark!=null)howlMark.SetActive(false);}
   else {
    if(howlMark==null){howlMark=Group("Howl mark",transform,Vector3.zero).gameObject;Part(howlMark.transform,PrimitiveType.Cylinder,new Vector3(0,9,0),new Vector3(.12f,6,.12f),new Color(1,.4f,.15f));Part(howlMark.transform,PrimitiveType.Sphere,new Vector3(0,2.6f,0),Vector3.one*.35f,new Color(1,.4f,.15f));}
    howlMark.SetActive(true);howlMark.transform.position=marked.Slot<bodies.Count?bodies[marked.Slot].transform.position:ToVector(marked.Position);
   }
  }
  float smokeTick,fieldTick;
  bool Tick(ref float next,float every){if(abilityClock<next)return false;next=abilityClock+every;return true;}
  static bool HasProp(AbilityKind kind)=>kind==AbilityKind.Balloon||kind==AbilityKind.Umbrella||kind==AbilityKind.Kickboard||kind==AbilityKind.Helmet||kind==AbilityKind.Disguise||kind==AbilityKind.Vaccine||kind==AbilityKind.Flap;
  static bool HoldHasProp(AbilityKind kind)=>kind==AbilityKind.Lasso||kind==AbilityKind.BigBubble||kind==AbilityKind.IceRay||kind==AbilityKind.Banana||kind==AbilityKind.Roar||kind==AbilityKind.Kickboard;
  GameObject BuildGadget(GadgetState g) {
   var root=Group("Gadget "+g.Kind,transform,ToVector(g.Position));
   switch(g.Kind) {
    case AbilityKind.Banana:
     Part(root,PrimitiveType.Sphere,new Vector3(0,.07f,0),new Vector3(.2f,.14f,.2f),Yellow);
     for(int k=0;k<4;k++)Part(root,PrimitiveType.Cube,Quaternion.Euler(0,k*90,0)*new Vector3(.2f,.03f,0),new Vector3(.34f,.03f,.12f),Yellow,Quaternion.Euler(0,k*90,-12));
     break;
    case AbilityKind.SpringPad:
     Part(root,PrimitiveType.Cylinder,new Vector3(0,.03f,0),new Vector3(1.5f,.03f,1.5f),Steel);
     for(int k=0;k<3;k++)Part(root,PrimitiveType.Cylinder,new Vector3(0,.08f+k*.05f,0),new Vector3(.5f,.012f,.5f),Steel);
     Part(root,PrimitiveType.Cylinder,new Vector3(0,.22f,0),new Vector3(1.3f,.03f,1.3f),Red);Part(root,PrimitiveType.Cylinder,new Vector3(0,.25f,0),new Vector3(.7f,.012f,.7f),Color.white);
     break;
    case AbilityKind.Scarecrow:
     Part(root,PrimitiveType.Cylinder,new Vector3(0,.9f,0),new Vector3(.08f,.9f,.08f),Wood);Part(root,PrimitiveType.Cube,new Vector3(0,1.35f,0),new Vector3(1.3f,.08f,.08f),Wood);
     Part(root,PrimitiveType.Cube,new Vector3(0,1.15f,0),new Vector3(.5f,.6f,.25f),Red);Part(root,PrimitiveType.Sphere,new Vector3(0,1.7f,0),Vector3.one*.38f,Straw);
     Part(root,PrimitiveType.Cylinder,new Vector3(0,1.88f,0),new Vector3(.6f,.02f,.6f),Wood);Part(root,PrimitiveType.Cylinder,new Vector3(0,1.98f,0),new Vector3(.3f,.09f,.3f),Wood);
     Part(root,PrimitiveType.Sphere,new Vector3(-.65f,1.3f,0),Vector3.one*.16f,Straw);Part(root,PrimitiveType.Sphere,new Vector3(.65f,1.3f,0),Vector3.one*.16f,Straw);
     break;
    case AbilityKind.Smoke:
     // Opaque blobs: the point is that nobody can see through it.
     for(int k=0;k<11;k++){float a=k*2.39996f,r=k==0?0:1.2f+(k%3)*.9f;Part(root,PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*r,.9f+(k%4)*.45f,Mathf.Sin(a)*r),Vector3.one*(2.6f-(k%3)*.4f),Smoke);}
     break;
    case AbilityKind.ZeroG:
     Effects.Dome(root,Abilities.ZeroGRadius);Part(root,PrimitiveType.Cylinder,new Vector3(0,.02f,0),new Vector3(Abilities.ZeroGRadius*2,.01f,Abilities.ZeroGRadius*2),Violet);
     break;
   }
   return root.gameObject;
  }
  GameObject BuildProp(Transform body,AbilityKind kind,bool held,Faction faction) {
   var root=Group((held?"Held by ":"Ability ")+kind,body,Vector3.zero);float mid=faction==Faction.Human?1:.6f;
   if(held)switch(kind) {
    case AbilityKind.Lasso:for(int k=0;k<3;k++)Part(root,PrimitiveType.Cylinder,new Vector3(0,mid-.15f+k*.15f,0),new Vector3(1.15f,.025f,1.15f),Straw);break;
    case AbilityKind.BigBubble:Effects.Dome(root,1.25f).localPosition=new Vector3(0,mid,0);break;
    case AbilityKind.IceRay:for(int k=0;k<6;k++)Part(root,PrimitiveType.Cube,Quaternion.Euler(0,k*60,0)*new Vector3(.55f,mid*.8f,0),new Vector3(.3f,mid*2,.3f),Ice,Quaternion.Euler(k*13,k*60,12));break;
    case AbilityKind.Banana:case AbilityKind.Roar:case AbilityKind.Kickboard:for(int k=0;k<3;k++)Part(root,PrimitiveType.Sphere,Quaternion.Euler(0,k*120,0)*new Vector3(.35f,mid*2+.25f,0),Vector3.one*.13f,Yellow);root.gameObject.AddComponent<DiscSpin>();break;
   }
   else switch(kind) {
    case AbilityKind.Balloon:
     var colors=new[]{Red,Yellow,Blue};
     for(int k=0;k<3;k++){var at=new Vector3((k-1)*.38f,3.1f+(k%2)*.3f,(k-1)*.1f);Part(root,PrimitiveType.Sphere,at,new Vector3(.62f,.74f,.62f),colors[k]);Part(root,PrimitiveType.Cylinder,(at+new Vector3(0,1.5f,.15f))*.5f+Vector3.down*.1f,new Vector3(.015f,(at.y-1.6f)*.5f,.015f),Color.white,Quaternion.FromToRotation(Vector3.up,at-new Vector3(0,1.5f,.15f)));}
     break;
    case AbilityKind.Umbrella:
     Part(root,PrimitiveType.Cylinder,new Vector3(.15f,1.95f,.2f),new Vector3(.03f,.6f,.03f),Steel);Part(root,PrimitiveType.Sphere,new Vector3(.15f,2.55f,.2f),new Vector3(1.7f,.4f,1.7f),Red);Part(root,PrimitiveType.Sphere,new Vector3(.15f,2.74f,.2f),Vector3.one*.1f,Yellow);
     break;
    case AbilityKind.Kickboard:
     Part(root,PrimitiveType.Cube,new Vector3(0,.09f,.1f),new Vector3(.22f,.04f,.95f),Blue);Part(root,PrimitiveType.Cylinder,new Vector3(0,.6f,.52f),new Vector3(.035f,.55f,.035f),Steel);Part(root,PrimitiveType.Cube,new Vector3(0,1.15f,.52f),new Vector3(.5f,.04f,.04f),Steel);
     Part(root,PrimitiveType.Cylinder,new Vector3(0,.06f,.55f),new Vector3(.13f,.03f,.13f),Color.black,Quaternion.Euler(0,0,90));Part(root,PrimitiveType.Cylinder,new Vector3(0,.06f,-.33f),new Vector3(.13f,.03f,.13f),Color.black,Quaternion.Euler(0,0,90));
     break;
    case AbilityKind.Helmet:Part(root,PrimitiveType.Sphere,new Vector3(0,1.8f,.02f),new Vector3(.36f,.26f,.4f),Yellow);Part(root,PrimitiveType.Cylinder,new Vector3(0,1.76f,.08f),new Vector3(.44f,.012f,.5f),Yellow);break;
    case AbilityKind.Vaccine:Part(root,PrimitiveType.Cube,new Vector3(0,2.3f,0),new Vector3(.34f,.11f,.08f),Leaf);Part(root,PrimitiveType.Cube,new Vector3(0,2.3f,0),new Vector3(.11f,.34f,.08f),Leaf);break;
    case AbilityKind.Flap:for(int k=-1;k<=1;k+=2)Part(root,PrimitiveType.Sphere,new Vector3(k*.35f,.55f,0),new Vector3(.5f,.05f,.3f),Color.white,Quaternion.Euler(0,0,k*-25));break;
    case AbilityKind.Disguise: {
     // The fox pulls a crate over itself. Where the map has crates it uses the same one, so it really can be mistaken for scenery.
     var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
     if(catalog!=null&&catalog.Crate!=null){var crate=Instantiate(catalog.Crate,root,false);crate.name="Disguise crate";crate.transform.localScale=Vector3.one*1.25f;foreach(var c in crate.GetComponentsInChildren<Collider>())Destroy(c);foreach(var b in crate.GetComponentsInChildren<Rigidbody>())Destroy(b);}
     else Part(root,PrimitiveType.Cube,new Vector3(0,.62f,0),Vector3.one*1.25f,Wood);
     break;}
   }
   return root.gameObject;
  }
  void ForgetAbilityViews() {
   foreach(var view in gadgetViews.Values)if(view!=null)Destroy(view);gadgetViews.Clear();
   for(int i=0;i<abilityProps.Length;i++){if(abilityProps[i]!=null)Destroy(abilityProps[i]);abilityProps[i]=null;abilityPropKeys[i]=0;}
   if(howlMark!=null)howlMark.SetActive(false);
  }
  /// <summary>One ability event, seen and heard once by everyone.</summary>
  void PresentAbilityEvent(FeelEvent e,Transform actor,Transform target) {
   var kind=(AbilityKind)Mathf.RoundToInt(e.Strength);var at=actor!=null?actor.position:ToVector(e.Position);var hit=target!=null?target.position:ToVector(e.Position);
   var forward=actor!=null&&e.Actor>=0&&e.Actor<bodies.Count?Quaternion.Euler(0,PresentationYaw(e.Actor),0)*Vector3.forward:Vector3.forward;var chest=at+Vector3.up*1.1f;
   if(e.Kind==FeelEventKind.AbilityEnd) {
    if(kind==AbilityKind.Balloon||kind==AbilityKind.Umbrella){Effects.Emit(at+Vector3.up*2.8f,Red,kind==AbilityKind.Balloon?12:4,2,false);Audio.Play("pop",at+Vector3.up*2.5f,1,kind==AbilityKind.Balloon?.8f:1.3f);}
    else if(kind==AbilityKind.Disguise){Effects.Emit(at+Vector3.up*.5f,Wood,10,2,false);Audio.Play("prop-knock",at,1,1.2f);}
    return;
   }
   if(e.Kind==FeelEventKind.AbilityHit) {
    switch(kind) {
     case AbilityKind.Acorn:Effects.Emit(hit+Vector3.up*1.7f,Wood,8,1.6f,false);Audio.Play("prop-knock",hit+Vector3.up*1.6f,1,1.6f);break;
     case AbilityKind.Helmet:Effects.Emit(hit+Vector3.up*1.8f,Yellow,12,2.2f);Audio.Play("prop-knock",hit+Vector3.up*1.8f,1,.8f);break;
     case AbilityKind.Vaccine:Effects.Wave(hit,Leaf,3);Effects.Emit(hit+Vector3.up*1,new Color(.4f,1,.55f),16,2.4f);Audio.Play("confirm",hit,1,.7f);break;
     case AbilityKind.Balloon:Effects.Emit(hit+Vector3.up*2.8f,Yellow,14,2.6f,false);Audio.Play("pop",hit+Vector3.up*2.5f,1,.7f);break;
     case AbilityKind.Banana:Effects.Emit(hit+Vector3.up*.2f,Yellow,10,2,false);Audio.Play("cushion",hit,1,1.5f);break;
     case AbilityKind.SpringPad:Effects.Wave(hit,Red,1.6f);Audio.Play("cushion",hit,1,1.9f);break;
     case AbilityKind.IceRay:Effects.Emit(hit+Vector3.up*.8f,Ice,16,2.2f);Audio.Play("disc-bounce",hit,1,1.7f);break;
     case AbilityKind.BigBubble:Effects.Emit(hit+Vector3.up*.8f,new Color(.8f,.95f,1,.9f),10,1.4f);Audio.Play("pop",hit,1,.55f);break;
     case AbilityKind.Lasso:Effects.Emit(hit+Vector3.up*.7f,Straw,10,1.6f);Audio.Play("punch-whiff",hit,1,.7f);break;
     case AbilityKind.WaterCannon:Effects.Emit(hit+Vector3.up*.8f,new Color(.45f,.75f,1,.9f),8,2,false);break;
     case AbilityKind.Whistle:case AbilityKind.Bite:case AbilityKind.Roar:Effects.Emit(hit+Vector3.up*1.9f,Yellow,6,1,false);break;
     default:Effects.Emit(hit+Vector3.up*.9f,Color.white,12,2.4f);Audio.Play("punch",hit+Vector3.up*.9f,1,kind==AbilityKind.Stampede?.7f:1);break;
    }
    return;
   }
   switch(kind) {
    case AbilityKind.Disguise:Effects.Emit(at+Vector3.up*.5f,Wood,8,1.4f);Audio.Play("prop-knock",at,1,.9f);break;
    case AbilityKind.DoubleJump:case AbilityKind.CatSpring:case AbilityKind.Leap:Effects.Emit(at,Color.white,8,1.6f);Audio.Play("cushion",at,1,1.7f);break;
    case AbilityKind.Flap:Effects.Emit(at+Vector3.up*.5f,Color.white,12,1.8f,false);Audio.Play("gust",at,1,1.6f);break;
    case AbilityKind.BellySlide:Effects.Emit(at,Ice,8,1.6f);Audio.Play("gust",at,1,1.3f);break;
    case AbilityKind.Acorn:Effects.Spray(chest+forward*.4f,forward+Vector3.up*.1f,Wood,3,Abilities.AcornRange*2,.03f,.6f);Audio.Play("punch-whiff",chest,1,1.5f);break;
    case AbilityKind.Howl:Effects.Wave(at,new Color(.8f,.85f,1),8);Audio.Play("growl-wolf",chest,1,.85f);break;
    case AbilityKind.Charge:case AbilityKind.Stampede:Effects.Emit(at,Dust,14,2.4f);Audio.Play("snarl",chest,1,kind==AbilityKind.Stampede?.6f:1.2f);break;
    case AbilityKind.Headbutt:Effects.Spray(chest,forward,Color.white,8,6,.5f,.4f);Audio.Play("punch-whiff",chest,1,.8f);break;
    case AbilityKind.Bite:Effects.Emit(chest,new Color(.55f,.95f,.4f),10,1.6f);Audio.Play("snarl",chest,1,.7f);break;
    case AbilityKind.Roar:Effects.Wave(at,new Color(1,.6f,.2f),Abilities.RoarRadius);Effects.Wave(at,new Color(1,.8f,.4f),Abilities.RoarRadius*.6f);Audio.Play("growl-bear",chest,1,.75f);break;
    case AbilityKind.Dive:Effects.Emit(at,Color.white,12,2.4f);Audio.Play("gust",at,1,.8f);break;
    case AbilityKind.Slam:Effects.Wave(at,Dust,Abilities.SlamRadius);Effects.Emit(at,Dust,18,3.4f);Audio.Play("land",at,1,.6f);break;
    case AbilityKind.Hunt:Effects.Emit(chest,new Color(1,.55f,.1f),16,2.4f);Audio.Play("growl-bear",chest,1,1.3f);break;
    case AbilityKind.Kickboard:Effects.Emit(at,Dust,8,1.8f);Audio.Play("gust",at,1,1.5f);break;
    case AbilityKind.Umbrella:Effects.Emit(at+Vector3.up*2.4f,Red,6,1.2f,false);Audio.Play("pop",at+Vector3.up*2,1,1.5f);break;
    case AbilityKind.Helmet:Effects.Emit(at+Vector3.up*1.8f,Yellow,6,1,false);Audio.Play("prop-knock",at+Vector3.up*1.8f,1,1.4f);break;
    case AbilityKind.Banana:case AbilityKind.Scarecrow:case AbilityKind.SpringPad:Effects.Emit(ToVector(e.Position)+Vector3.up*.2f,Dust,8,1.4f);Audio.Play("prop-knock",ToVector(e.Position),1,1.1f);break;
    case AbilityKind.Hook:Effects.Spray(chest,forward+Vector3.up*.5f,Steel,6,18,.03f,.2f);Audio.Play("punch-whiff",chest,1,1.3f);break;
    case AbilityKind.Whistle:Effects.Wave(at,Yellow,Abilities.WhistleRadius);Audio.Play("confirm",chest,1,1.9f);break;
    case AbilityKind.Lasso:Effects.Spray(chest,forward,Straw,8,Abilities.LassoRange*2,.03f,.2f);Audio.Play("punch-whiff",chest,1,.9f);break;
    case AbilityKind.BigBubble:Effects.Spray(chest,forward,new Color(.8f,.95f,1,.9f),8,Abilities.BubbleRange*2,.08f,.1f);Audio.Play("pop",chest,1,.6f);break;
    case AbilityKind.IceRay:Effects.Spray(chest,forward,Ice,14,Abilities.IceRange*2.4f,.02f,0);Audio.Play("disc",chest,1,1.8f);break;
    case AbilityKind.WaterCannon:Audio.Play("gust",chest,1,.7f);break;
    case AbilityKind.Smoke:Effects.Wave(at,Smoke,Abilities.SmokeRadius);Effects.Emit(at+Vector3.up*.5f,Smoke,18,3);Audio.Play("gust",at,1,.6f);break;
    case AbilityKind.ZeroG:Effects.Wave(at,Violet,Abilities.ZeroGRadius);Audio.Play("confirm",at,1,.5f);break;
    case AbilityKind.Balloon:Effects.Emit(at+Vector3.up*2.6f,Yellow,8,1.4f,false);Audio.Play("gust",at+Vector3.up*2,1,1.8f);break;
    case AbilityKind.Vaccine:Effects.Emit(chest,Leaf,10,1.6f);Audio.Play("confirm",chest,1,1.3f);break;
   }
  }
 }
}
