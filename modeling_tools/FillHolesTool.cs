// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using g3;
using f3;

namespace gs
{
    public class FillHolesToolBuilder : IToolBuilder
    {
        public Action<FillHolesTool, DMeshSO> OnApplyF = null;
        public SOMaterial PreviewMaterial = null;

        public Action<FillHolesTool> BuildCustomizeF = null;

        public Func<SceneObject, bool> TypeFilterF = (so) => { return so is DMeshSO; };

        public virtual bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            return (type == ToolTargetType.SingleObject && TypeFilterF(targets[0]) ) ||
                (type == ToolTargetType.Scene && targets.Where(TypeFilterF).Count() == 1);
        }

        public virtual ITool Build(FScene scene, List<SceneObject> targets)
        {
            DMeshSO target = targets.Where(TypeFilterF).First() as DMeshSO;
            FillHolesTool tool = new_tool(scene, target);
            tool.OnApplyF = this.OnApplyF;
            tool.PreviewMaterial = this.PreviewMaterial;
            if (BuildCustomizeF != null)
                BuildCustomizeF(tool);
            return tool;
        }

        protected virtual FillHolesTool new_tool(FScene scene, DMeshSO target)
        {
            return new FillHolesTool(scene, target);
        }
    }




    /// <summary>
    /// This tool is for element-level mesh edits, eg like poly-modeling type edits
    /// </summary>
    public class FillHolesTool : BaseToolCore, ITool
    {
        static readonly public string Identifier = "hole_fill";
        virtual public string Name { get { return "HoleFill"; } }
        virtual public string TypeIdentifier { get { return Identifier; } }

        /// <summary>
        /// Called on Apply(). By default, does nothing, as the DMeshSO is already
        /// in scene as preview. Override to implement your own behavior
        /// </summary>
        public Action<FillHolesTool, DMeshSO> OnApplyF;

        /// <summary>
        /// This is the material set on the DMeshSO during the preview
        /// </summary>
        public SOMaterial PreviewMaterial;

        /// <summary>
        /// The material set on the hole border loops
        /// </summary>
        public SOMaterial HoleBoundaryMaterial;


        /// <summary> If true, exception messages are printed </summary>
        public bool VerboseOutput = true;


        fDimension radius = fDimension.Scene(0.25);
        virtual public fDimension BorderRadius {
            get { return radius; }
            set { radius = value; }
        }

        bool show_hidden = false;
        virtual public bool ShowHidden {
            get { return show_hidden; }
            set { show_hidden = value; visibility_valid = false; }
        }



        InputBehaviorSet behaviors;
        virtual public InputBehaviorSet InputBehaviors {
            get { return behaviors; }
            set { behaviors = value; }
        }

        DMeshSO target;
        virtual public DMeshSO Target {
            get { return target; }
        }

        public FillHolesTool(FScene scene, DMeshSO target)
        {
            this.Scene = scene;
            this.target = target;

            behaviors = new InputBehaviorSet();

            // TODO is this where we should be doing this??
            behaviors.Add(
                new HoleFillTool_2DInputBehavior(this, scene.Context) { Priority = 5 });

            // shut off transform gizmo
            scene.Context.TransformManager.PushOverrideGizmoType(TransformManager.NoGizmoType);

            // set up parameters
            initialize_parameters();

            SceneUtil.SetVisible(Target, false);
        }


        protected DMeshSO previewSO;
        protected DMesh3 PreviewMesh { get { return previewSO.Mesh; } }

        protected MeshBoundaryLoops Loops;
        protected BoundaryCurveSet boundaryGeom;
        protected int numHolesFilled = 0;

        double sceneToObjUnitScale = 1.0;

        virtual public void Setup()
        {
            // push history stream, so that we can do undo/redo internal to tool,
            // that will not end up in external history
            push_history_stream();

            if (PreviewMaterial == null)
                PreviewMaterial = SOMaterial.CreateFlatShaded("holefill_preview", Colorf.DimGrey);

            if (HoleBoundaryMaterial == null)
                HoleBoundaryMaterial = SOMaterial.CreateStandard("holefill_boundary", Colorf.PivotYellow);

            previewSO = new DMeshSO();
            previewSO.EnableSpatial = false;
            previewSO.Create(new DMesh3(Target.Mesh), PreviewMaterial);
            previewSO.Name = "HoleFillTool_preview";
            previewSO.SetLocalFrame(Target.GetLocalFrame(CoordSpace.ObjectCoords), CoordSpace.ObjectCoords);
            previewSO.SetLocalScale(Target.GetLocalScale());
            previewSO.OnMeshModified += PreviewSO_OnMeshModified;
            Scene.AddSceneObject(previewSO);

            sceneToObjUnitScale = SceneTransforms.SceneToObject(Target, 1.0f);

            Loops = new MeshBoundaryLoops(previewSO.Mesh);
            boundaryGeom = new BoundaryCurveSet(previewSO.RootGameObject, HoleBoundaryMaterial.ToFMaterial());
            boundaryGeom.Radius = BorderRadius.SceneValue * sceneToObjUnitScale;
            boundaryGeom.Initialize(Loops);
        }

        /*
         * Parameters
         */

        ParameterSet parameters;
        public ParameterSet Parameters {
            get { return parameters; }
        }


        bool is_computing = false;
        public bool IsComputing {
            get { return is_computing; }
        }
        bool get_is_computing() { return is_computing; }
        void set_is_computing(bool ignored) { }


        public enum FillTypes
        {
            Trivial = 0,
            Minimal = 1,
            Smooth = 2
        }
        protected FillTypes fill_type = FillTypes.Minimal;
        public FillTypes FillType {
            get { return fill_type; }
            set { if (value != fill_type) fill_type = value; }
        }
        int get_fill_type_int() { return (int)FillType; }
        void set_fill_type_int(int value) { FillType = (FillTypes)value; }


        bool get_show_hidden() { return ShowHidden; }
        void set_show_hidden(bool value) { ShowHidden = value; }



        // these params are for minimal fills

        public bool OptimizeTriangles {
            get { return optimize_tris; }
            set { if ( value != optimize_tris ) optimize_tris = value; }
        }
        bool optimize_tris = true;
        bool get_optimize_tris() { return OptimizeTriangles; }
        void set_optimize_tris(bool value) { OptimizeTriangles = value; }


        public double OptimizeTrisDeviationThresh {
            get { return optimize_tris_deviation_thresh; }
            set { if (optimize_tris_deviation_thresh != value) optimize_tris_deviation_thresh = value; }
        }
        double optimize_tris_deviation_thresh = 0.1;
        double get_optimize_tris_deviation_thresh() { return OptimizeTrisDeviationThresh; }
        void set_optimize_tris_deviation_thresh(double value) { OptimizeTrisDeviationThresh = value; }



        // these params are for smooth fills

        public bool AutoTargetEdgeLength {
            get { return auto_target_edge_length; }
            set { if (value != auto_target_edge_length) auto_target_edge_length = value; }
        }
        bool auto_target_edge_length = true;
        bool get_auto_target_edge_length() { return AutoTargetEdgeLength; }
        void set_auto_target_edge_length(bool value) { AutoTargetEdgeLength = value; }


        public double TargetEdgeLength {
            get { return scene_length; }
            set { if (scene_length != value) scene_length = value; }
        }
        double scene_length = 2.5;
        double get_target_edge_length() { return scene_length; }
        void set_target_edge_length(double value) {
            scene_length = value;
        }
        double get_obj_taret_edge_length() {
            return scene_length * sceneToObjUnitScale;
        }


        public int SmoothOptimizeRounds {
            get { return smooth_opt_rounds; }
            set { int v = MathUtil.Clamp(value, 1, 100); if (smooth_opt_rounds != v) smooth_opt_rounds = v; }
        }
        int smooth_opt_rounds = 2;
        int get_smooth_opt_rounds() { return SmoothOptimizeRounds; }
        void set_smooth_opt_rounds(int value) { SmoothOptimizeRounds = value; }


        protected virtual void initialize_parameters()
        {
            parameters = new ParameterSet();
            parameters.Register("computing", get_is_computing, set_is_computing, false, false);

            parameters.Register("fill_type", get_fill_type_int, set_fill_type_int, (int)FillTypes.Minimal, false)
                .SetValidRange(0, (int)FillTypes.Smooth);

            parameters.Register("show_hidden", get_show_hidden, set_show_hidden, true, false);

            parameters.Register("optimize_tris", get_optimize_tris, set_optimize_tris, true, false);
            Parameters.Register("optimize_tris_deviation_thresh", get_optimize_tris_deviation_thresh, set_optimize_tris_deviation_thresh, 0.1, false);

            parameters.Register("auto_edge_length", get_auto_target_edge_length, set_auto_target_edge_length, true, false);
            Parameters.Register("edge_length", get_target_edge_length, set_target_edge_length, 1.0, false)
                .SetValidRange(0.0001, 1000.0);
            Parameters.Register("smooth_opt_rounds", get_smooth_opt_rounds, set_smooth_opt_rounds, 1, false)
                .SetValidRange(1, 100);
        }




        virtual public void Shutdown()
        {
            begin_shutdown();

            // in case we are computing in background (todo: fix)
            cancel_background_task = true;
            is_computing = false;

            // if we did not Apply(), this history stream is still here...
            pop_history_stream();

            // restore transform gizmo
            Scene.Context.TransformManager.PopOverrideGizmoType();

            boundaryGeom.Clear();

            if (previewSO != null) {
                Scene.RemoveSceneObject(previewSO, true);
                previewSO = null;
            }
            
            SceneUtil.SetVisible(Target, true);
        }


        bool visibility_valid = true;
        private void PreviewSO_OnMeshModified(DMeshSO so)
        {
            visibility_valid = false;
        }


        virtual public void PreRender()
        {
            boundaryGeom.PreRender();

            // could avoid full iteration here if we were smarter...
            if (visibility_valid == false) {
                numHolesFilled = 0;
                for (int i = 0; i < Loops.Count; ++i) {
                    EdgeLoop l = Loops[i];
                    bool bIsBoundary = previewSO.Mesh.IsBoundaryEdge(l.Edges[0]);
                    boundaryGeom.SetVisibility(i, bIsBoundary);
                    if (bIsBoundary == false)
                        numHolesFilled++;
                }
                visibility_valid = true;

                boundaryGeom.SetLayer(show_hidden ? FPlatform.WidgetOverlayLayer : FPlatform.GeometryLayer);
            }
        }


        virtual public bool HasApply { get { return true; } }
        virtual public bool CanApply { get { return IsComputing == false; } }
        virtual public void Apply() {
            // pop the history stream we pushed
            pop_history_stream();

            if (OnApplyF != null) {
                OnApplyF(this, previewSO);
                previewSO = null;
            }
        }


        public int FindHitHoleID(Ray3f worldRay)
        {
            if (is_computing)
                return -1;

            Ray3f sceneRay = Scene.ToSceneRay(worldRay);
            Ray3d objRay = SceneTransforms.SceneToObject(previewSO, sceneRay);
            int hit_index = boundaryGeom.FindRayIntersection(objRay);
            return hit_index;
        }


        public bool FillHole(int hitHoleID, bool bInteractive)
        {
            if (is_computing)
                return false;

            EdgeLoop loop = Loops[hitHoleID];

            AddTrianglesMeshChange addChange = null;
            previewSO.EditAndUpdateMesh((mesh) => {
                switch (FillType) {
                    case FillTypes.Trivial: addChange = fill_trivial(mesh, loop); break;
                    case FillTypes.Minimal: addChange = fill_minimal(mesh, loop); break;
                    case FillTypes.Smooth: addChange = fill_smooth(mesh, loop); break;
                }
            }, GeometryEditTypes.ArbitraryEdit);

            if (addChange != null) {
                add_change(addChange, bInteractive);
                // we know what changed visibility-wise, so we can avoid full visibility update
                boundaryGeom.SetVisibility(hitHoleID, false);
                numHolesFilled++;
                visibility_valid = true;

                return true;
            } else
                return false;
        }




        public AddTrianglesMeshChange fill_trivial(DMesh3 mesh, EdgeLoop loop)
        {
            AddTrianglesMeshChange addChange = null;
            SimpleHoleFiller filler = new SimpleHoleFiller(mesh, loop);
            bool fill_ok = filler.Fill();
            if (fill_ok) {
                addChange = new AddTrianglesMeshChange();
                addChange.InitializeFromExisting(mesh,
                    new List<int>() { filler.NewVertex },
                    filler.NewTriangles);
            }
            return addChange;
        }


        public AddTrianglesMeshChange fill_minimal(DMesh3 mesh, EdgeLoop loop)
        {
            AddTrianglesMeshChange addChange = null;
            MinimalHoleFill filler = new MinimalHoleFill(mesh, loop);
            filler.IgnoreBoundaryTriangles = false;
            filler.OptimizeDevelopability = true;
            filler.OptimizeTriangles = this.OptimizeTriangles;
            filler.DevelopabilityTolerance = this.OptimizeTrisDeviationThresh;

            bool fill_ok = filler.Apply();
            if (fill_ok) {
                addChange = new AddTrianglesMeshChange();
                addChange.InitializeFromExisting(mesh,
                    filler.FillVertices, filler.FillTriangles);
            }
            return addChange;
        }




        public AddTrianglesMeshChange fill_smooth(DMesh3 mesh, EdgeLoop loop)
        {
            AddTrianglesMeshChange addChange = null;
            SmoothedHoleFill filler = new SmoothedHoleFill(mesh, loop);
            filler.ConstrainToHoleInterior = true;
            if (this.AutoTargetEdgeLength) {
                double mine, maxe, avge;
                MeshQueries.EdgeLengthStatsFromEdges(mesh, loop.Edges, out mine, out maxe, out avge);
                filler.TargetEdgeLength = avge;
            } else {
                filler.TargetEdgeLength = this.TargetEdgeLength;
            }
            filler.SmoothSolveIterations = this.SmoothOptimizeRounds;

            bool fill_ok = filler.Apply();
            if (fill_ok) {
                addChange = new AddTrianglesMeshChange();
                addChange.InitializeFromExisting(mesh,
                    filler.FillVertices, filler.FillTriangles);
            }
            return addChange;
        }



        void add_change(AddTrianglesMeshChange add, bool bInteractive)
        {
            AddTrianglesChange change = new AddTrianglesChange(previewSO, add);
            Scene.History.PushChange(change, true);
            if (bInteractive)
                Scene.History.PushInteractionCheckpoint();
        }




        bool cancel_background_task = false;



        public void FillAllHoles()
        {
            if (IsComputing)
                return;

            is_computing = true;

            List<EdgeLoop> cur_loops = new List<EdgeLoop>(Loops.Loops);
            Task.Run((Action)(() => {
                var use_fill_type = FillType;
                DMesh3 filled_mesh = (DMesh3)previewSO.SafeMeshRead((mesh) => { return new DMesh3(mesh); });

                try_fills_again:
                if (cancel_background_task)
                    return;

                foreach (EdgeLoop loop in cur_loops) {
                    if (cancel_background_task)
                        return;
                    try {
                        // skip loops that have been filled
                        if (loop.IsBoundaryLoop(filled_mesh) == false)
                            continue;

                        switch (use_fill_type) {
                            case FillTypes.Trivial: fill_trivial(filled_mesh, loop); break;
                            case FillTypes.Minimal: fill_minimal(filled_mesh, loop); break;
                            case FillTypes.Smooth: fill_smooth(filled_mesh, loop); break;
                        }
                    } catch ( Exception e ) {
                        DebugUtil.Log("FillHolesTool.FillAllHoles: Exception: " + e.Message);
                        // ignore-and-continue for now
                    }
                }

                // recompute loops in case any failed
                MeshBoundaryLoops loops = new MeshBoundaryLoops(filled_mesh, true);

                // if any did fail, fall back to simpler fills in second pass
                if (loops.Count > 0 && use_fill_type != FillTypes.Trivial) {
                    if (use_fill_type == FillTypes.Smooth)
                        use_fill_type = FillTypes.Minimal;
                    else
                        use_fill_type = FillTypes.Trivial;
                    cur_loops = new List<EdgeLoop>(loops.Loops);
                    goto try_fills_again;
                }

                if (cancel_background_task) 
                    return;

                ReplaceEntireMeshChange change = new ReplaceEntireMeshChange(previewSO,
                    previewSO.Mesh, filled_mesh);
                ThreadMailbox.PostToMainThread((Action)(() => {
                    this.Scene.History.PushChange(change, false);
                    this.Scene.History.PushInteractionCheckpoint();
                }));

                is_computing = false;
            }));  // end task
        }




        public void HoleStats(out int totalHoles, out int filled)
        {
            totalHoles = filled = 0;
            if (Loops != null) {
                totalHoles = Loops.Count;
                filled = numHolesFilled;
            }
        }


    }


    




    class HoleFillTool_2DInputBehavior : Any2DInputBehavior
    {
        FContext context;
        FillHolesTool tool;

        int startHitHoleID;


        public HoleFillTool_2DInputBehavior(FillHolesTool tool, FContext s)
        {
            this.tool = tool;
            context = s;
        }

        override public CaptureRequest WantsCapture(InputState input)
        {
            if (context.ToolManager.ActiveRightTool == null || !(context.ToolManager.ActiveRightTool is FillHolesTool))
                return CaptureRequest.Ignore;
            if ( Pressed(ref input) ) {
                if ( (startHitHoleID = tool.FindHitHoleID(WorldRay(ref input))) >= 0 )
                    return CaptureRequest.Begin(this);
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            return Capture.Begin(this, CaptureSide.Any);
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            if (tool == null)
                throw new Exception("HoleFillTool_MouseBehavior.UpdateCapture: tool is null, how did we get here?");

            if ( Released(ref input) ) {
                int hitHoleID = tool.FindHitHoleID(WorldRay(ref input));
                if (hitHoleID == startHitHoleID)
                    tool.FillHole(hitHoleID, true);
                return Capture.End;
            } else {
                return Capture.Continue;
            }
        }

        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            return Capture.End;
        }


        public override bool EnableHover
        {
            get { return CachedIsMouseInput; }
        }
        public override void UpdateHover(InputState input)
        {
        }
        public override void EndHover(InputState input)
        {
        }
    }




}
