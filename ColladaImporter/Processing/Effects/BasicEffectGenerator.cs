﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Omi.Xna.Collada.Model;
using Omi.Xna.Collada.Model.Materials;
using Omi.Xna.Collada.Model.Lighting;
using Microsoft.Xna.Framework;
using System.IO;
using System.Globalization;
using Microsoft.Xna.Framework.Graphics;

namespace Omi.Xna.Collada.Importer.Processing.Effects
{
    using DirectionalLight = Omi.Xna.Collada.Model.Lighting.DirectionalLight;

    public class BasicEffectGenerator : EffectGenerator
    {
        /// <summary>
        /// Creates an effect that fits the given material and the model.
        /// </summary>
        /// <param name="material"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public override EffectDescription CreateEffect(Material material, IntermediateModel model)
        {
            if (material == null || model == null)
            {
                throw new ArgumentNullException(material == null ? "material" : "model");
            }

            // Check for external shaders
            var customShaders = material.Properties.OfType<CustomShader>();
            if (customShaders.Count() == 1)
            {
                return CreateCustomShader(customShaders.First());
            }
            else
            {
                // Otherwise now a effect will be generated
                EffectCode code = new EffectCode(material, model);
                code.Generate();

                return CreateGeneratedShader(code);
            }
        }

        /// <summary>
        /// Generates an ID for given model depending on what features
        /// a shader generated for it needs. Relevant for this are lights,
        /// animation and so on.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        string GenerateModelTypeId(IntermediateModel model)
        {
            string id = "";

            // Ambient Lights ("Omni Light")            
            id += "A" + model.Lights.Count(light => light is AmbientLight);

            // Directional Lights (multiple)            
            id += "D" + model.Lights.Count(light => light is DirectionalLight);
            
            // ... other types of lights (as soon as they are supported)

            // ... other considerations for soon to come hardware skinning support etc.

            return id;
        }

        EffectDescription CreateGeneratedShader(EffectCode code)
        {
            string fxName = "Generic-" + code.Material.GenerateId() + "-" + 
                GenerateModelTypeId(code.Model);
            string filename = fxName + ".fx";

            // Save HLSL code to a .fx file within the content folder
            using (var writer = new StreamWriter(filename, false))
            {                
                writer.Write(code.Code);
            }            

            var desc = new EffectDescription();
            desc.Code = code.Code;
            desc.Name = fxName;
            desc.AddParameters(code.Parameters);
            desc.Filename = filename;

            return desc;
        }

        /// <summary>
        /// Compiles an external custom shader (.fx file) and returns the effect description
        /// containing its name, the compiled code as byte array. The resulting descriptions
        /// does not contain a property collection (null).
        /// </summary>
        /// <param name="shader"></param>
        /// <returns></returns>
        EffectDescription CreateCustomShader(CustomShader shader)
        {
            // 

            return new EffectDescription(shader.Name, shader.Filename, shader.Parameters);            
        }
    }

    /// <summary>
    /// Internal class for generation of HLSL code
    /// </summary>
    internal class EffectCode
    {
        Material material;
        IntermediateModel model;
        StringBuilder sb;        
        Dictionary<String,Object> parameters;

        bool hasDiffuseMap;
        bool hasNormalMap;
        bool hasLight;
        bool hasTexCoords;

        public Material Material { get { return material; } }
        public IntermediateModel Model { get { return model; } }
        public Dictionary<String,Object> Parameters { get { return parameters; } }

        public String Code { get { return sb.ToString(); } }

        /// <summary>
        /// Generates code for an HLSL effect fitting the material and model given.
        /// </summary>
        /// <param name="material"></param>
        /// <param name="model"></param>
        /// <param name="desc"></param>
        public EffectCode(Material material, IntermediateModel model)
        {
            this.material = material;
            this.model = model;            

            sb = new StringBuilder(2000);
            parameters = new Dictionary<String,Object>();

            if (model.Lights.OfType<DirectionalLight>().Any() == false)
            {
                //throw new ApplicationException("No Directional Light defined");
            }
        }

