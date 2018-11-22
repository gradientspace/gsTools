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
    public class MeshShellToolBuilder : BaseCombineInputSOToolBuilder<MeshShellTool>
    {
        public SOMaterial OuterPreviewMaterial;

        public override MeshShellTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new MeshShellTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial,
                OuterPreviewMaterial = this.OuterPreviewMaterial
            };
        }
    }




    public class MeshShellTool : BaseCombineInputSOTool<MeshShellTool>
    {
        static readonly public string Identifier = "mesh_shell";

        override public string Name { get { return "MeshShell"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        /// <summary>
        /// This is the material set on the DMeshSO during the preview
        /// </summary>
        public SOMaterial OuterPreviewMaterial;
        fMaterial fOuterPreviewMaterial;


        public MeshShellTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
        }


        /*
         * These will be called by base class 
         */

        protected override BaseDMeshSourceOp edit_op_factory(DMeshSourceOp meshSourceOp)
        {
            return new MeshShellOp() {
                MeshSource = meshSourceOp,
                ShellSurfaceOnly = true,
                GridCellSize = base.sceneToObjUnitScale,
                MeshCellSize = base.sceneToObjUnitScale,
                ShellThickness = 2.4 * base.sceneToObjUnitScale
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

            ShellOp.ShellType = (is_closed) ?
                MeshShellOp.ShellTypes.DistanceField : MeshShellOp.ShellTypes.Extrusion;

            double cell_size = ToolDefaults.DefaultVoxelSceneSizeF(new AxisAlignedBox3d(MaxDimension, MaxDimension, MaxDimension)); ;
            set_grid_cell_size(cell_size);
            set_mesh_cell_size(cell_size);
            set_min_comp_size(2.0);

            show_original = is_closed;

            fOuterPreviewMaterial = OuterPreviewMaterial.ToFMaterial();
        }



        public override void PreRender()
        {
            base.PreRender();

            update_show_targets(show_original && current_result_ok);
        }


        bool current_result_is_partial = false;
        protected override void process_new_result(DMeshOutputStatus result)
        {
            bool bOK = (result.IsErrorOutput() == false);
            current_result_is_partial = 
                bOK && result.Mesh.HasMetadata && result.Mesh.FindMetadata("is_partial") != null;
        }


        bool preview_materials_pushed = false;
        bool show_targets_state = false;
        void update_show_targets(bool bShow)
        {
            if (bShow == show_targets_state)
                return;

            if (bShow) {
                if (OuterPreviewMaterial != null) {
                    if (preview_materials_pushed == false) {
                        foreach (var so in Targets) {
                            so.PushOverrideMaterial(fOuterPreviewMaterial);
                            so.SetLayer(FPlatform.WidgetOverlayLayer);
                        }
                        preview_materials_pushed = true;
                    }
                    set_targets_visibility(true);
                }
                show_targets_state = true;

            } else { 
                set_targets_visibility(false);
                show_targets_state = false;
                return;
            }

        }


        public override void Apply()
        {
            if (current_result_is_partial) {
                PreviewSO.EditAndUpdateMesh((mesh) => {
                    if (ShellDirection == ShellDirections.Outer)
                        combineMesh.ReverseOrientation();
                    else if (ShellDirection == ShellDirections.Inner)
                        mesh.ReverseOrientation();
                    MeshEditor.Append(mesh, combineMesh);
                }, GeometryEditTypes.ArbitraryEdit);
            }

            base.Apply();
        }


        public override void Shutdown()
        {
            if (preview_materials_pushed) {
                foreach (var so in Targets) {
                    so.PopOverrideMaterial();
                    so.SetLayer(FPlatform.GeometryLayer);
                }
            }

            base.Shutdown();
        }



        protected MeshShellOp ShellOp {
            get { return base.EditOp as MeshShellOp; }
        }

        public enum ShellTypes
        {
            Extrusion = MeshShellOp.ShellTypes.Extrusion,
            DistanceField = MeshShellOp.ShellTypes.DistanceField
        }
        public ShellTypes ShellType {
            get { return (ShellTypes)get_shell_type_int(); }
            set { set_shell_type_int((int)value); }
        }
        int get_shell_type_int() { return (int)ShellOp.ShellType; }
        void set_shell_type_int(int value) { ShellOp.ShellType = (MeshShellOp.ShellTypes)value; }


        public enum ShellDirections
        {
            Inner = MeshShellOp.ShellDirections.Inner,
            Outer = MeshShellOp.ShellDirections.Outer,
            Symmetric = MeshShellOp.ShellDirections.Symmetric
        }
        public ShellDirections ShellDirection {
            get { return (ShellDirections)get_shell_direction_int(); }
            set { set_shell_direction_int((int)value); }
        }
        int get_shell_direction_int() { return (int)ShellOp.ShellDirection; }
        void set_shell_direction_int(int value) { ShellOp.ShellDirection = (MeshShellOp.ShellDirections)value; }


        public double GridCellSize {
            get { return get_grid_cell_size(); }
            set { set_grid_cell_size(value); }
        }
        double scene_grid_cell_size = 1.0;
        double get_grid_cell_size() { return scene_grid_cell_size; }
        void set_grid_cell_size(double value)
        {
            scene_grid_cell_size = value;
            ShellOp.GridCellSize = scene_grid_cell_size * base.sceneToObjUnitScale;
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
            ShellOp.MeshCellSize = value * base.sceneToObjUnitScale;
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
            get { return get_shell_thickness(); }
            set { set_shell_thickness(value); }
        }
        double scene_shell_thickness = 2.4;
        double get_shell_thickness() { return scene_shell_thickness; }
        void set_shell_thickness(double value) {
            scene_shell_thickness = value;
            ShellOp.ShellThickness = scene_shell_thickness * base.sceneToObjUnitScale;
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
            ShellOp.MinComponentVolume = dim * dim * dim;
        }




        bool show_original = true;
        public bool ShowOriginal {
            get { return show_original; }
            set { show_original = value; }
        }
        bool get_show_original() { return show_original; }
        void set_show_original(bool value) { show_original = value; }



        protected override void initialize_parameters()
        {
            Parameters.Register("shell_type", get_shell_type_int, set_shell_type_int, (int)MeshShellOp.ShellTypes.DistanceField, false)
                .SetValidRange(0, 1);
            Parameters.Register("shell_direction", get_shell_direction_int, set_shell_direction_int, (int)MeshShellOp.ShellDirections.Inner, false)
                .SetValidRange(0, 2);
            Parameters.Register("grid_cell_size", get_grid_cell_size, set_grid_cell_size, 1.0, false);
            Parameters.Register("grid_cell_count", get_grid_cell_count, set_grid_cell_count, 128, false)
                .SetValidRange(4, 4096);
            Parameters.Register("mesh_cell_size", get_mesh_cell_size, set_mesh_cell_size, 1.0, false);
            Parameters.Register("mesh_cell_count", get_mesh_cell_count, set_mesh_cell_count, 128, false)
                .SetValidRange(4, 4096);
            Parameters.Register("shell_thickness", get_shell_thickness, set_shell_thickness, 0.0, false)
                .SetValidRange(0.00001, 9999999);

            Parameters.Register("min_component_size", get_min_comp_size, set_min_comp_size, 1.0, false)
                .SetValidRange(0, 9999);

            Parameters.Register("all_cell_size", get_all_cell_size, set_all_cell_size, 1.0, true);

            Parameters.Register("show_original", get_show_original, set_show_original, false, false);
        }

    }


}