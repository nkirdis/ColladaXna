using System;
using System.Collections.Generic;
using System.Linq;
using Omi.Xna.Collada.Model.Animation;
using Omi.Xna.Collada.Model.Geometry;
using Omi.Xna.Collada.Model.Lighting;
using Omi.Xna.Collada.Model.Materials;

namespace Omi.Xna.Collada.Model
{
    /// <summary>
    /// Union of data necessary to describe a COLLADA model
    /// that is used during the import and processing stages,
    /// but at runtime.
    /// </summary>
    public class IntermediateModel
    {
        /// <summary>
        /// Name of the COLLADA file that this model is based on
        /// </summary>
        public String SourceFilename;

        /// <summary>
        /// Collection of different meshes (geometry only)
        /// </summary>
        public List<Mesh> Meshes = new List<Mesh>();

        /// <summary>
        /// Collection of individual mesh instances whose
        /// geometry is based on one of the meshes (see Model.Meshes)
        /// but who have different values for transformation etc.
        /// </summary>
        public List<MeshInstance> MeshInstances = new List<MeshInstance>();

        /// <summary>
        /// Collection of all used materials
        /// </summary>
        public List<Material> Materials = new List<Material>();

        /// <summary>
        /// Lighting information
        /// </summary>
        public List<Light> Lights = new List<Light>();

        /// <summary>
        /// Collection of all joints
        /// </summary>
        public JointList Joints = new JointList();        

        /// <summary>
        /// List of joint animations for skeletal / skinned animation
        /// </summary>
        public JointAnimationList JointAnimations = new JointAnimationList();

        /// <summary>
        /// Returns the Root joint of the model, which by convention is the last joint
        /// in the Joints collection.
        /// </summary>
        public Joint RootJoint
        {
            get
            {
                if (Joints != null && Joints.Any())
                {
                    return Joints[Joints.Count - 1];
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
