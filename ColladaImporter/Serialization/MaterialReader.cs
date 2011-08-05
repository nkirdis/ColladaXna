using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Seafarer.Xna.Collada.Importer.Materials;

namespace Seafarer.Xna.Collada.Importer.Serialization
{
    using EffectMaterial = Seafarer.Xna.Collada.Importer.Materials.EffectMaterial;

    /// <summary>
    /// Reads a material and creates an Effect from it.
    /// </summary>
    public class MaterialReader : ContentTypeReader<EffectMaterial>
    {
        protected override EffectMaterial Read(ContentReader input, EffectMaterial existingInstance)
        {
            // Name of the material as referenced by meshes
            string name = input.ReadString();

            // Properties of the material
            List<MaterialProperty> properties = input.ReadObject<List<MaterialProperty>>();            

            Material material = new Material(name, properties);

            Effect effect = input.ReadExternalReference<Effect>();
            List<String> parameters = input.ReadObject<List<String>>();

            // Create copy of effect and set individual parameters
            effect = effect.Clone();

            // set textures
            var textureProperties = from p in material.Properties
                                    where p is TextureProperty
                                    select p as TextureProperty;

            foreach (var property in textureProperties)
            {
                string paramName = property.Name;
                var reference = property.Texture as LoadedTextureReference;

                effect.Parameters[paramName].SetValue(reference.Texture);

                if (property is NormalMap)
                {
                    NormalMap normalMap = property as NormalMap;
                    if (normalMap.Type == NormalMapType.ParallaxMapping ||
                        normalMap.Type == NormalMapType.ReliefMapping)
                    {
                        effect.Parameters["ReliefScale"].SetValue(normalMap.ParallaxScale);
                    }
                }
            }

            // set default values for other parameters
            foreach (var colorProperty in material.Properties.OfType<ColorProperty>())
            {
                if (colorProperty.ShaderInstructions.ParameterType == "float3")
                {
                    effect.Parameters[colorProperty.Name].SetValue(colorProperty.Color.ToVector3());
                }
                else
                {
                    effect.Parameters[colorProperty.Name].SetValue(colorProperty.Color.ToVector4());
                }
            }

            foreach (var valueProperty in material.Properties.OfType<ValueProperty>())
            {
                effect.Parameters[valueProperty.Name].SetValue(valueProperty.Value);
            }
            
            // Test BasicEffect for comparison
            //BasicEffect test = new BasicEffect(GetGraphicsDevice(input), null);
            //test.TextureEnabled = true;
            //test.Texture = (material.Properties.OfType<DiffuseMap>().First().Texture as LoadedTextureReference).Texture;
            //test.EnableDefaultLighting();
            //test.PreferPerPixelLighting = true;            

            //effect = test;

            return new EffectMaterial(name, effect, parameters, material);
        }

        GraphicsDevice GetGraphicsDevice(ContentReader input)
        {
            IGraphicsDeviceService graphicsDeviceService =
                (IGraphicsDeviceService)input.ContentManager.ServiceProvider.
                GetService(typeof(IGraphicsDeviceService));
            return graphicsDeviceService.GraphicsDevice;
        }
    }
}