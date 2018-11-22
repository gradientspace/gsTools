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
    public class GenerateBlockSupportsToolBuilder : BaseCombineInputSOToolBuilder<GenerateBlockSupportsTool>
    {
        public override GenerateBlockSupportsTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new GenerateBlockSupportsTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
        }
    }




    public class GenerateBlockSupportsTool : BaseCombineInputSOTool<GenerateBlockSupportsTool>
    {
        static readonly public string Identifier = "generate_block_supports";

        override public string Name { get { return "GeneratedBlockSupports"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        public GenerateBlockSupportsTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
            base.ForceSceneSpaceComputation = true;
            set_targets_visibility(true);
        }


        /*
         * These will be called by base class 
         */

        protected override BaseDMeshSourceOp edit_op_factory(DMeshSourceOp meshSourceOp)
        {
            return new GenerateBlockSupportsOp() {
                MeshSource = meshSourceOp
            };
        }


        double MaxDimension = 1.0;
        public override void postprocess_target_objects()
        {
            MaxDimension = base.combineMesh.CachedBounds.MaxDim;
        }

        protected GenerateBlockSupportsOp GenerateSupportsOp {
            get { return base.EditOp as GenerateBlockSupportsOp; }
        }



        public double OverhangAngleDeg {
            get { return get_overhang_angle(); }
            set { set_overhang_angle(value); }
        }
        double get_overhang_angle() { return GenerateSupportsOp.OverhangAngleDeg; }
        void set_overhang_angle(double value) { GenerateSupportsOp.OverhangAngleDeg = value; }


        public double GridCellSize {
            get { return get_grid_cell_size(); }
            set { set_grid_cell_size(value); }
        }
        double get_grid_cell_size() { return GenerateSupportsOp.GridCellSize; }
        void set_grid_cell_size(double value) { GenerateSupportsOp.GridCellSize = value; }


        public int GridCellCount {
            get { return get_grid_cell_count(); }
            set { set_grid_cell_count(value); }
        }
        int get_grid_cell_count() { return (int)(MaxDimension / get_grid_cell_size()); }
        void set_grid_cell_count(int value)
        {
            value = MathUtil.Clamp(value, 3, 8096);
            double cell_size = MaxDimension / (double)value;
            set_grid_cell_size(cell_size);
        }


        public bool SubtractInput {
            get { return get_subtract_input(); }
            set { set_subtract_input(value); }
        }
        bool get_subtract_input() { return GenerateSupportsOp.SubtractInput; }
        void set_subtract_input(bool value) { GenerateSupportsOp.SubtractInput = value; }


        public double SubtractOffsetDistance {
            get { return get_subtract_offset_distance(); }
            set { set_subtract_offset_distance(value); }
        }
        double get_subtract_offset_distance() { return GenerateSupportsOp.SubtractOffsetDistance; }
        void set_subtract_offset_distance(double value) { GenerateSupportsOp.SubtractOffsetDistance = value; }

        protected override void initialize_parameters()
        {
            Parameters.Register("overhang_angle", get_overhang_angle, set_overhang_angle, 30.0, false);
            Parameters.Register("grid_cell_size", get_grid_cell_size, set_grid_cell_size, 1.0, false);
            Parameters.Register("grid_cell_count", get_grid_cell_count, set_grid_cell_count, 128, false)
                .SetValidRange(4, 4096);
            Parameters.Register("subtract_input", get_subtract_input, set_subtract_input, false, false);
            Parameters.Register("subtract_offset", get_subtract_offset_distance, set_subtract_offset_distance, 0.0, false);
        }

    }


}