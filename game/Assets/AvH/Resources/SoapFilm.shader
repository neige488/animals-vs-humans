Shader "AvH/SoapFilm" {
 Properties { _Tint("Tint",Color)=(.5,.85,1,1) }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off
  Cull Back
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;};
   struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;};
   float4 _Tint;
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
   fixed4 frag(v2f i):SV_Target {
    float3 n=normalize(i.normal),v=normalize(_WorldSpaceCameraPos-i.world);
    float facing=saturate(dot(n,v)),rim=pow(1-facing,2.4);
    float film=(1-facing)*9+n.y*1.8+_Time.y*.55;
    float3 rainbow=.5+.5*cos(film+float3(0,2.094,4.188));
    float spec=pow(saturate(dot(n,normalize(v+float3(-.5,.8,-.35)))),100);
    float broad=pow(saturate(dot(n,normalize(v+float3(.7,1,.2)))),18)*.25;
    float3 col=lerp(float3(.58,.83,.91),rainbow,.7)*(.55+rim*.6)+spec*1.8+broad;
    return float4(col*_Tint.rgb, saturate(.035+rim*.66+spec*.8+broad*.25)*_Tint.a);
   }
   ENDCG
  }
 }
}
