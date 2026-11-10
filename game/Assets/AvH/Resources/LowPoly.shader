// Matte low-poly surface: a two-tone soft light ramp instead of a smooth falloff, shade filled by the ambient colours,
// and a thin band of the sun's colour along silhouettes (stronger on characters so they read against any wall).
Shader "AvH/LowPoly" {
 Properties {
  _Color("Color",Color)=(1,1,1,1)
  _MainTex("Albedo",2D)="white" {}
  _EmissionColor("Emission",Color)=(0,0,0,1)
  _Rim("Rim light",Range(0,2))=.25
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  LOD 200
  CGPROGRAM
  #pragma surface surf Soft fullforwardshadows
  #pragma target 3.0
  sampler2D _MainTex;fixed4 _Color,_EmissionColor,_AvHRim;float4 _AvHSunDir;half _Rim;
  struct Input {float2 uv_MainTex;float3 viewDir;float3 worldNormal;};
  half4 LightingSoft(SurfaceOutput s,half3 lightDir,half atten) {
   half ndl=dot(s.Normal,lightDir);
   // Lit and shaded faces separate cleanly; a short soft edge keeps round shapes from banding.
   half lit=smoothstep(-.02,.42,ndl)*atten;
   half4 c;c.rgb=s.Albedo*_LightColor0.rgb*lit;c.a=s.Alpha;return c;
  }
  void surf(Input IN,inout SurfaceOutput o) {
   fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;o.Albedo=c.rgb;o.Alpha=1;
   half3 n=normalize(IN.worldNormal);
   half edge=pow(1-saturate(dot(normalize(IN.viewDir),n)),3.2);
   // The band is brightest where the surface turns toward the sun or the sky, and absent underneath.
   half facing=saturate(dot(n,normalize(_AvHSunDir.xyz))*.6+.5)*saturate(n.y*.5+.75);
   o.Emission=_EmissionColor.rgb+_AvHRim.rgb*lerp(c.rgb,half3(1,1,1),.35)*edge*facing*_Rim;
  }
  ENDCG
 }
 Fallback "Diffuse"
}
