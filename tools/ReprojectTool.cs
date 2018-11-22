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

    public class ReprojectToolBuilder : IToolBuilder
    {
        public Action<ReprojectTool, Dictionary<DMeshSO, DMeshSO>> OnApplyF = null;
        public SOMaterial PreviewMaterial = null;

        public Action<ReprojectTool> BuildCustomizeF = null;

        public bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            if (type == ToolTargetType.Scene) 
                targets = new List<SceneObject>(targets.Where((so) => { return so is DMeshSO; }));
            if (targets.Count != 2)
                return false;
            if ( targets[0] is DMeshSO == false || targets[1] is DMeshSO == false )
                return false;
            return true;
        }

        public ITool Build(FScene scene, List<SceneObject> targets)
        {
            targets = new List<SceneObject>(targets.Where((so) => { return so is DMeshSO; }));
            var source = new List<DMeshSO>() { targets[0] as DMeshSO };
            DMeshSO target = targets[1] as DMeshSO;
            ReprojectTool tool = new ReprojectTool(scene, source, target) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
            if (BuildCustomizeF != null)
                BuildCustomizeF(tool);
            return tool;
        }
    }







    public class ReprojectTool : BaseMultipleDMeshSOTool<ReprojectTool>
    {
        static readonly public string Identifier = "reproject";

        override public string Name { get { return "Reproject"; } }
        override public string TypeIdentifier { get { return Identifier; } }

        protected DMeshSO ReprojectTargetSO;
        WrapDMeshSourceOp ReprojectTargetMeshOp;
        protected double targetSceneToObjUnitScale;


        public ReprojectTool(FScene scene, List<DMeshSO> meshSOs, DMeshSO target) : base(scene, meshSOs)
        {
            ReprojectTargetSO = target;

            SceneUtil.SetVisible(ReprojectTargetSO, false);
        }


        public override void Setup()
        {
            targetSceneToObjUnitScale = SceneTransforms.SceneToObject(ReprojectTargetSO, 1.0f);

            ReprojectTargetMeshOp = new WrapDMeshSourceOp() {
                MeshSourceF = () => { return ReprojectTargetSO.Mesh; },
                SpatialSourceF = () => { return ReprojectTargetSO.Spatial; }
            };

            base.Setup();
        }

        protected override BaseDMeshSourceOp edit_op_factory(TargetObject o)
        {
            TransformSequence xform = SceneTransforms.ObjectToObjectXForm(o.SO, ReprojectTargetSO);

            return new ReprojectOp() {
                MeshSource = o.MeshSourceOp,
                TargetMaxDistance = 1.0,
                RemeshRounds = 10,
                ProjectionRounds = 20,
                TargetSource = ReprojectTargetMeshOp,
                TransformToTarget = xform,
                ReprojectMode = ReprojectOp.ReprojectModes.SmoothSurfaceFlow
            };
        }


        public override void postprocess_target_objects()
        {
            double max_scene_dim = TargetObjects[0].SO.Mesh.CachedBounds.MaxDim / TargetObjects[0].sceneToObjUnitScale;
            double scene_length = ToolDefaults.DefaultTargetEdgeLengthF(max_scene_dim);
            set_target_edge_length(scene_length);
            set_max_distance(1.0);
        }



        public override void Shutdown()
        {
            base.Shutdown();
            SceneUtil.SetVisible(ReprojectTargetSO, true);
        }



        /*
         * Parameters
         */
        protected ReprojectOp mainOp;
        protected IEnumerable<ReprojectOp> Operators {
            get { foreach (var obj in TargetObjects) yield return obj.EditOp as ReprojectOp; }
        }


        public enum ReprojectModes
        {
            Smooth = ReprojectOp.ReprojectModes.SmoothSurfaceFlow,
            Sharp = ReprojectOp.ReprojectModes.SharpEdgesFlow,
            Bounded = ReprojectOp.ReprojectModes.BoundedDistance
        }
        public ReprojectModes ReprojectMode {
            get { return (ReprojectModes)get_reproject_mode_int(); }
            set { set_reproject_mode_int((int)value); }
        }
        int get_reproject_mode_int() { return (int)mainOp.ReprojectMode; }
        void set_reproject_mode_int(int value) { foreach (var op in Operators) op.ReprojectMode = (ReprojectOp.ReprojectModes)value; }


        public double TargetEdgeLength {
            get { return get_target_edge_length(); }
            set { set_target_edge_length(value); }
        }
        double scene_length = 1.0;
        double get_target_edge_length() { return scene_length; }
        void set_target_edge_length(double value)
        {
            scene_length = value;
            foreach (var target in TargetObjects)
                (target.EditOp as ReprojectOp).TargetEdgeLength = scene_length * target.sceneToObjUnitScale;
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


        public int Iterations {
            get { return get_iterations(); }
            set { set_iterations(value); }
        }
        int get_iterations() { return mainOp.RemeshRounds; }
        void set_iterations(int value) { foreach (var op in Operators) { op.RemeshRounds = value; op.ProjectionRounds = 2 * value; }  }


        public enum BoundaryModes
        {
            FreeBoundaries = ReprojectOp.BoundaryModes.FreeBoundaries,
            FixedBoundaries = ReprojectOp.BoundaryModes.FixedBoundaries,
            ConstrainedBoundaries = ReprojectOp.BoundaryModes.ConstrainedBoundaries
        }
        public BoundaryModes BoundaryMode {
            get { return (BoundaryModes)get_boundary_mode_int(); }
            set { set_boundary_mode_int((int)value); }
        }
        int get_boundary_mode_int() { return (int)mainOp.BoundaryMode; }
        void set_boundary_mode_int(int value) { foreach (var op in Operators) op.BoundaryMode = (ReprojectOp.BoundaryModes)value; }


        public double MaxDistance {
            get { return get_max_distance(); }
            set { set_max_distance(value); }
        }
        double scene_max_distance = 1.0;
        double get_max_distance() { return scene_max_distance; }
        void set_max_distance(double value) {
            scene_max_distance = value;
            foreach (var target in TargetObjects)
                (target.EditOp as ReprojectOp).TargetMaxDistance = scene_max_distance * targetSceneToObjUnitScale;        // TARGET DIMENSION HERE!!
        }


        public double TransitionSmoothness {
            get { return get_transition_smoothness(); }
            set { set_transition_smoothness(value); }
        }
        double get_transition_smoothness() { return mainOp.TransitionSmoothness; }
        void set_transition_smoothness(double value) { foreach (var op in Operators) op.TransitionSmoothness = value; }



        protected override void initialize_parameters()
        {
            mainOp = TargetObjects[0].EditOp as ReprojectOp;

            Parameters.Register("project_mode", get_reproject_mode_int, set_reproject_mode_int, (int)ReprojectModes.Smooth, false)
                .SetValidRange((int)ReprojectModes.Smooth, (int)ReprojectModes.Bounded);

            Parameters.Register("edge_length", get_target_edge_length, set_target_edge_length, 1.0, false)
                .SetValidRange(0.0001, 1000.0);
            Parameters.Register("smooth_alpha", get_smooth_alpha, set_smooth_alpha, 0.5, false)
                .SetValidRange(0.0, 1.0);
            Parameters.Register("enable_smooth", get_enable_smooth, set_enable_smooth, true, false);
            Parameters.Register("enable_collapse", get_enable_collapse, set_enable_collapse, true, false);
            Parameters.Register("enable_split", get_enable_split, set_enable_split, true, false);
            Parameters.Register("enable_flip", get_enable_flip, set_enable_flip, true, false);
            Parameters.Register("iterations", get_iterations, set_iterations, 25, false);
            Parameters.Register("boundary_mode", get_boundary_mode_int, set_boundary_mode_int, (int)BoundaryModes.FreeBoundaries, false);

            Parameters.Register("max_distance", get_max_distance, set_max_distance, 10.0, false)
                .SetValidRange(0.0001, 1000.0);
            Parameters.Register("transition_smoothness", get_transition_smoothness, set_transition_smoothness, 0.5, false)
                .SetValidRange(0.0, 1.0);
        }



    }

}