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
    public class MeshVoxelBooleanToolBuilder : BaseNAryInputSOToolBuilder<MeshVoxelBooleanTool>
    {
        public override MeshVoxelBooleanTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new MeshVoxelBooleanTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
        }
    }




    public class MeshVoxelBooleanTool : BaseNAryInputSOTool<MeshVoxelBooleanTool>
    {
        static readonly public string Identifier = "mesh_voxel_boolean";

        override public string Name { get { return "MeshVoxelBoolean"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        public MeshVoxelBooleanTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
        }


        /*
         * These will be called by base class 
         */

        protected override BaseDMeshSourceOp edit_op_factory(IEnumerable<DMeshSourceOp> meshSourceOps)
        {
            MeshVoxelBooleanOp op = new MeshVoxelBooleanOp();
            op.SetSources(new List<DMeshSourceOp>(meshSourceOps));
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
            set_min_comp_size(2.0);
        }

        protected MeshVoxelBooleanOp BooleanOp {
            get { return base.EditOp as MeshVoxelBooleanOp; }
        }



        public enum OpTypes
        {
            Union = MeshVoxelBooleanOp.OpTypes.Union,
            Intersection = MeshVoxelBooleanOp.OpTypes.Intersection,
            Difference = MeshVoxelBooleanOp.OpTypes.Difference
        }
        public OpTypes OpType {
            get { return (OpTypes)get_op_type_int(); }
            set { set_op_type_int((int)value); }
        }
        int get_op_type_int() { return (int)BooleanOp.OpType; }
        void set_op_type_int(int value) { BooleanOp.OpType = (MeshVoxelBooleanOp.OpTypes)value; }



        public double GridCellSize {
            get { return get_grid_cell_size(); }
            set { set_grid_cell_size(value); }
        }
        double get_grid_cell_size() { return BooleanOp.GridCellSize; }
        void set_grid_cell_size(double value) { BooleanOp.GridCellSize = value; }



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
        double get_mesh_cell_size() { return BooleanOp.MeshCellSize; }
        void set_mesh_cell_size(double value) { BooleanOp.MeshCellSize = value; }


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


        double get_all_cell_size() { return BooleanOp.GridCellSize; }
        void set_all_cell_size(double value) { BooleanOp.GridCellSize = value; BooleanOp.MeshCellSize = value; }


        public double MinComponentSize {
            get { return get_min_comp_size(); }
            set { set_min_comp_size(value); }
        }
        double scene_min_comp_size = 2.0;
        double get_min_comp_size() { return scene_min_comp_size; }
        void set_min_comp_size(double value) {
            scene_min_comp_size = value;
            double dim = scene_min_comp_size * base.sceneToObjUnitScale;
            BooleanOp.MinComponentVolume = dim * dim * dim;
        }



        protected override void initialize_parameters()
        {
            Parameters.Register("op_type", get_op_type_int, set_op_type_int, (int)MeshVoxelBooleanOp.OpTypes.Union, false)
                .SetValidRange(0, 3);
            Parameters.Register("grid_cell_size", get_grid_cell_size, set_grid_cell_size, 1.0, false);
            Parameters.Register("grid_cell_count", get_grid_cell_count, set_grid_cell_count, 128, false)
                .SetValidRange(4, 4096);
            Parameters.Register("mesh_cell_size", get_mesh_cell_size, set_mesh_cell_size, 1.0, false);
            Parameters.Register("mesh_cell_count", get_mesh_cell_count, set_mesh_cell_count, 128, false)
                .SetValidRange(4, 4096);
            Parameters.Register("all_cell_size", get_all_cell_size, set_all_cell_size, 1.0, true);

            Parameters.Register("min_component_size", get_min_comp_size, set_min_comp_size, 1.0, false)
                .SetValidRange(0, 9999);
        }

    }


}