﻿using System;
using System.Collections.Generic;
using Omi.Xna.Collada.Model.Materials;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;

namespace Omi.Xna.Collada.Importer.Data
{
    /// <summary>
    /// A material that was compiled to XNB by the XNA content pipeline.
    /// Contains an external reference pointing to the
    /// XNB file that contains the compiled effect corresponding to 
    /// this material.
    /// </summary>
    public class CompiledMaterial : Material
    {
        /// <summary>
        /// List of parameters used by the effect 
        /// (e.g. World, View, Projection)
        /// </summary>
        public Dictionary<String,Object> EffectParameters;

        /// <summary>
        /// Reference pointing to the XNB file that contains 
        /// the compiled effect.
        /// </summary>
        public ExternalReference<CompiledEffectContent> Effect;

        /// <summary>
        /// Creates a new compiled material.
        /// </summary>
        /// <param name="material"></param>
        public CompiledMaterial(Material material)
            : base(material.Name, material.Properties)
        {
            
        }
    }
}
