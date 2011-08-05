﻿using System.Linq;
using Omi.Xna.Collada.Model;
using Omi.Xna.Collada.Model.Materials;

namespace Omi.Xna.Collada.Importer.Processing
{
    /// <summary>
    /// The material processor processes all materials by building all referenced textures
    /// and creating a ProcessedMaterial from each Material containing a reference to the
    /// built assets.
    /// </summary>
    public abstract class MaterialProcessor : IColladaProcessor
    {
        #region IColladaProcessor Member

        public IntermediateModel Process(IntermediateModel model, ProcessingOptions options)
        {
            model.Materials = (from material in model.Materials 
                               select ProcessMaterial(material, model, options)).ToList();            

            return model;
        }

        #endregion

        #region Material Processing        

        protected abstract Material ProcessMaterial(Material material, IntermediateModel model,
            ProcessingOptions options);        

        #endregion
    }
}