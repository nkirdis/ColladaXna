//--------------------------------------------------------------------------------
// Shader Generic-AcBlinnEcNpOpScSpTx, generated by EffectCode at 05.08.2011 20:49:52
//--------------------------------------------------------------------------------
float4x4 World : WORLD;
float4x4 WorldIT : WORLDINVERSETRANSPOSE;
shared float4x4 View : VIEW;
shared float4x4 Projection : PROJECTION;
shared float3 EyePosition : CAMERAPOSITION; 
float2 ReliefScale = float2(0.03f, -0.025f);
//--------------------------------------------------------------------------------
// Shader Parameters - Material
//-------------------------------------------------------------------------------
float3 EmissiveColor = float3(0,0,0);
float3 AmbientColor = float3(0.5882353,0.5882353,0.5882353);
Texture DiffuseMap;
float3 SpecularColor = float3(0.9019608,0.9019608,0.9019608);
float SpecularPower = 10;
float Opacity = 1;
Texture NormalMap;
float3 DirLight1Color = float3(1, 1, 0.9019608);
float3 DirLight1Direction = float3(0, -0.5144958, 0.8574929);
float3 DirLight2Color = float3(0.2980392, 0.2980392, 0.4);
float3 DirLight2Direction = float3(-0.7071068, 0.7071068, 0);
float3 DirLight3Color = float3(0.1019608, 0.1019608, 0.1019608);
float3 DirLight3Direction = float3(0, 0, -1);
float3 AmbientLightColor = float3(0, 0, 0);
//--------------------------------------------------------------------------------
// Texture Samplers
//--------------------------------------------------------------------------------
sampler DiffuseMapSampler = sampler_state
{
    texture = <DiffuseMap>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};
sampler NormalMapSampler = sampler_state
{
    texture = <NormalMap>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
};
struct VertexShaderInput
{
	float4 Position : POSITION;
	float2 TexCoord : TEXCOORD0;
	float3 Normal : NORMAL;
	float3 Tangent : TANGENT;
};
struct VertexShaderOutput
{
	float2 TexCoord : TEXCOORD0;
	float4 PositionPS : POSITION; // Position in Projection Space
	float4 PositionWS : TEXCOORD1; // Position in World Space
	float3 Normal : NORMAL;
	float3 DirLight1DirT : TEXCOORD2;
	float3 DirLight2DirT : TEXCOORD3;
	float3 DirLight3DirT : TEXCOORD4;
	float3 ViewDirectionT : TEXCOORD5;
};
struct PixelShaderInput
{
	float2 TexCoord : TEXCOORD0;
	float4 PositionWS : TEXCOORD1; // Position in World Space
	float3 Normal : NORMAL;
	float3 DirLight1DirT : TEXCOORD2;
	float3 DirLight2DirT : TEXCOORD3;
	float3 DirLight3DirT : TEXCOORD4;
	float3 ViewDirectionT : TEXCOORD5;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput vin)
{
	VertexShaderOutput output;
	float4 pos_ws = mul(vin.Position, World);
	float4 pos_vs = mul(pos_ws, View);
	float4 pos_ps = mul(pos_vs, Projection);
	output.PositionPS = pos_ps;
	output.PositionWS = pos_ws;
	output.Normal = normalize(mul(vin.Normal.xyz, (float3x3)WorldIT));

	float3 Binormal = cross(vin.Tangent, vin.Normal);
	float3x3 tangentToObject;
	tangentToObject[0] = normalize(Binormal);
	tangentToObject[1] = normalize(vin.Tangent);
	tangentToObject[2] = normalize(vin.Normal);
	float3x3 tangentToWorld = mul(tangentToObject, World);

	output.DirLight1DirT = mul(tangentToWorld, DirLight1Direction);
	output.DirLight2DirT = mul(tangentToWorld, DirLight2Direction);
	output.DirLight3DirT = mul(tangentToWorld, DirLight3Direction);
	output.ViewDirectionT = output.PositionWS - EyePosition;
	output.ViewDirectionT = mul(tangentToWorld, output.ViewDirectionT);
	output.TexCoord = vin.TexCoord;
	return output;
};

float4 PixelShaderFunction(PixelShaderInput pin) : COLOR
{
	float4 bump = tex2D(NormalMapSampler, pin.TexCoord);
	float3 normalT = normalize((bump.xyz - 0.5f) * 2.0f);
	float3 viewDirT = normalize(pin.ViewDirectionT);
	float depth = ReliefScale.x * bump.a + ReliefScale.y;	pin.TexCoord = depth * viewDirT + pin.TexCoord;
	float3 diffuse = AmbientColor;
	float3 specular = 0;
	float3 posToEye = EyePosition - pin.PositionWS.xyz;
	float3 N = normalize(normalT);
	float3 E = normalize(posToEye);
	diffuse *= AmbientLightColor;

	float3 L;
	float3 H;
	float dt;


	// Directional Light: DirLight1
	L = -normalize(pin.DirLight1DirT);
	dt = max(0,dot(L,N));
	diffuse += DirLight1Color * dt;
	if (dt != 0)
		specular += DirLight1Color * pow(max(0.00001f,dot(normalize(E + L),N)), SpecularPower);

	// Directional Light: DirLight2
	L = -normalize(pin.DirLight2DirT);
	dt = max(0,dot(L,N));
	diffuse += DirLight2Color * dt;
	if (dt != 0)
		specular += DirLight2Color * pow(max(0.00001f,dot(normalize(E + L),N)), SpecularPower);

	// Directional Light: DirLight3
	L = -normalize(pin.DirLight3DirT);
	dt = max(0,dot(L,N));
	diffuse += DirLight3Color * dt;
	if (dt != 0)
		specular += DirLight3Color * pow(max(0.00001f,dot(normalize(E + L),N)), SpecularPower);
	specular *= SpecularColor;
	diffuse += EmissiveColor;
	float4 finalDiffuse = tex2D(DiffuseMapSampler, pin.TexCoord) * 	float4(diffuse, 1);
	finalDiffuse.a = Opacity;
	float4 color = finalDiffuse + float4(specular, 0);
	return color;
}

technique BaseTechnique
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
