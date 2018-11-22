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
    public class WeldEdgesToolBuilder : BaseCombineInputSOToolBuilder<WeldEdgesTool>
    {
        public override WeldEdgesTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new WeldEdgesTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
        }
    }




    public class WeldEdgesTool : BaseCombineInputSOTool<WeldEdgesTool>
    {
        static readonly public string Identifier = "weld_edges";

        override public string Name { get { return "WeldEdges"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        public WeldEdgesTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
        }


        /*
         * These will be called by base class 
         */

        public override void postprocess_target_objects()
        {
        }

        protected override BaseDMeshSourceOp edit_op_factory(DMeshSourceOp meshSourceOp)
        {
            return new WeldEdgesOp() {
                MeshSource = meshSourceOp
            };
        }

        protected WeldEdgesOp WeldOp {
            get { return base.EditOp as WeldEdgesOp; }
        }


        public bool OnlyUniquePairs {
            get { return get_only_unique_pairs(); }
            set { set_only_unique_pairs(value); }
        }
        bool get_only_unique_pairs() { return WeldOp.OnlyUniquePairs; }
        void set_only_unique_pairs(bool value) { WeldOp.OnlyUniquePairs = value; }


        public double MergeTolerance {
            get { return get_merge_tolerance(); }
            set { set_merge_tolerance(value); }
        }
        double get_merge_tolerance() { return WeldOp.MergeTolerance; }
        void set_merge_tolerance(double value) { WeldOp.MergeTolerance = value; }


        protected override void initialize_parameters()
        {
            Parameters.Register("only_unique_pairs", get_only_unique_pairs, set_only_unique_pairs, false, false);
            Parameters.Register("merge_tolerance", get_merge_tolerance, set_merge_tolerance, MathUtil.ZeroTolerance, false);
        }

    }


}