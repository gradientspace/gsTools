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
    public class MeshVoxelBlendToolBuilder : BaseNAryInputSOToolBuilder<MeshVoxelBlendTool>
    {
        public override MeshVoxelBlendTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new MeshVoxelBlendTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
        }
    }




    public class MeshVoxelBlendTool : BaseNAryInputSOTool<MeshVoxelBlendTool>
    {
        static readonly public string Identifier = "mesh_voxel_blend";

        override public string Name { get { return "MeshVoxelBlend"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        public MeshVoxelBlendTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
        }


        /*
         * These will be called by base class 
         */

        protected override BaseDMeshSourceOp edit_op_factory(IEnumerable<DMeshSourceOp> meshSourceOps)
        {
            MeshVoxelBlendOp op = new MeshVoxelBlendOp();
            op.SetSources(new List<DMeshSourceOp>(meshSourceOps));

            double largest_dim = 0;
            foreach ( DMesh3 m in base.SceneMeshes )
                largest_dim = Math.Max(largest_dim, m.CachedBounds.MaxDim);

            // [RMS] this is helpful for full-field blend...
            //double target_cell_size = Math.Max(1, Math.Round(largest_dim / 64, 0)+1);
            //op.MeshCellSize = target_d;
            //op.GridCellSize = target_cell_size;

            op.GridCellSize = 1.0;

            return op;
        }


        double MaxDimension = 1.0;
        public override void postprocess_target_objects()
        {
            AxisAlignedBox3d bounds = SceneMeshes[0].CachedBounds;
            for (int k = 1; k < InputSOs.Count; ++k)
                bounds.Contain(SceneMeshes[k].CachedBounds);
            MaxDimension = bounds.MaxDim;

            double cell_size = ToolDefaults.DefaultVoxelSceneSizeF(new AxisAlignedBox3d(MaxDimension, MaxDimension, MaxDimension)); ;
            set_grid_cell_size(cell_size);
            set_mesh_cell_size(cell_size);
            set_blend_distance(5 * cell_size);
            set_min_comp_size(2.0);
        }

        protected MeshVoxelBlendOp BlendOp {
            get { return base.EditOp as MeshVoxelBlendOp; }
        }




        public double GridCellSize {
            get { return get_grid_cell_size(); }
            set { set_grid_cell_size(value); }
        }
        double get_grid_cell_size() { return BlendOp.GridCellSize; }
        void set_grid_cell_size(double value) { BlendOp.GridCellSize = value; }



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



        public double MeshCellSize {
            get { return get_mesh_cell_size(); }
            set { set_mesh_cell_size(value); }
        }
        double get_mesh_cell_size() { return BlendOp.MeshCellSize; }
        void set_mesh_cell_size(double value) { BlendOp.MeshCellSize = value; }


        public int MeshCellCount {
            get { return get_mesh_cell_count(); }
            set { set_mesh_cell_count(value); }
        }
        int get_mesh_cell_count() { return (int)(MaxDimension / get_mesh_cell_size()); }
        void set_mesh_cell_count(int value)
        {
            value = MathUtil.Clamp(value, 3, 8096);
            double cell_size = MaxDimension / (double)value;
            set_mesh_cell_size(cell_size);
        }


        double get_all_cell_size() { return BlendOp.GridCellSize; }
        void set_all_cell_size(double value) { BlendOp.GridCellSize = value; BlendOp.MeshCellSize = value; }



        public double BlendPower {
            get { return get_blend_power(); }
            set { set_blend_power(value); }
        }
        double get_blend_power() { return BlendOp.BlendPower; }
        void set_blend_power(double value) { BlendOp.BlendPower = value; }


        public double BlendDistance {
            get { return get_blend_distance(); }
            set { set_blend_distance(value); }
        }
        double get_blend_distance() { return BlendOp.BlendFalloff; }
        void set_blend_distance(double value) { BlendOp.BlendFalloff = value; }


        public double MinComponentSize {
            get { return get_min_comp_size(); }
            set { set_min_comp_size(value); }
        }
        double scene_min_comp_size = 1.0;
        double get_min_comp_size() { return scene_min_comp_size; }
        void set_min_comp_size(double value) {
            scene_min_comp_size = value;
            double dim = scene_min_comp_size * base.sceneToObjUnitScale;
            BlendOp.MinComponentVolume = dim * dim * dim;
        }



        protected override void initialize_parameters()
        {
            Parameters.Register("grid_cell_size", get_grid_cell_size, set_grid_cell_size, 1.0, false);
            Parameters.Register("grid_cell_count", get_grid_cell_count, set_grid_cell_count, 128, false)
                .SetValidRange(4, 4096);
            Parameters.Register("mesh_cell_size", get_mesh_cell_size, set_mesh_cell_size, 1.0, false);
            Parameters.Register("mesh_cell_count", get_mesh_cell_count, set_mesh_cell_count, 128, false)
                .SetValidRange(4, 4096);
            Parameters.Register("all_cell_size", get_all_cell_size, set_all_cell_size, 1.0, true);
            Parameters.Register("blend_power", get_blend_power, set_blend_power, 1.0, true)
                .SetValidRange(0.001, 10000);
            Parameters.Register("blend_distance", get_blend_distance, set_blend_distance, 1.0, true)
                .SetValidRange(0.001, 10000);

            Parameters.Register("min_component_size", get_min_comp_size, set_min_comp_size, 2.0, false)
                .SetValidRange(0, 9999);
        }

    }


}