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
    public class RepairOrientationToolBuilder : BaseCombineInputSOToolBuilder<RepairOrientationTool>
    {
        public override RepairOrientationTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new RepairOrientationTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
        }
    }


    public class RepairOrientationTool : BaseCombineInputSOTool<RepairOrientationTool>
    {
        static readonly public string Identifier = "repair_orientation";

        override public string Name { get { return "RepairOrientation"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        public RepairOrientationTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
        }


        /*
         * These will be called by base class 
         */


        protected override BaseDMeshSourceOp edit_op_factory(DMeshSourceOp meshSourceOp) {
            return new RepairOrientationOp() {
                MeshSource = meshSourceOp
            };
        }


        public override void postprocess_target_objects()
        {
        }

        protected RepairOrientationOp RepairOp {
            get { return base.EditOp as RepairOrientationOp; }
        }


        public bool InvertResult {
            get { return get_invert_result(); }
            set { set_invert_result(value); }
        }
        bool get_invert_result() { return RepairOp.InvertResult; }
        void set_invert_result(bool value) { RepairOp.InvertResult = value; }



        protected override void initialize_parameters()
        {
            Parameters.Register("invert_result", get_invert_result, set_invert_result, false, false);
        }

    }

}