        /// <summary>
        /// Generates the effect code and stores all used parameters
        /// in the "Parameters" List<String>.
        /// </summary>
        public void Generate()
        {
            string name = "Generic-" + material.GenerateId();

            // Global flags
            hasLight = model.Lights.Any();
            hasDiffuseMap = material.Properties.OfType<DiffuseMap>().Any();            
            hasNormalMap = material.Properties.OfType<NormalMap>().Any();
            hasTexCoords = model.Meshes.Any(mesh => mesh.VertexContainers.Any(
                vc => vc.VertexChannels.Any(
                    channel => channel.Description.VertexElementUsage == VertexElementUsage.TextureCoordinate)));

            // Header with general information
            sb.AppendFormat(
@"//--------------------------------------------------------------------------------
// Shader {0}, generated by {1} at {2}
//--------------------------------------------------------------------------------
",
                name, this.GetType().Name, DateTime.Now);

            // Default world, view and projection matrices and camera position
            sb.Append(
@"float4x4 World : WORLD;
float4x4 WorldIT : WORLDINVERSETRANSPOSE;
shared float4x4 View : VIEW;
shared float4x4 Projection : PROJECTION;
shared float3 EyePosition : CAMERAPOSITION; 
");

            if (hasNormalMap)
            {                
                NormalMapType type = material.Properties.OfType<NormalMap>().First().Type;
                if (type == NormalMapType.ParallaxMapping ||
                    type == NormalMapType.ReliefMapping)
                {
                    // Scaling factor for parallax and relief mapping
                    sb.Append("float2 ReliefScale = float2(0.03f, -0.025f);\n");
                }
            }

            parameters.Add("World", null);
            parameters.Add("View", null);
            parameters.Add("Projection", null);
            parameters.Add("EyePosition", null);            
             
            // Shader Parameters
            AppendMaterialParameters();
            AppendModelParameters();

            // Texture samplers
            AppendTextureSamplers();

            // Input Structs
            AppendVertexShaderInput();
            AppendVertexShaderOutput();
            AppendPixelShaderInput();

            // VertexShader
            AppendVertexShader();

            // PixelShader
            AppendPixelShader();
              
            // Techniques and Passes
            AppendTechniques();            
        }

        //=====================================================================
        #region Code Generation

        void AppendTechniques()
        {
            sb.Append(@"
technique BaseTechnique
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
");
        }

        /// <summary>
        /// Adds the pixel shader function "float4 PixelShader(PixelShaderInput in) : COLOR"
        /// </summary>
        void AppendPixelShader()
        {
            bool hasSpecularity = material.Properties.OfType<SpecularPower>().Any();
            bool hasAmbientMaterial = material.Properties.OfType<AmbientColor>().Any();
            bool hasAmbientLight = model.Lights.OfType<AmbientLight>().Any();
            bool hasSpecularMap = material.Properties.OfType<SpecularMap>().Any();
            LightingModel lightingModel = material.Properties.OfType<LightingModel>().FirstOrDefault();
            if (lightingModel == null) lightingModel = new LightingModel();

            sb.Append("\nfloat4 PixelShaderFunction(PixelShaderInput pin) : COLOR\n{\n");

            // Normal Mapping (Dot3 Bump Mapping)
            if (hasNormalMap)
            {
                NormalMapType type = material.Properties.OfType<NormalMap>().First().Type;

                if (type == NormalMapType.DotThreeBumpMapping ||
                    type == NormalMapType.ParallaxMapping)
                {
                    sb.Append("\tfloat4 bump = tex2D(NormalMapSampler, pin.TexCoord);\n");
                    sb.Append("\tfloat3 normalT = normalize((bump.xyz - 0.5f) * 2.0f);\n");
                }

                if (type == NormalMapType.ParallaxMapping)
                {
                    sb.Append("\tfloat3 viewDirT = normalize(pin.ViewDirectionT);\n");
                    sb.Append("\tfloat depth = ReliefScale.x * bump.a + ReliefScale.y;");
                    sb.Append("\tpin.TexCoord = depth * viewDirT + pin.TexCoord;\n");
                }
                else if (type == NormalMapType.ReliefMapping)
                {
                    sb.Append(
@"
    // Relief Mapping
    const int numStepsLinear = 15;
    const int numStepsBinary = 6;
    
    float3 position = float3(pin.TexCoord, 0);
    float3 viewDirT = normalize(pin.ViewDirectionT);
    
    float depthBias = 1.0 - viewDirT.z;
    depthBias *= depthBias;
    depthBias *= depthBias;
    depthBias = 1.0 - depthBias * depthBias;
    viewDirT.xy *= depthBias;
    viewDirT.xy *= ReliefScale;

    // Ray Tracing
    viewDirT /= viewDirT.z * numStepsLinear;
    int i;

    for (i = 0; i < numStepsLinear; i++)
    {
        float4 tex = tex2D(NormalMapSampler, position.xy);
        if (position.z < tex.w)
        {
            position += viewDirT;
        }
    }

    for (i = 0; i < numStepsBinary; i++)
    {
        viewDirT *= 0.5f;
        float4 tex = tex2D(NormalMapSampler, position.xy);
        if (position.z < tex.w)
        {
            position += viewDirT;
        }
        else
        {
            position -= viewDirT;
        }
    }

    pin.TexCoord = position;

    float3 bump = tex2D(NormalMapSampler, pin.TexCoord);
    float3 normalT = (bump - 0.5f) * 2.0f;
    normalT.y = -normalT.y;
    normalT.z = sqrt(1.0 - normalT.x * normalT.x - normalT.y * normalT.y);
"
                        );
                }
            }

            // Ambient Color
            if (material.Properties.OfType<AmbientColor>().Count() == 1)
            {
                // float3 AmbientColor parameter can be used                    
            }
            else if (material.Properties.OfType<AmbientMap>().Count() == 1)
            {
                // TODO: implement ambient maps
                throw new NotImplementedException("Ambient Maps are not implemented yet");
            }
            else
            {                
                sb.Append("\tfloat3 AmbientColor = float3(1, 1, 0);\n");
            }

            // Base color is ambient color of material
            sb.Append("\tfloat3 diffuse = AmbientColor;\n");

            sb.Append("\tfloat3 specular = 0;\n");

            // Diffuse and specular lighting
            if (hasLight)
            {                
                sb.Append("\tfloat3 posToEye = EyePosition - pin.PositionWS.xyz;\n");

                if (hasNormalMap)
                {
                    // use calculated normal from normal map in tangent space
                    sb.Append("\tfloat3 N = normalize(normalT);\n");
                }
                else
                {
                    // use normal from geometry
                    sb.Append("\tfloat3 N = normalize(pin.Normal);\n");   
                }                

                sb.Append("\tfloat3 E = normalize(posToEye);\n");                

                // Add ambient light of the scene, if there is one
                AmbientLight ambientLight = model.Lights.OfType<AmbientLight>().FirstOrDefault();
                if (ambientLight != null)
                {
                    sb.AppendFormat("\tdiffuse *= {0}Color;\n", ambientLight.Name);
                }

                // Directional lights
                if (model.Lights.OfType<DirectionalLight>().Any())
                {
                    // Now add up all directional lights
                    sb.Append("\n\tfloat3 L;\n");
                    sb.Append("\tfloat3 H;\n");
                    sb.Append("\tfloat dt;\n\n");

                    foreach (var dirLight in model.Lights.OfType<DirectionalLight>())
                    {
                        string dirVar = dirLight.Name + "Direction";
                        string colorVar = dirLight.Name + "Color";

                        if (hasNormalMap)
                        {
                            // take calculated tangent direction 
                            dirVar = "pin." + dirLight.Name + "DirT";
                        }

                        sb.AppendFormat("\n\t// Directional Light: {0}\n", dirLight.Name);                        
                        sb.AppendFormat("\tL = -normalize({0});\n", dirVar);                        
                        sb.Append("\tdt = max(0,dot(L,N));\n");                        

                        sb.AppendFormat("\tdiffuse += {0} * dt;\n", colorVar);

                        // Only if specular power of the material is defined the specularity
                        // is calculated, otherwise only diffuse lighting is used
                        if (!hasSpecularity) continue;

                        sb.Append("\tif (dt != 0)\n");

                        // reflect color of light
                        sb.AppendFormat("\t\tspecular += {0}", colorVar);

                        if (hasSpecularMap)
                        {
                            sb.Append(" * tex2D(SpecularMapSampler, pin.TexCoord)");
                        }

                        if (lightingModel.Value == LightingModel.Model.Blinn)
                        {
                            sb.Append(" * pow(max(0.00001f,dot(normalize(E + L),N)), SpecularPower);\n");
                        }
                        else if (lightingModel.Value == LightingModel.Model.Phong)
                        {
                            sb.Append(" * pow(max(0.00001f,(2 * dot(L,N) * dot(N,E) - dot(E,L))), SpecularPower);\n");
                        }
                    }
                }

                // Add Specular Color of material
                if (material.Properties.OfType<SpecularColor>().Any())
                {
                    sb.Append("\tspecular *= SpecularColor;\n");
                }                
            }

            // Emissive Color of material
            if (material.Properties.OfType<EmissiveColor>().Count() > 0)
            {
                sb.Append("\tdiffuse += EmissiveColor;\n");
            }
            else if (material.Properties.OfType<EmissiveMap>().Count() > 0)
            {
                // TODO: implement emissive maps
                throw new NotImplementedException("Emissive Map not yet implemented");
            }

            // Diffuse base color from material (color or texture)
            if (material.Properties.OfType<DiffuseMap>().Count() > 0)
            {
                sb.Append("\tfloat4 finalDiffuse = tex2D(DiffuseMapSampler, pin.TexCoord) * ");
                sb.Append("\tfloat4(diffuse, 1);\n");
            }
            else if (material.Properties.OfType<DiffuseColor>().Count() > 0)
            {
                sb.Append("\tfloat4 finalDiffuse = float4(DiffuseColor * diffuse,1);\n");
            }

            // Transparency
            if (material.Properties.OfType<Opacity>().Count() > 0)
            {
                sb.Append("\tfinalDiffuse.a = Opacity;\n");
            }
            else if (material.Properties.OfType<OpacityMap>().Count() > 0)
            {
                // Take opacity value from texture (opacity map)
                sb.Append("\tfinalDiffuse.a = tex2D(OpacityMapSampler, pin.TexCoord).a;\n");
            }

            sb.Append("\tfloat4 color = finalDiffuse + float4(specular, 0);\n");
            sb.Append("\treturn color;\n}\n");            
        }

        void AppendVertexShader()
        {
            sb.Append("\nVertexShaderOutput VertexShaderFunction(VertexShaderInput vin)\n{\n");
            sb.Append("\tVertexShaderOutput output;\n");

            sb.Append("\tfloat4 pos_ws = mul(vin.Position, World);\n");
            sb.Append("\tfloat4 pos_vs = mul(pos_ws, View);\n");
            sb.Append("\tfloat4 pos_ps = mul(pos_vs, Projection);\n");

            sb.Append("\toutput.PositionPS = pos_ps;\n");
            sb.Append("\toutput.PositionWS = pos_ws;\n");

            // Tangent + Binormal for Normal Mapping
            if (hasNormalMap)
            {
                sb.Append("\toutput.Normal = normalize(mul(vin.Normal.xyz, (float3x3)WorldIT));\n");
                //sb.Append("\toutput.Tangent = normalize(mul(vin.Tangent.xyz, (float3x3)WorldIT));\n");
                //sb.Append("\toutput.Binormal = normalize(cross(output.Normal, output.Tangent));\n");

                sb.Append("\n\tfloat3 Binormal = cross(vin.Tangent, vin.Normal);\n"); 
                sb.Append("\tfloat3x3 tangentToObject;\n");
                sb.Append("\ttangentToObject[0] = normalize(Binormal);\n");
                sb.Append("\ttangentToObject[1] = normalize(vin.Tangent);\n");
                sb.Append("\ttangentToObject[2] = normalize(vin.Normal);\n");
                sb.Append("\tfloat3x3 tangentToWorld = mul(tangentToObject, World);\n\n");

                // Light directions to tangent space
                foreach (var light in model.Lights.OfType<DirectionalLight>())
                {
                    sb.AppendFormat("\toutput.{0}DirT = mul(tangentToWorld, {0}Direction);\n", 
                        light.Name);
                }

                var normalMapType = material.Properties.OfType<NormalMap>().First().Type;
                if (normalMapType == NormalMapType.ParallaxMapping ||
                    normalMapType == NormalMapType.ReliefMapping)
                {
                    sb.Append("\toutput.ViewDirectionT = output.PositionWS - EyePosition;\n");
                    sb.Append("\toutput.ViewDirectionT = mul(tangentToWorld, output.ViewDirectionT);\n");
                }
            }
            // Normal vector
            else if (hasLight)
            {
                sb.Append("\toutput.Normal = normalize(mul(vin.Normal.xyz, (float3x3)WorldIT));\n");
            }            

            // Texture Coordinates
            if (material.Properties.Any(p => p is TextureProperty) && hasTexCoords)
            {
                sb.Append("\toutput.TexCoord = vin.TexCoord;\n");
            }

            sb.Append("\treturn output;\n};\n");
        }

        /// <summary>
        /// Appends the input shader struct (which is used as vertex shader output at
        /// the same time) definition "struct PixelShaderInput { ... }" with all 
        /// necessary inputs.
        /// </summary>
        void AppendPixelShaderInput()
        {
            int texCoordIndex = 0;

            sb.Append("struct PixelShaderInput\n{\n");            

            // Texture coordinates for diffuse map (texture)
            if (material.Properties.Any(p => p is TextureProperty) && hasTexCoords)
            {
                sb.AppendFormat("\tfloat2 TexCoord : TEXCOORD{0};\n", texCoordIndex++);
            }
            
            sb.AppendFormat("\tfloat4 PositionWS : TEXCOORD{0}; // Position in World Space\n",
                texCoordIndex++);

            // If there are lights, the shader needs normals
            if (hasLight)
            {
                sb.Append("\tfloat3 Normal : NORMAL;\n");
            }

            if (hasNormalMap)
            {
                //sb.Append("\tfloat3 Tangent : TANGENT;\n");
                //sb.Append("\tfloat3 Binormal : BINORMAL;\n");

                // Inputs for light directions (world to tangent space)
                foreach (var dirLight in model.Lights.OfType<DirectionalLight>())
                {
                    sb.AppendFormat("\tfloat3 {0}DirT : TEXCOORD{1};\n", dirLight.Name, texCoordIndex++);
                }

                var normalMapType = material.Properties.OfType<NormalMap>().First().Type;
                if (normalMapType == NormalMapType.ParallaxMapping ||
                    normalMapType == NormalMapType.ReliefMapping)
                {
                    sb.AppendFormat("\tfloat3 ViewDirectionT : TEXCOORD{0};\n", texCoordIndex++);
                }
            }

            sb.Append("};\n");
        }

        void AppendVertexShaderOutput()
        {
            int texCoordIndex = 0;

            sb.Append("struct VertexShaderOutput\n{\n");

            // Texture coordinates for diffuse map (texture)
            if (material.Properties.Any(p => p is TextureProperty) && hasTexCoords)
            {
                sb.AppendFormat("\tfloat2 TexCoord : TEXCOORD{0};\n", texCoordIndex++);
            }

            sb.Append("\tfloat4 PositionPS : POSITION; // Position in Projection Space\n");
            sb.AppendFormat("\tfloat4 PositionWS : TEXCOORD{0}; // Position in World Space\n", 
                texCoordIndex++);            

            // If there are lights, the shader needs normals
            if (hasLight)
            {
                sb.Append("\tfloat3 Normal : NORMAL;\n");
            }

            if (hasNormalMap)
            {
                //sb.Append("\tfloat3 Tangent : TANGENT;\n");
                //sb.Append("\tfloat3 Binormal : BINORMAL;\n");

                // Outputs for light directions (world to tangent space)
                foreach (var dirLight in model.Lights.OfType<DirectionalLight>())
                {
                    sb.AppendFormat("\tfloat3 {0}DirT : TEXCOORD{1};\n", dirLight.Name, texCoordIndex++);
                }

                var normalMapType = material.Properties.OfType<NormalMap>().First().Type;
                if (normalMapType == NormalMapType.ParallaxMapping ||
                    normalMapType == NormalMapType.ReliefMapping)
                {
                    sb.AppendFormat("\tfloat3 ViewDirectionT : TEXCOORD{0};\n", texCoordIndex++);
                }
            }

            sb.Append("};\n");
        }

        /// <summary>
        /// Appends the vertex shader input struct definition "struct VertexShaderInput { ... }"
        /// with all necessary inputs.
        /// </summary>
        void AppendVertexShaderInput()
        {
            sb.Append("struct VertexShaderInput\n{\n");
            sb.Append("\tfloat4 Position : POSITION;\n");

            // Texture Coordinates for diffuse map (texture)
            if (material.Properties.Any(p => p is TextureProperty) && hasTexCoords)
            {
                sb.Append("\tfloat2 TexCoord : TEXCOORD0;\n");
            }

            // If there are lights, the shader needs normals
            if (hasLight)
            {
                sb.Append("\tfloat3 Normal : NORMAL;\n");
            }

            // If there is a normal map, the shader needs tangents
            if (hasNormalMap)
            {
                sb.Append("\tfloat3 Tangent : TANGENT;\n");
            }

            // TODO: Add vertex shader input for animations

            sb.Append("};\n");
        }

        /// <summary>
        /// Append model dependant parameters, e.g. for animation, light
        /// </summary>
        void AppendModelParameters()
        {
            // Directional Light Inputs            
            foreach (DirectionalLight dirLight in model.Lights.OfType<DirectionalLight>())
            {
                string name = dirLight.Name;

                Vector3 color = dirLight.Color.ToVector3();

                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "float3 {0}Color = float3({1}, {2}, {3});\n", name,
                    color.X, color.Y, color.Z);

                sb.AppendFormat(CultureInfo.InvariantCulture, 
                    "float3 {0}Direction = float3({1}, {2}, {3});\n", name,
                    dirLight.Direction.X, dirLight.Direction.Y, dirLight.Direction.Z);

                parameters.Add(name + "Color", color);
                parameters.Add(name + "Direction", dirLight.Direction);
            }

            // Ambient Light (only one supported)
            AmbientLight ambientLight = model.Lights.OfType<AmbientLight>().FirstOrDefault();
            if (ambientLight != null)
            {
                Vector3 color = ambientLight.Color.ToVector3();

                sb.AppendFormat(CultureInfo.InvariantCulture, 
                    "float3 {0}Color = float3({1}, {2}, {3});\n",
                    ambientLight.Name, color.X, color.Y, color.Z);

                parameters.Add(ambientLight.Name + "Color", color);
            }
        }

        /// <summary>
        /// Appends all global (uniform) variables according to the material
        /// definition
        /// </summary>
        void AppendMaterialParameters()
        {
            sb.Append(
@"//--------------------------------------------------------------------------------
// Shader Parameters - Material
//-------------------------------------------------------------------------------
");

            // 1. Material parameters
            foreach (var property in material.Properties)
            {
                if (property.ShaderInstructions == null ||
                    property.ShaderInstructions.ParameterType == null) continue;

                IShaderDefaultValue def = property as IShaderDefaultValue;
                if (def != null)
                {
                    sb.AppendFormat("{0} {1} = {2};\n", 
                        property.ShaderInstructions.ParameterType, property.Name,
                        def.ShaderDefaultValue);
                }
                else
                {
                    sb.AppendFormat("{0} {1};\n", property.ShaderInstructions.ParameterType,
                    property.Name);
                }                

                Object value = null;

                if (property is ColorProperty) value = (property as ColorProperty).Color;
                else if (property is ValueProperty) value = (property as ValueProperty).Value;
                else if (property is TextureProperty) value = (property as TextureProperty).Texture;

                parameters.Add(property.Name, value);
            }
        }

        /// <summary>
        /// Creates a texture sampler for every texture property. The name
        /// is MaterialProperty.Name + "Sampler", e.g. "AmbientMapSampler".
        /// </summary>
        void AppendTextureSamplers()
        {
            sb.Append(
@"//--------------------------------------------------------------------------------
// Texture Samplers
//--------------------------------------------------------------------------------
");

            var properties = material.Properties.OfType<TextureProperty>();

            foreach (var property in properties)
            {
                // TODO: Allow custom sampler_state options 
                if (property is NormalMap)
                {
                    sb.AppendFormat(
@"sampler {0}Sampler = sampler_state
{{
    texture = <{0}>;
    magfilter = {1};
    minfilter = {2};
    mipfilter = {3};
}};
", property.Name, "LINEAR", "LINEAR", "LINEAR");
                }
                else
                {
                    sb.AppendFormat(
                        @"sampler {0}Sampler = sampler_state
{{
    texture = <{0}>;
    magfilter = {1};
    minfilter = {2};
    mipfilter = {3};
    AddressU = {4};
    AddressV = {5};
}};
",
                        property.Name, "LINEAR", "LINEAR", "LINEAR", "wrap", "wrap");
                }
            }
        }

        #endregion 
    }
}
