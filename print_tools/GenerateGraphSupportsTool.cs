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
    public class GenerateGraphSupportsToolBuilder : BaseCombineInputSOToolBuilder<GenerateGraphSupportsTool>
    {
        public override GenerateGraphSupportsTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new GenerateGraphSupportsTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
        }
    }




    public class GenerateGraphSupportsTool : BaseCombineInputSOTool<GenerateGraphSupportsTool>
    {
        static readonly public string Identifier = "generate_graph_supports";

        override public string Name { get { return "GeneratedGraphSupports"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        public GenerateGraphSupportsTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
            base.ForceSceneSpaceComputation = true;
            set_targets_visibility(true);
        }


        /*
         * These will be called by base class 
         */

        protected override BaseDMeshSourceOp edit_op_factory(DMeshSourceOp meshSourceOp)
        {
            return new GenerateGraphSupportsOp() {
                MeshSource = meshSourceOp
            };
        }


        double MaxDimension = 1.0;
        public override void postprocess_target_objects()
        {
            MaxDimension = base.combineMesh.CachedBounds.MaxDim;
        }

        protected GenerateGraphSupportsOp GenerateSupportsOp {
            get { return base.EditOp as GenerateGraphSupportsOp; }
        }



        public double OverhangAngleDeg {
            get { return get_overhang_angle(); }
            set { set_overhang_angle(value); }
        }
        double get_overhang_angle() { return GenerateSupportsOp.OverhangAngleDeg; }
        void set_overhang_angle(double value) { GenerateSupportsOp.OverhangAngleDeg = value; }


        public double GraphSurfaceOffset {
            get { return get_surface_offset(); }
            set { set_surface_offset(value); }
        }
        double get_surface_offset() { return GenerateSupportsOp.SurfaceOffsetDistance; }
        void set_surface_offset(double value) { GenerateSupportsOp.SurfaceOffsetDistance = value; }



        public double SupportMinAngleDeg {
            get { return get_min_support_angle(); }
            set { set_min_support_angle(value); }
        }
        double get_min_support_angle() { return GenerateSupportsOp.SupportMinAngleDeg; }
        void set_min_support_angle(double value) { GenerateSupportsOp.SupportMinAngleDeg = value; }


        public int OptimizeRounds {
            get { return get_optimize_rounds(); }
            set { set_optimize_rounds(value); }
        }
        int get_optimize_rounds() { return GenerateSupportsOp.OptimizeRounds; }
        void set_optimize_rounds(int value) { GenerateSupportsOp.OptimizeRounds = value; }



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


        public double PostDiameter {
            get { return get_post_diam(); }
            set { set_post_diam(value); }
        }
        double get_post_diam() { return GenerateSupportsOp.PostDiameter; }
        void set_post_diam(double value) { GenerateSupportsOp.PostDiameter = value; }




        public bool BottomUp {
            get { return get_bottom_up(); }
            set { set_bottom_up(value); }
        }
        bool get_bottom_up() { return GenerateSupportsOp.BottomUp; }
        void set_bottom_up(bool value) { GenerateSupportsOp.BottomUp = value; }


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
            Parameters.Register("support_min_angle", get_min_support_angle, set_min_support_angle, 30.0, false);
            Parameters.Register("surface_offset", get_surface_offset, set_surface_offset, 0.0, false);
            Parameters.Register("optimize_rounds", get_optimize_rounds, set_optimize_rounds, 20, false)
                .SetValidRange(0, 1000);
            Parameters.Register("grid_cell_size", get_grid_cell_size, set_grid_cell_size, 1.0, false);
            Parameters.Register("grid_cell_count", get_grid_cell_count, set_grid_cell_count, 128, false)
                .SetValidRange(4, 4096);
            Parameters.Register("post_diameter", get_post_diam, set_post_diam, 1.0, false);
            Parameters.Register("bottom_up", get_bottom_up, set_bottom_up, false, false);
            Parameters.Register("subtract_input", get_subtract_input, set_subtract_input, false, false);
            Parameters.Register("subtract_offset", get_subtract_offset_distance, set_subtract_offset_distance, 0.0, false);
        }

    }


}