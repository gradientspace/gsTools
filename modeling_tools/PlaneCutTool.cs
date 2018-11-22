using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;
using f3;

namespace gs
{


    public class PlaneCutToolBuilder : BaseMultipleDMeshSOToolBuilder<PlaneCutTool>
    {
        public override PlaneCutTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new PlaneCutTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
        }
    }





    public class PlaneCutTool : BaseMultipleDMeshSOTool<PlaneCutTool>
    {
        static readonly public string Identifier = "plane_cut";

        override public string Name { get { return "PlaneCut"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        public PlaneCutTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
        }


        Vector3d PlaneOriginS;
        Vector3d PlaneNormalS;

        protected PlaneCutPivotSO gizmoSO;


        protected override BaseDMeshSourceOp edit_op_factory(TargetObject o)
        {
            return new PlaneCutOp() {
                MeshSource = o.MeshSourceOp,
                MinimalFill = false,
                FillEdgeLength = 2.0
            };
        }

        public override void postprocess_target_objects()
        {
            // turn on xform gizmo
            Scene.Context.TransformManager.PopOverrideGizmoType();
            Scene.Context.TransformManager.PushOverrideGizmoType(TransformManager.DefaultGizmoType);


            PlaneOriginS = Vector3d.Zero;
            PlaneNormalS = Vector3d.AxisY;

            foreach (var obj in TargetObjects) 
                PlaneOriginS += SceneTransforms.ObjectToSceneP(obj.SO, obj.SO.Mesh.CachedBounds.Center);
            PlaneOriginS /= TargetObjects.Count;

            gizmoSO = new PlaneCutPivotSO();
            gizmoSO.Create(Scene.PivotSOMaterial, Scene.FrameSOMaterial);
            gizmoSO.OnTransformModified += GizmoSO_OnTransformModified;
            Scene.AddSceneObject(gizmoSO);

            Frame3f cutFrameS = new Frame3f(PlaneOriginS, PlaneNormalS);
            gizmoSO.SetLocalFrame(cutFrameS, CoordSpace.SceneCoords);

            set_plane_origin(PlaneOriginS);
            set_plane_normal(PlaneNormalS);

            set_allow_selection_changes(true);
            Scene.Select(gizmoSO, true);
            set_allow_selection_changes(false);
        }


        private void GizmoSO_OnTransformModified(SceneObject so)
        {
            Frame3f cutFrameS = gizmoSO.GetLocalFrame(CoordSpace.SceneCoords);
            PlaneOrigin = cutFrameS.Origin;
            PlaneNormal = cutFrameS.Z;
        }

        public override void Shutdown()
        {
            if (gizmoSO != null) {
                set_allow_selection_changes(true);
                Scene.RemoveSceneObject(gizmoSO, true);
                set_allow_selection_changes(false);
                gizmoSO = null;
            }

            base.Shutdown();
        }



        public Dictionary<DMeshSO, DMesh3> OtherSideMeshes = new Dictionary<DMeshSO, DMesh3>();

        protected override void process_new_result(TargetObject obj, DMeshOutputStatus result)
        {
            if (result.Mesh != null) {
                DMesh3 otherSideMesh = result.Mesh.FindMetadata("other_side") as DMesh3;
                if (otherSideMesh != null) 
                    result.Mesh.RemoveMetadata("other_side");
                OtherSideMeshes[obj.SO] = otherSideMesh;
            }
        }


        //
        // Parameters
        //


        protected PlaneCutOp mainOp;
        protected IEnumerable<PlaneCutOp> Operators {
            get { foreach (var obj in TargetObjects) yield return obj.EditOp as PlaneCutOp; }
        }



        public bool FillHoles {
            get { return get_fill_holes(); }
            set { set_fill_holes(value); }
        }
        bool get_fill_holes() { return mainOp.FillHoles; }
        void set_fill_holes(bool value)
        {
            foreach (var op in Operators) op.FillHoles = value;
        }


        public bool ReverseNormal {
            get { return get_reverse_normal(); }
            set { set_reverse_normal(value); }
        }
        bool get_reverse_normal() { return mainOp.ReverseNormal; }
        void set_reverse_normal(bool value)
        {
            foreach (var op in Operators) op.ReverseNormal = value;
        }

        public bool KeepBothSides {
            get { return get_keep_both_sides(); }
            set { set_keep_both_sides(value); }
        }
        bool get_keep_both_sides() { return mainOp.ReturnBothSides; }
        void set_keep_both_sides(bool value)
        {
            foreach (var op in Operators) op.ReturnBothSides = value;
        }


        public bool MinimalFill {
            get { return get_minimal_fill(); }
            set { set_minimal_fill(value); }
        }
        bool get_minimal_fill() { return mainOp.MinimalFill; }
        void set_minimal_fill(bool value)
        {
            foreach (var op in Operators) op.MinimalFill = value;
        }



        public double FillEdgeLength {
            get { return get_fill_edge_length(); }
            set { set_fill_edge_length(value); }
        }
        double get_fill_edge_length() { return mainOp.FillEdgeLength; }
        void set_fill_edge_length(double value)
        {
            foreach (var op in Operators) op.FillEdgeLength = value;
        }


        public Vector3d PlaneOrigin {
            get { return get_plane_origin(); }
            set { set_plane_origin(value); }
        }
        Vector3d get_plane_origin() { return PlaneOriginS; }
        void set_plane_origin(Vector3d value)
        {
            PlaneOriginS = value;
            foreach (var obj in TargetObjects)
                (obj.EditOp as PlaneCutOp).PlaneOrigin = SceneTransforms.SceneToObjectP(obj.SO, PlaneOriginS);
        }


        public Vector3d PlaneNormal {
            get { return get_plane_normal(); }
            set { set_plane_normal(value); }
        }
        Vector3d get_plane_normal() { return PlaneNormalS; }
        void set_plane_normal(Vector3d value)
        {
            PlaneNormalS = value;
            foreach (var obj in TargetObjects)
                (obj.EditOp as PlaneCutOp).PlaneNormal = SceneTransforms.SceneToObjectN(obj.SO, (Vector3f)PlaneNormalS);
        }



        protected override void initialize_parameters()
        {
            mainOp = TargetObjects[0].EditOp as PlaneCutOp;

            Parameters.Register("fill_holes", get_fill_holes, set_fill_holes, true, false);
            Parameters.Register("reverse_normal", get_reverse_normal, set_reverse_normal, true, false);
            Parameters.Register("minimal_fill", get_minimal_fill, set_minimal_fill, false, false);
            Parameters.Register("fill_edge_length", get_fill_edge_length, set_fill_edge_length, 1.0, false);
            Parameters.Register("keep_both", get_keep_both_sides, set_keep_both_sides, false, false);
        }






        InputBehavior active_behavior;
        bool end_on_set = false;

        public void BeginSetPlaneFromSingleClick()
        {
            active_behavior = new PlaneCutTool_2DBehavior(Scene.Context, this) { Priority = 5 };
            InputBehaviors.Add(active_behavior);
            end_on_set = true;
            set_preview_visibility(false);
            Scene.Context.RegisterNextFrameAction(() => {
                set_targets_visibility(true);
            });
        }

        public void EndSetPlaneFromSingleClick()
        {
            if (active_behavior != null) {
                InputBehaviors.Remove(active_behavior);
                active_behavior = null;
            }
            set_targets_visibility(false);
            Scene.Context.RegisterNextFrameAction(() => {
                set_preview_visibility(true);
            });
        }

        public void SetPlaneFromSingleClick(Frame3f worldF, bool bShiftDown)
        {
            Frame3f sceneF = Scene.ToSceneFrame(worldF);
            if (bShiftDown == false)
                sceneF.AlignAxis(2, Vector3f.AxisY);
            TransformSOChange change = new TransformSOChange(gizmoSO, sceneF, CoordSpace.SceneCoords);
            Scene.History.PushChange(change, false);
            Scene.History.PushInteractionCheckpoint();
            if (end_on_set)
                EndSetPlaneFromSingleClick();
        }




        protected class PlaneCutPivotSO : PivotSO
        {
            public override bool IsTemporary {
                get { return true; }
            }
        }

    }







    class PlaneCutTool_2DBehavior : Any2DInputBehavior
    {
        FContext context;
        PlaneCutTool tool;


        public PlaneCutTool_2DBehavior(FContext s, PlaneCutTool tool)
        {
            context = s;
            this.tool = tool;
        }

        override public CaptureRequest WantsCapture(InputState input)
        {
            if (context.ToolManager.ActiveRightTool != tool)
                return CaptureRequest.Ignore;
            if (Pressed(input)) {
                SORayHit rayHit;
                if ( SceneUtil.FindNearestRayIntersection(tool.Targets, WorldRay(input), out rayHit) ) {
                    return CaptureRequest.Begin(this);
                }
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            SORayHit rayHit;
            if (SceneUtil.FindNearestRayIntersection(tool.Targets, WorldRay(input), out rayHit)) {
                return Capture.Begin(this);
            }
            return Capture.Ignore;
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            if (Released(input)) {
                SORayHit rayHit;
                if (SceneUtil.FindNearestRayIntersection(tool.Targets, WorldRay(input), out rayHit)) {
                    Frame3f clickW = new Frame3f(rayHit.hitPos, rayHit.hitNormal);
                    tool.Scene.Context.RegisterNextFrameAction(() => {
                        tool.SetPlaneFromSingleClick(clickW, input.bShiftKeyDown);
                    });
                }
                return Capture.End;
            } else
                return Capture.Continue;
        }


        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            return Capture.End;
        }

    }




}
