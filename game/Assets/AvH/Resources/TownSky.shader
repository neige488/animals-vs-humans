Shader "AvH/TownSky" {
 Properties {
  _Horizon("Horizon",Color)=(.79,.84,.79,1)
  _Zenith("Zenith",Color)=(.3,.56,.75,1)
  _Cloud("Cloud lit",Color)=(1,.98,.94,1)
  _CloudShade("Cloud shade",Color)=(.72,.76,.84,1)
  _SunColor("Sun",Color)=(1,.91,.73,1)
  _SunDir("To sun",Vector)=(-.45,.8,-.3,0)
  _CloudAmount("Cloud amount",Range(0,1))=.45
 }
 SubShader {
  Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
  Cull Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Horizon,_Zenith,_Cloud,_CloudShade,_SunColor;float4 _SunDir;float _CloudAmount;
   struct appdata {float4 vertex:POSITION;};
   struct v2f {float4 pos:SV_POSITION;float3 direction:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;}
   float hash(float2 p){p=frac(p*float2(123.34,456.21));p+=dot(p,p+45.32);return frac(p.x*p.y);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+float2(1,1)),f.x),f.y);}
   float fbm(float2 p){float a=.5,n=0;for(int i=0;i<4;i++){n+=noise(p)*a;p=p*2.03+17.1;a*=.5;}return n;}
   fixed4 frag(v2f i):SV_Target {
    float3 d=normalize(i.direction),s=normalize(_SunDir.xyz);float h=saturate(d.y),toward=saturate(dot(d,s));
    // Gradient from the haze at the horizon up to the zenith; the side of the sky near the sun glows in its colour.
    float3 col=lerp(_Horizon.rgb,_Zenith.rgb,pow(h,.5));
    col=lerp(col,_SunColor.rgb,pow(toward,3)*pow(1-h,3)*.45);
    col+=_SunColor.rgb*(pow(toward,24)*.16+pow(toward,220)*.5+smoothstep(.9992,.9996,toward)*1.6);
    // Drifting clouds on a flat layer overhead, lit on the side that faces the sun.
    float2 p=d.xz/max(.1,d.y+.16)*1.15;p+=_Time.y*float2(.012,.004);
    float n=fbm(p),lit=fbm(p+s.xz*.22);
    // Hard-edged puffs in two tones, to sit with the faceted world below.
    float cover=smoothstep(1-_CloudAmount*.75,1.025-_CloudAmount*.75,n+.32)*smoothstep(.03,.2,h);
    float3 cloud=lerp(_CloudShade.rgb,_Cloud.rgb,smoothstep(.42,.52,(n-lit)*3.5+.55));
    cloud=lerp(cloud,_SunColor.rgb*1.15,pow(toward,6)*.5);
    col=lerp(col,cloud,cover*.9);
    return float4(col,1);
   }
   ENDCG
  }
 }
}
