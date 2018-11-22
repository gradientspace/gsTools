using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;
using f3;

namespace gs
{
    public class AddHoleToolBuilder : MultiPointToolBuilder
    {
        public SOMaterial HolePreviewMaterial;
        public SOMaterial CutPreviewMaterial = null;
        public SOMaterial ErrorMaterial = null;

        public Action<AddHoleTool> OnApplyF = null;
        public Action<AddHoleTool> BuildCustomizeF = null;

        public Func<SceneObject, bool> TypeFilterF = (so) => { return so is DMeshSO; };

        public override bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            return ((type == ToolTargetType.SingleObject) && targets[0] is DMeshSO) ||
                ((type == ToolTargetType.Scene) && targets.Count(TypeFilterF) == 1);
        }


        public override ITool Build(FScene scene, List<SceneObject> targets)
        {
            DMeshSO target = targets.Where(TypeFilterF).First() as DMeshSO;
            return base.Build(scene, new List<SceneObject>() { target });
        }


        protected override MultiPointTool new_tool(FScene scene, SceneObject target)
        {
            AddHoleTool tool = new AddHoleTool(scene, target);
            tool.HolePreviewMaterial = this.HolePreviewMaterial;
            tool.CutPreviewMaterial = this.CutPreviewMaterial;
            tool.ErrorMaterial = this.ErrorMaterial;
            tool.OnApplyF = this.OnApplyF;
            if (BuildCustomizeF != null)
                BuildCustomizeF(tool);
            return tool;
        }
    }



    public class AddHoleTool : MultiPointTool
    {
        static readonly public string Identifier = "add_hole";
        override public string Name { get { return "AddHoleTool"; } }
        override public string TypeIdentifier { get { return Identifier; } }

        public SOMaterial HolePreviewMaterial;
        public SOMaterial CutPreviewMaterial;
        public SOMaterial ErrorMaterial;

        public Action<AddHoleTool> OnApplyF = null;

        public bool VerboseOutput = true;

        public AddHoleTool(FScene scene, SceneObject target) : base(scene,target)
        {
        }


        public DMeshSO GetOutputSO()
        {
            if (HoleType == HoleTypes.CutHole)
                return CutPreviewSO;
            else
                return CavityPreviewSO;
        }


        DMeshSO InputMeshSO;
        protected List<SceneObject> inputSelection;


        DMeshSO CavityPreviewSO;


        DMesh3 combineMesh;
        WrapDMeshSourceOp MeshSourceOp;
        CutPolygonHoleOp CutOp;
        ThreadedMeshComputeOp ComputeOp;
        DMeshSO CutPreviewSO;
        bool current_result_ok;

        int hole_start_id;      // this is MultiPointTool point ID of hole


        public override void Setup()
        {
            // push history stream, so that we can do undo/redo internal to tool,
            // that will not end up in external history
            push_history_stream();

            base.Setup();

            // shut off transform gizmo
            Scene.Context.TransformManager.PushOverrideGizmoType(TransformManager.NoGizmoType);

            InputMeshSO = TargetSO as DMeshSO;

            // create preview obj
            if (HolePreviewMaterial == null)
                HolePreviewMaterial = SOMaterial.CreateMesh("tool_generated", Colorf.DimGrey.WithAlpha(0.5f) );
            if (CutPreviewMaterial == null)
                CutPreviewMaterial = SOMaterial.CreateMesh("tool_generated", Colorf.DimGrey);
            if (ErrorMaterial == null)
                ErrorMaterial = SOMaterial.CreateMesh("tool_generated_error", Colorf.VideoRed);

            // clear selection here so that multi-select GroupSO goes away, otherwise
            // when we copy frmaes below, they are relative to that GroupSO, and things move
            inputSelection = new List<SceneObject>(Scene.Selected);
            set_allow_selection_changes(true);
            Scene.ClearSelection();
            set_allow_selection_changes(false);

            // create preview hole object
            CavityPreviewSO = new DMeshSO();
            CavityPreviewSO.Create(new DMesh3(), HolePreviewMaterial);
            update_hole_mesh();
            Scene.AddSceneObject(CavityPreviewSO);
            CavityPreviewSO.SetLayer(FPlatform.WidgetOverlayLayer);

            // initialize cut op
            initialize_cut_op();

            // nooooo
            Vector3d posL = InputMeshSO.Mesh.GetVertex(0);

            hole_start_id = base.AppendSurfacePoint("hole_point", Colorf.ForestGreen, 2.0f);
            SetPointPosition(hole_start_id, new Frame3f(posL), CoordSpace.ObjectCoords);
            SetPointColor(hole_start_id, Colorf.ForestGreen, FPlatform.WidgetOverlayLayer);

            // init params
            initialize_parameters();
        }


        public override void Shutdown()
        {
            begin_shutdown();

            base.Shutdown();

            // terminate any computes
            if (ComputeOp.IsComputing)
                CutOp.ForceInvalidate();

            if (CavityPreviewSO != null) {
                Scene.RemoveSceneObject(CavityPreviewSO, true);
                CavityPreviewSO = null;
            }
            if (CutPreviewSO != null) {
                Scene.RemoveSceneObject(CutPreviewSO, true);
                CutPreviewSO = null;
            }

            Scene.Context.TransformManager.PopOverrideGizmoType();

            Scene.SetVisible(TargetSO, true);
        }






        public override void PreRender()
        {
            base.PreRender();

            if (in_shutdown())
                return;

            if (active_hole_type != HoleType)
                update_current_hole_type();

            if (HoleType == HoleTypes.CutHole) {
                Scene.SetVisible(TargetSO, false);
                Scene.SetVisible(CutPreviewSO, true);
                update_cut_op();

            } else {
                Scene.SetVisible(CutPreviewSO, false);
                Scene.SetVisible(TargetSO, true);
            }

            if (HoleType == HoleTypes.CavityObject || AlwaysShowPreview) {
                Scene.SetVisible(CavityPreviewSO, true);
                if (mesh_parameters_dirty) {
                    update_hole_mesh();
                    mesh_parameters_dirty = false;
                }
                if (transform_parameters_dirty) {
                    CavityPreviewSO.SetLocalScale(new Vector3f(hole_size, hole_size, (float)CurHoleDepth));
                    transform_parameters_dirty = false;
                }
            } else {
                Scene.SetVisible(CavityPreviewSO, false);
            }
        }






        void initialize_cut_op()
        {
            if (MeshSourceOp != null)
                return;

            MeshSourceOp = new WrapDMeshSourceOp() {
                MeshSourceF = () => { return InputMeshSO.Mesh; },
                SpatialSourceF = () => { return InputMeshSO.Spatial; }
            };
            CutOp = new CutPolygonHoleOp() {
                MeshSource = MeshSourceOp
            };
            ComputeOp = new ThreadedMeshComputeOp() {
                MeshSource = CutOp
            };

            if (CutPreviewMaterial == null)
                CutPreviewMaterial = SOMaterial.CreateFlatShaded("add_hole_cut", Colorf.DimGrey);

            CutPreviewSO = new DMeshSO() { EnableSpatial = false };
            CutPreviewSO.Create(new DMesh3(), CutPreviewMaterial);
            Scene.AddSceneObject(CutPreviewSO);
            CutPreviewSO.SetLocalFrame(InputMeshSO.GetLocalFrame(CoordSpace.ObjectCoords), CoordSpace.ObjectCoords);
            CutPreviewSO.SetLocalScale(InputMeshSO.GetLocalScale());
        }


        void update_cut_op()
        {
            List<ModelingOpException> exceptions = null;

            if (cut_parameters_dirty) {
                CutOp.HoleSize = hole_size;
                CutOp.HoleSubdivisions = subdivisions;
                CutOp.ThroughHole = through_hole;
                CutOp.HoleDepth = hole_depth;
                cut_parameters_dirty = false;
            }

            try {
                DMeshOutputStatus result = ComputeOp.CheckForNewMesh();
                is_computing = (result.State == DMeshOutputStatus.States.Computing);
                if (result.State == DMeshOutputStatus.States.Ready) {

                    current_result_ok = (result.IsErrorOutput() == false);

                    var setMesh = result.Mesh;
                    if (result.Mesh.CompactMetric < 0.8)
                        setMesh = new DMesh3(result.Mesh, true);
                    CutPreviewSO.ReplaceMesh(setMesh, true);
                    CutPreviewSO.AssignSOMaterial((current_result_ok) ? CutPreviewMaterial : ErrorMaterial);

                    exceptions = result.ComputeExceptions;
                }
            } catch (Exception e) {
                DebugUtil.Log(2, Name + "Tool.PreRender: caught exception! " + e.Message);
            }

            if (ComputeOp.HaveBackgroundException) {
                Exception e = ComputeOp.ExtractBackgroundException();
                if (VerboseOutput) {
                    DebugUtil.Log(2, GetType().ToString() + ".PreRender: exception in background compute: " + e.Message);
                    DebugUtil.Log(2, e.StackTrace);
                }
            }

            if (exceptions != null && VerboseOutput) {
                foreach (var mopex in exceptions) {
                    DebugUtil.Log(2, GetType().ToString() + ".PreRender: exception in background compute " + mopex.op.GetType().ToString() + " : " + mopex.e.Message);
                    DebugUtil.Log(2, mopex.e.StackTrace);
                }
            }
        }
    






        override public bool HasApply { get { return OnApplyF != null; } }
        override public bool CanApply {
            get {
                return (HoleType == HoleTypes.CutHole) ?
                    (ComputeOp.IsComputing == false && ComputeOp.ResultConsumed && current_result_ok) : true;
            }
        }
        override public void Apply()
        {
            if (OnApplyF != null) {

                set_allow_selection_changes(true);

                // pop the history stream we pushed
                pop_history_stream();

                // restore input selection
                Scene.ClearSelection();
                foreach (var so in inputSelection)
                    Scene.Select(so, false);

                //if (InputSOs.Count != 1 || ForceSceneSpaceComputation) {
                //    Frame3f sceneF = estimate_frame();
                //    PreviewSO.RepositionPivot(sceneF);
                //}

                // apply
                if ( HoleType == HoleTypes.CavityObject)
                    bake_hole_mesh();
                OnApplyF(this);

                set_allow_selection_changes(false);

                // assume object is consumed...this is not ideal...
                if (HoleType == HoleTypes.CavityObject)
                    CavityPreviewSO = null;
                else
                    CutPreviewSO = null;
            }
        }



        void update_hole_mesh()
        {
            CappedCylinderGenerator cylgen = new CappedCylinderGenerator() {
                BaseRadius = 0.5f, TopRadius = 0.5f, Height = 1, Slices = this.subdivisions,
                Clockwise = true
            };
            DMesh3 mesh = cylgen.Generate().MakeDMesh();
            MeshTransforms.Rotate(mesh, Vector3d.Zero, Quaterniond.AxisAngleD(Vector3d.AxisX, 90));
            CavityPreviewSO.ReplaceMesh(mesh, true);
        }

        void bake_hole_mesh()
        {
            CavityPreviewSO.EditAndUpdateMesh((mesh) => {
                MeshTransforms.Scale(mesh, new Vector3d(HoleSize, HoleSize, (float)CurHoleDepth), Vector3d.Zero);
            }, GeometryEditTypes.VertexDeformation);
            CavityPreviewSO.SetLocalScale(Vector3f.One);
        }



        Ray3d LastUpdateRay;
        double LastThroughDepth;


        protected override void OnPointUpdated(ControlPoint pt, Frame3f prevFrameS, bool isFirst)
        {
            DMesh3 mesh = InputMeshSO.Mesh;
            DMeshAABBTree3 spatial = InputMeshSO.Spatial;

            Vector3f ptO = SceneTransforms.SceneToObjectP(InputMeshSO,pt.currentFrameS.Origin);
            Frame3f frameO = MeshQueries.NearestPointFrame(mesh, spatial, ptO, true);

            Vector3d dir = -frameO.Z;
            if ( hole_direction != HoleDirections.Normal ) {
                Vector3f axis = Vector3f.AxisX;
                if (hole_direction == HoleDirections.AxisY)
                    axis = Vector3f.AxisY;
                else if (hole_direction == HoleDirections.AxisZ)
                    axis = Vector3f.AxisZ;
                axis = SceneTransforms.SceneToObjectN(InputMeshSO, axis);
                dir = (dir.Dot(axis) < 0) ? -axis : axis;
            }
            //dir.Normalize();

            LastUpdateRay = new Ray3d(frameO.Origin, dir);

            List<int> hitTris = new List<int>();
            int hit_tris = spatial.FindAllHitTriangles(LastUpdateRay, hitTris);
            double max_t = 0;
            foreach ( int tid in hitTris ) {
                Vector3d n = mesh.GetTriNormal(tid);
                if (n.Dot(LastUpdateRay.Direction) < 0)
                    continue;
                IntrRay3Triangle3 rayhit = MeshQueries.TriangleIntersection(InputMeshSO.Mesh, tid, LastUpdateRay);
                max_t = rayhit.RayParameter;
                break;
            }
            if (max_t <= 0)
                return;

            LastThroughDepth = max_t;
            update_current_hole_type();

        }



        void update_current_hole_type()
        {
            if (HoleType == HoleTypes.CutHole) {
                if (CutOp != null) {
                    CutOp.StartPoint = LastUpdateRay.Origin;
                    CutOp.EndPoint = LastUpdateRay.PointAt(LastThroughDepth);
                }
            } 

            if ( HoleType == HoleTypes.CavityObject || AlwaysShowPreview ) {
                Frame3f holeFrame = new Frame3f(LastUpdateRay.Origin, LastUpdateRay.Direction);
                holeFrame.Translate((float)(-CurEndOffset) * holeFrame.Z);

                Frame3f holeFrameS = SceneTransforms.ObjectToScene(InputMeshSO, holeFrame);
                CavityPreviewSO.SetLocalFrame(holeFrameS, CoordSpace.SceneCoords);
                CavityPreviewSO.SetLocalScale(new Vector3f(hole_size, hole_size, (float)CurHoleDepth));
            }

            active_hole_type = HoleType;
        }





        double hole_size = 4;
        double hole_depth = 10;
        int subdivisions = 16;
        HoleTypes hole_type = HoleTypes.CutHole;
        HoleDirections hole_direction = HoleDirections.Normal;
        bool through_hole = true;

        HoleTypes active_hole_type = HoleTypes.CutHole;
        bool mesh_parameters_dirty = true;
        bool transform_parameters_dirty = true;
        bool cut_parameters_dirty = true;
        void mark_dirty(bool bMeshParams, bool bTransformParams)
        {
            mesh_parameters_dirty = bMeshParams;
            transform_parameters_dirty = bTransformParams;
            if ( hole_type == HoleTypes.CutHole ) {
                cut_parameters_dirty = true;
            }
        }


        double CurEndOffset {
            get { return hole_size; }
        }
        double CurHoleDepth {
            get {
                if (through_hole)
                    return LastThroughDepth + 2 * CurEndOffset;
                else
                    return hole_depth + CurEndOffset;
            }
        }




        public enum HoleTypes
        {
            CutHole = 0,
            CavityObject = 1
        }
        public HoleTypes HoleType {
            get { return get_hole_type(); }
            set { set_hole_type(value); }
        }
        HoleTypes get_hole_type() { return hole_type; }
        void set_hole_type(HoleTypes value) { hole_type = value; mark_dirty(true, true); }
        int get_hole_type_int() { return (int)get_hole_type(); }
        void set_hole_type_int(int value) { set_hole_type((HoleTypes)value); }





        public enum HoleDirections
        {
            AxisX = 0,
            AxisY = 1,
            AxisZ = 2,
            Normal = 3
        }
        public HoleDirections HoleDirection {
            get { return get_hole_direction(); }
            set { set_hole_direction(value); }
        }
        HoleDirections get_hole_direction() { return hole_direction; }
        void set_hole_direction(HoleDirections value) {
            if (hole_direction != value) {
                hole_direction = value;
                mark_dirty(false, true);
                // gross. but will force recalc of hole from current position.
                OnPointUpdated(GizmoPoints[hole_start_id], GizmoPoints[hole_start_id].currentFrameS, false);
            }
        }
        int get_hole_direction_int() { return (int)get_hole_direction(); }
        void set_hole_direction_int(int value) { set_hole_direction((HoleDirections)value); }




        public double HoleSize {
            get { return get_hole_size(); }
            set { set_hole_size(value); }
        }
        double get_hole_size() { return hole_size; }
        void set_hole_size(double value) { hole_size = value; mark_dirty(false, true); }

        public double HoleDepth {
            get { return get_hole_depth(); }
            set { set_hole_depth(value); }
        }
        double get_hole_depth() { return hole_depth; }
        void set_hole_depth(double value) { hole_depth = value; mark_dirty(false, true); }

        public int Subdivisions {
            get { return get_subdivisions(); }
            set { set_subdivisions(value); }
        }
        int get_subdivisions() { return subdivisions; }
        void set_subdivisions(int value) { subdivisions = MathUtil.Clamp(value, 1, 1000); mark_dirty(true, true); }


        public bool ThroughHole {
            get { return through_hole; }
            set { through_hole = value; }
        }
        bool get_through_hole() { return through_hole; }
        void set_through_hole(bool value) { through_hole = value; mark_dirty(false, true); }


        bool always_show_preview = true;
        public bool AlwaysShowPreview {
            get { return always_show_preview; }
        }
        bool get_always_show_preview() { return always_show_preview; }
        void set_always_show_preview(bool value) { always_show_preview = value; mark_dirty(false, true); update_current_hole_type();  }


        bool is_computing = false;
        public bool IsComputing {
            get { return is_computing; }
        }
        bool get_is_computing() { return is_computing; }
        void set_is_computing(bool ignored) { }



        protected virtual void initialize_parameters()
        {
            Parameters.Register("hole_type", get_hole_type_int, set_hole_type_int, (int)HoleTypes.CutHole, false)
                .SetValidRange(0, (int)HoleTypes.CavityObject);
            Parameters.Register("hole_direction", get_hole_direction_int, set_hole_direction_int, (int)HoleDirections.Normal, false)
                .SetValidRange(0, (int)HoleDirections.Normal);

            Parameters.Register("hole_size", get_hole_size, set_hole_size, 10.0, false)
                .SetValidRange(0.0001, 9999999);
            Parameters.Register("hole_depth", get_hole_depth, set_hole_depth, 10.0, false)
                .SetValidRange(0.0001, 9999999);
            Parameters.Register("subdivisions", get_subdivisions, set_subdivisions, 16, false)
                .SetValidRange(3, 10000);
            Parameters.Register("through_hole", get_through_hole, set_through_hole, true, false);
            Parameters.Register("show_preview", get_always_show_preview, set_always_show_preview, true, false);

            Parameters.Register("computing", get_is_computing, set_is_computing, false, false);
        }
















        /*
         * This is cut-pasted from BaseToolCore, which we cannot use in this class because of
         * no multiple inheritance...
         */
        // shutdown bits
        bool is_shutting_down = false;
        protected virtual void begin_shutdown()
        {
            is_shutting_down = true;
        }
        protected virtual bool in_shutdown()
        {
            return is_shutting_down;
        }



        //
        // selection-changes control
        //

        public override bool AllowSelectionChanges { get { return allow_selection_changes; } }
        bool allow_selection_changes = false;
        protected virtual void set_allow_selection_changes(bool allow)
        {
            allow_selection_changes = allow;
        }



        //
        // support for in-tool history stream
        //

        protected virtual bool enable_internal_history_stream() { return true; }

        bool pushed_history_stream = false;

        protected virtual void push_history_stream()
        {
            if (enable_internal_history_stream() == false)
                return;

            Util.gDevAssert(pushed_history_stream == false);
            if (!pushed_history_stream) {
                Scene.PushHistoryStream();
                pushed_history_stream = true;
            }
        }


        protected virtual void pop_history_stream()
        {
            if (enable_internal_history_stream() == false)
                return;

            if (pushed_history_stream) {
                Scene.PopHistoryStream();
                pushed_history_stream = false;
            }
        }





    }
}
