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
    public class MeshMorphologyToolBuilder : BaseCombineInputSOToolBuilder<MeshMorphologyTool>
    {
        public override MeshMorphologyTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new MeshMorphologyTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
        }
    }




    public class MeshMorphologyTool : BaseCombineInputSOTool<MeshMorphologyTool>
    {
        static readonly public string Identifier = "mesh_morphology";

        override public string Name { get { return "MeshMorphology"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        public MeshMorphologyTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
        }


        /*
         * These will be called by base class 
         */

        protected override BaseDMeshSourceOp edit_op_factory(DMeshSourceOp meshSourceOp)
        {
            return new MeshMorphologyOp() {
                MeshSource = meshSourceOp
            };
        }


        double MaxDimension = 1.0;
        public override void postprocess_target_objects()
        {
            bool is_closed = true;
            if (InputSOs.Count == 1) {
                is_closed = InputSOs[0].Mesh.CachedIsClosed;
                MaxDimension = InputSOs[0].Mesh.CachedBounds.MaxDim / base.sceneToObjUnitScale;
            } else {
                foreach (var so in InputSOs) 
                    is_closed = is_closed && so.Mesh.CachedIsClosed;
                MaxDimension = base.combineMesh.CachedBounds.MaxDim;
            }

            MorphOp.OpType = MeshMorphologyOp.OperationTypes.Dilate;

            double cell_size = ToolDefaults.DefaultVoxelSceneSizeF(new AxisAlignedBox3d(MaxDimension, MaxDimension, MaxDimension)); ;
            set_grid_cell_size(cell_size);
            set_mesh_cell_size(cell_size);
            set_min_comp_size(2.0);

            set_distance(1.0);
        }

        protected MeshMorphologyOp MorphOp {
            get { return base.EditOp as MeshMorphologyOp; }
        }



        public enum OperationTypes
        {
            Dilate = MeshMorphologyOp.OperationTypes.Dilate,
            Contract = MeshMorphologyOp.OperationTypes.Contract,
            Open = MeshMorphologyOp.OperationTypes.Open,
            Close = MeshMorphologyOp.OperationTypes.Close
        }
        public OperationTypes OpType {
            get { return (OperationTypes)get_op_type_int(); }
            set { set_op_type_int((int)value); }
        }
        int get_op_type_int() { return (int)MorphOp.OpType; }
        void set_op_type_int(int value) { MorphOp.OpType = (MeshMorphologyOp.OperationTypes)value; }



        public double GridCellSize {
            get { return get_grid_cell_size(); }
            set { set_grid_cell_size(value); }
        }
        double scene_grid_cell_size = 1.0;
        double get_grid_cell_size() { return scene_grid_cell_size; }
        void set_grid_cell_size(double value)
        {
            scene_grid_cell_size = value;
            MorphOp.GridCellSize = scene_grid_cell_size * base.sceneToObjUnitScale;
        }



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
        double scene_mesh_cell_size = 1.0;
        double get_mesh_cell_size() { return scene_mesh_cell_size; }
        void set_mesh_cell_size(double value)
        {
            scene_mesh_cell_size = value;
            MorphOp.MeshCellSize = value * base.sceneToObjUnitScale;
        }


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



        double get_all_cell_size() { return get_grid_cell_size(); }
        void set_all_cell_size(double value) { set_grid_cell_size(value); set_mesh_cell_size(value); }




        public double Distance {
            get { return get_distance(); }
            set { set_distance(value); }
        }
        double scene_distance = 1.0;
        double get_distance() { return scene_distance; }
        void set_distance(double value) {
            scene_distance = value;
            MorphOp.Distance = scene_distance * base.sceneToObjUnitScale;
        }


        public double MinComponentSize {
            get { return get_min_comp_size(); }
            set { set_min_comp_size(value); }
        }
        double scene_min_comp_size = 2.0;
        double get_min_comp_size() { return scene_min_comp_size; }
        void set_min_comp_size(double value) {
            scene_min_comp_size = value;
            double dim = scene_min_comp_size * base.sceneToObjUnitScale;
            MorphOp.MinComponentVolume = dim * dim * dim;
        }



        protected override void initialize_parameters()
        {
            Parameters.Register("op_type", get_op_type_int, set_op_type_int, (int)MeshMorphologyOp.OperationTypes.Dilate, false)
                .SetValidRange(0, 3);
            Parameters.Register("grid_cell_size", get_grid_cell_size, set_grid_cell_size, 1.0, false);
            Parameters.Register("grid_cell_count", get_grid_cell_count, set_grid_cell_count, 128, false)
                .SetValidRange(4, 4096);
            Parameters.Register("mesh_cell_size", get_mesh_cell_size, set_mesh_cell_size, 1.0, false);
            Parameters.Register("mesh_cell_count", get_mesh_cell_count, set_mesh_cell_count, 128, false)
                .SetValidRange(4, 4096);

            Parameters.Register("distance", get_distance, set_distance, 0.0, false)
                .SetValidRange(0.001, 9999.0);

            Parameters.Register("min_component_size", get_min_comp_size, set_min_comp_size, 2.0, false)
                .SetValidRange(0, 9999);

            Parameters.Register("all_cell_size", get_all_cell_size, set_all_cell_size, 1.0, true);
        }

    }


}