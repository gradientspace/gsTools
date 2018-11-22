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
    public class GenerateClosedMeshToolBuilder : BaseCombineInputSOToolBuilder<GenerateClosedMeshTool>
    {
        public override GenerateClosedMeshTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new GenerateClosedMeshTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
        }
    }




    public class GenerateClosedMeshTool : BaseCombineInputSOTool<GenerateClosedMeshTool>
    {
        static readonly public string Identifier = "generate_closed_mesh";

        override public string Name { get { return "GeneratedClosedMesh"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        public GenerateClosedMeshTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
        }


        /*
         * These will be called by base class 
         */

        protected override BaseDMeshSourceOp edit_op_factory(DMeshSourceOp meshSourceOp)
        {
            return new GenerateClosedMeshOp() {
                MeshSource = meshSourceOp,
                GridCellSize = base.sceneToObjUnitScale,
                MeshCellSize = base.sceneToObjUnitScale
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

            //if (is_closed == false)
            GenerateClosedOp.ClosingType = GenerateClosedMeshOp.ClosingTypes.WindingNumberGrid;

            double cell_size = ToolDefaults.DefaultVoxelSceneSizeF(new AxisAlignedBox3d(MaxDimension, MaxDimension, MaxDimension)); ;
            set_grid_cell_size(cell_size);
            set_mesh_cell_size(cell_size);
            set_min_comp_size(2.0);
        }

        protected GenerateClosedMeshOp GenerateClosedOp {
            get { return base.EditOp as GenerateClosedMeshOp; }
        }



        public enum ClosingTypes
        {
            LevelSet = GenerateClosedMeshOp.ClosingTypes.LevelSet,
            WindingNumberGrid = GenerateClosedMeshOp.ClosingTypes.WindingNumberGrid,
            WindingNumberAnalytic = GenerateClosedMeshOp.ClosingTypes.WindingNumberAnalytic
        }
        public ClosingTypes ClosingType {
            get { return (ClosingTypes)get_closing_type_int(); }
            set { set_closing_type_int((int)value); }
        }
        int get_closing_type_int() { return (int)GenerateClosedOp.ClosingType; }
        void set_closing_type_int(int value) { GenerateClosedOp.ClosingType = (GenerateClosedMeshOp.ClosingTypes)value; }



        public double GridCellSize {
            get { return get_grid_cell_size(); }
            set { set_grid_cell_size(value); }
        }
        double scene_grid_cell_size = 1.0;
        double get_grid_cell_size() { return scene_grid_cell_size; }
        void set_grid_cell_size(double value) {
            scene_grid_cell_size = value;
            GenerateClosedOp.GridCellSize = scene_grid_cell_size * base.sceneToObjUnitScale;
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
        void set_mesh_cell_size(double value) {
            scene_mesh_cell_size = value;
            GenerateClosedOp.MeshCellSize = value * base.sceneToObjUnitScale;
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



        public double OffsetDistance {
            get { return get_offset_distance(); }
            set { set_offset_distance(value); }
        }
        double scene_offset_distance = 0.0;
        double get_offset_distance() { return scene_offset_distance; }
        void set_offset_distance(double value) {
            scene_offset_distance = value;
            GenerateClosedOp.OffsetDistance = scene_offset_distance * base.sceneToObjUnitScale;
        }



        public double WindingIsoValue {
            get { return get_winding_iso_value(); }
            set { set_winding_iso_value(value); }
        }
        double get_winding_iso_value() { return GenerateClosedOp.WindingIsoValue; }
        void set_winding_iso_value(double value) { GenerateClosedOp.WindingIsoValue = value; }

        // this is just inverse of isovalue, makes more sense to expose to user
        double get_winding_inflate() { return 1.0 - GenerateClosedOp.WindingIsoValue; }
        void set_winding_inflate(double value) { GenerateClosedOp.WindingIsoValue = 1.0 - value; }




        public double MinComponentSize {
            get { return get_min_comp_size(); }
            set { set_min_comp_size(value); }
        }
        double scene_min_comp_size = 1.0;
        double get_min_comp_size() { return scene_min_comp_size; }
        void set_min_comp_size(double value) {
            scene_min_comp_size = value;
            double dim = scene_min_comp_size * base.sceneToObjUnitScale;
            GenerateClosedOp.MinComponentVolume = dim * dim * dim;
        }




        protected override void initialize_parameters()
        {
            Parameters.Register("closing_type", get_closing_type_int, set_closing_type_int, (int)GenerateClosedMeshOp.ClosingTypes.LevelSet, false)
                .SetValidRange(0, 2);
            Parameters.Register("grid_cell_size", get_grid_cell_size, set_grid_cell_size, 1.0, false);
            Parameters.Register("grid_cell_count", get_grid_cell_count, set_grid_cell_count, 128, false)
                .SetValidRange(4, 4096);
            Parameters.Register("mesh_cell_size", get_mesh_cell_size, set_mesh_cell_size, 1.0, false);
            Parameters.Register("mesh_cell_count", get_mesh_cell_count, set_mesh_cell_count, 128, false)
                .SetValidRange(4, 4096);

            Parameters.Register("offset_distance", get_offset_distance, set_offset_distance, 0.0, false);

            Parameters.Register("winding_iso", get_winding_iso_value, set_winding_iso_value, 0.5, false)
                .SetValidRange(0.01, 0.99);
            Parameters.Register("winding_inflate", get_winding_inflate, set_winding_inflate, 0.5, true)
                .SetValidRange(0.01, 0.99);

            Parameters.Register("min_component_size", get_min_comp_size, set_min_comp_size, 2.0, false)
                .SetValidRange(0, 9999);

            Parameters.Register("all_cell_size", get_all_cell_size, set_all_cell_size, 1.0, true);
        }

    }


}