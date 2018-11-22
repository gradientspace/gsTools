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

    public class RemeshToolBuilder : BaseMultipleDMeshSOToolBuilder<RemeshTool>
    {
        public override RemeshTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new RemeshTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
        }
    }





    public class RemeshTool : BaseMultipleDMeshSOTool<RemeshTool>
    {
        static readonly public string Identifier = "remesh";

        override public string Name { get { return "Remesh"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        public RemeshTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
        }


        /*
         * These will be called by base class 
         */


        protected override BaseDMeshSourceOp edit_op_factory(TargetObject o)
        {
            return new RemeshOp() {
                MeshSource = o.MeshSourceOp
            };
        }

        public override void postprocess_target_objects()
        {
            double max_scene_dim = 0;
            foreach (var target in TargetObjects)
                max_scene_dim = Math.Max(max_scene_dim, target.SO.Mesh.CachedBounds.MaxDim / target.sceneToObjUnitScale);
            double scene_length = ToolDefaults.DefaultTargetEdgeLengthF(max_scene_dim);
            set_target_edge_length(scene_length);
        }


        /*
         * Parameters
         */

        protected RemeshOp mainOp;
        protected IEnumerable<RemeshOp> Operators {
            get { foreach (var obj in TargetObjects) yield return obj.EditOp as RemeshOp; }
        }

        public double TargetEdgeLength {
            get { return get_target_edge_length(); }
            set { set_target_edge_length(value); }
        }
        double scene_length = 1.0;
        double get_target_edge_length() { return scene_length; }
        void set_target_edge_length(double value) {
            scene_length = value;
            foreach (var target in TargetObjects)
                (target.EditOp as RemeshOp).TargetEdgeLength = scene_length * target.sceneToObjUnitScale;
        }

        public double SmoothAlpha {
            get { return get_smooth_alpha(); }
            set { set_smooth_alpha(value); }
        }
        double get_smooth_alpha() { return mainOp.SmoothingSpeed; }
        void set_smooth_alpha(double value) { foreach (var op in Operators) op.SmoothingSpeed = value; }


        public bool EnableSmooth {
            get { return get_enable_smooth(); }
            set { set_enable_smooth(value); }
        }
        bool get_enable_smooth() { return mainOp.EnableSmoothing; }
        void set_enable_smooth(bool value) { foreach (var op in Operators) op.EnableSmoothing = value; }

        public bool EnableCollapse {
            get { return get_enable_collapse(); }
            set { set_enable_collapse(value); }
        }
        bool get_enable_collapse() { return mainOp.EnableCollapses; }
        void set_enable_collapse(bool value) { foreach (var op in Operators) op.EnableCollapses = value; }

        public bool EnableSplit {
            get { return get_enable_split(); }
            set { set_enable_split(value); }
        }
        bool get_enable_split() { return mainOp.EnableSplits; }
        void set_enable_split(bool value) { foreach (var op in Operators) op.EnableSplits = value; }

        public bool EnableFlip {
            get { return get_enable_flip(); }
            set { set_enable_flip(value); }
        }
        bool get_enable_flip() { return mainOp.EnableFlips; }
        void set_enable_flip(bool value) { foreach (var op in Operators) op.EnableFlips = value; }


        public bool PreserveCreases {
            get { return get_preserve_creases(); }
            set { set_preserve_creases(value); }
        }
        bool get_preserve_creases() { return mainOp.PreserveCreases; }
        void set_preserve_creases(bool value) { foreach (var op in Operators) op.PreserveCreases = value; }

        public double CreaseAngle {
            get { return get_crease_angle(); }
            set { set_crease_angle(value); }
        }
        double get_crease_angle() { return mainOp.CreaseAngle; }
        void set_crease_angle(double value) { foreach (var op in Operators) op.CreaseAngle = value; }


        public bool ReprojectToInput {
            get { return get_reproject(); }
            set { set_reproject(value); }
        }
        bool get_reproject() { return mainOp.ReprojectToInput; }
        void set_reproject(bool value) { foreach (var op in Operators) op.ReprojectToInput = value; }

        public int Iterations {
            get { return get_iterations(); }
            set { set_iterations(value); }
        }
        int get_iterations() { return mainOp.RemeshRounds; }
        void set_iterations(int value) { foreach (var op in Operators) op.RemeshRounds = value; }


        public enum BoundaryModes
        {
            FreeBoundaries = RemeshOp.BoundaryModes.FreeBoundaries,
            FixedBoundaries = RemeshOp.BoundaryModes.FixedBoundaries,
            ConstrainedBoundaries = RemeshOp.BoundaryModes.ConstrainedBoundaries
        }
        public BoundaryModes BoundaryMode {
            get { return (BoundaryModes)get_boundary_mode_int(); }
            set { set_boundary_mode_int((int)value); }
        }
        int get_boundary_mode_int() { return (int)mainOp.BoundaryMode; }
        void set_boundary_mode_int(int value) { foreach (var op in Operators) op.BoundaryMode = (RemeshOp.BoundaryModes)value; }




        protected override void initialize_parameters()
        {
            mainOp = TargetObjects[0].EditOp as RemeshOp;

            Parameters.Register("edge_length", get_target_edge_length, set_target_edge_length, 1.0, false)
                .SetValidRange(0.0001, 1000.0);
            Parameters.Register("smooth_alpha", get_smooth_alpha, set_smooth_alpha, 0.5, false)
                .SetValidRange(0.0, 1.0);
            Parameters.Register("enable_smooth", get_enable_smooth, set_enable_smooth, true, false);
            Parameters.Register("enable_collapse", get_enable_collapse, set_enable_collapse, true, false);
            Parameters.Register("enable_split", get_enable_split, set_enable_split, true, false);
            Parameters.Register("enable_flip", get_enable_flip, set_enable_flip, true, false);
            Parameters.Register("preserve_creases", get_preserve_creases, set_preserve_creases, true, false);
            Parameters.Register("crease_angle", get_crease_angle, set_crease_angle, 30.0, false);
            Parameters.Register("reproject", get_reproject, set_reproject, true, false);
            Parameters.Register("iterations", get_iterations, set_iterations, 25, false);
            Parameters.Register("boundary_mode", get_boundary_mode_int, set_boundary_mode_int, (int)BoundaryModes.FreeBoundaries, false);
        }

    }



}