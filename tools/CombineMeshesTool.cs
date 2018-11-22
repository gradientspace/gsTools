// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using f3;

namespace gs
{
    public class CombineMeshesToolBuilder : BaseCombineInputSOToolBuilder<CombineMeshesTool>
    {
        public override CombineMeshesTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new CombineMeshesTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
        }
    }




    public class CombineMeshesTool : BaseCombineInputSOTool<CombineMeshesTool>
    {
        static readonly public string Identifier = "combine_solids";

        override public string Name { get { return "GeneratedClosedMesh"; } }
        override public string TypeIdentifier { get { return Identifier; } }



        public CombineMeshesTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
        }


        /*
         * These will be called by base class 
         */

        protected override BaseDMeshSourceOp edit_op_factory(DMeshSourceOp meshSourceOp)
        {
            return new CombineMeshesOp() {
                MeshSource = meshSourceOp
            };
        }



        /*
         * Parameters
         */

        protected CombineMeshesOp CombineOp {
            get { return base.EditOp as CombineMeshesOp; }
        }


        public bool OrientNestedShells {
            get { return get_orient_nested(); }
            set { set_orient_nested(value); }
        }
        bool get_orient_nested() { return CombineOp.OrientNestedShells; }
        void set_orient_nested(bool value) { CombineOp.OrientNestedShells = value; }


        protected override void initialize_parameters()
        {
            Parameters.Register("orient_nested", get_orient_nested, set_orient_nested, false, false);
        }


    }

}