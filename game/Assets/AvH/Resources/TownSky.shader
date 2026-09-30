Shader "AvH/TownSky" {
 SubShader {
  Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
  Cull Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;};
   struct v2f {float4 pos:SV_POSITION;float3 direction:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;}
   fixed4 frag(v2f i):SV_Target {
    float3 d=normalize(i.direction);float h=saturate(d.y);
    float3 col=lerp(float3(.79,.84,.79),float3(.3,.56,.75),pow(h,.55));
    float2 p=d.xz/max(.12,d.y+.2);p.x+=_Time.y*.008;
    float n=sin(p.x*2.3+sin(p.y*1.3))*.5+sin(p.y*3.1+p.x*.8)*.25+sin(p.x*5.4-p.y*2)*.12;
    float cloud=smoothstep(.45,.7,n)*smoothstep(.06,.3,h)*.55;
    col=lerp(col,float3(.96,.95,.86),cloud);
    float sun=pow(saturate(dot(d,normalize(float3(-.45,.8,-.3)))),480);
    return float4(col+sun*float3(.8,.65,.36),1);
   }
   ENDCG
  }
 }
}
