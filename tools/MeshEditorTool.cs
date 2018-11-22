// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using f3;

namespace gs
{
    public class MeshEditorToolBuilder : IToolBuilder
    {
        public Action<MeshEditorTool, DMeshSO> OnApplyF = null;
        public SOMaterial PreviewMaterial = null;

        public Action<MeshEditorTool> BuildCustomizeF = null;

        public Func<SceneObject, bool> TypeFilterF = (so) => { return so is DMeshSO; };

        public virtual bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            return (type == ToolTargetType.SingleObject && TypeFilterF(targets[0])) ||
                (type == ToolTargetType.Scene && targets.Where(TypeFilterF).Count() == 1);
        }

        public virtual ITool Build(FScene scene, List<SceneObject> targets)
        {
            DMeshSO target = targets.Where(TypeFilterF).First() as DMeshSO;
            MeshEditorTool tool = new_tool(scene, target);
            tool.OnApplyF = this.OnApplyF;
            tool.PreviewMaterial = this.PreviewMaterial;
            if (BuildCustomizeF != null)
                BuildCustomizeF(tool);
            return tool;
        }

        protected virtual MeshEditorTool new_tool(FScene scene, DMeshSO target)
        {
            return new MeshEditorTool(scene, target);
        }
    }




    /// <summary>
    /// This tool is for element-level mesh edits, eg like poly-modeling type edits
    /// </summary>
    public class MeshEditorTool : BaseToolCore, ITool
    {
        static readonly public string Identifier = "mesh_editor";
        virtual public string Name { get { return "MeshEditor"; } }
        virtual public string TypeIdentifier { get { return Identifier; } }

        /// <summary>
        /// Called on Apply(). By default, does nothing, as the DMeshSO is already
        /// in scene as preview. Override to implement your own behavior
        /// </summary>
        public Action<MeshEditorTool, DMeshSO> OnApplyF;

        /// <summary>
        /// This is the material set on the DMeshSO during the preview
        /// </summary>
        public SOMaterial PreviewMaterial;


        fDimension radius = fDimension.World(0.1);
        virtual public fDimension Radius {
            get { return radius; }
            set { radius = value; }
        }


        public enum EditOperations
        {
            DeleteTriangle = 0,
            PokeTriangle = 1,

            DeleteEdge = 10,
            FlipEdge = 11,
            SplitEdge = 12,
            CollapseEdge = 13,

            DeleteVertex = 20,

            BridgeEdges = 30,

            DeleteComponent = 100,
            DeleteBorderRing = 101
        }
        protected EditOperations active_operation = EditOperations.DeleteTriangle;
        public EditOperations ActiveOperation {
            get { return active_operation; }
            set { if (value != active_operation) set_active_op(value); }
        }


        protected bool allow_backface_hits = false;
        public bool AllowBackfaceHits {
            get { return allow_backface_hits; }
            set { allow_backface_hits = value; }
        }


        void set_active_op(EditOperations op)
        {
            switch (op) {
                case EditOperations.DeleteTriangle:
                    activeOp = new MeshEditorDeleteTriOp(); break;
                case EditOperations.PokeTriangle:
                    activeOp = new MeshEditorPokeTriOp(); break;

                case EditOperations.DeleteEdge:
                    activeOp = new MeshEditorDeleteEdgeOp(); break;
                case EditOperations.FlipEdge:
                    activeOp = new MeshEditorFlipEdgeOp(); break;
                case EditOperations.SplitEdge:
                    activeOp = new MeshEditorSplitEdgeOp(); break;
                case EditOperations.CollapseEdge:
                    activeOp = new MeshEditorCollapseEdgeOp(); break;

                case EditOperations.DeleteVertex:
                    activeOp = new MeshEditorDeleteVertexOp(); break;

                case EditOperations.BridgeEdges:
                    activeOp = new MeshEditorBridgeEdgesOp(); break;

                case EditOperations.DeleteComponent:
                    activeOp = new MeshEditorDeleteComponentOp(); break;
                case EditOperations.DeleteBorderRing:
                    activeOp = new MeshEditorDeleteRingOp(); break;
            }
            active_operation = op;
        }
        protected MeshEditorOpType activeOp = new MeshEditorDeleteTriOp();



        InputBehaviorSet behaviors;
        virtual public InputBehaviorSet InputBehaviors {
            get { return behaviors; }
            set { behaviors = value; }
        }

        DMeshSO target;
        virtual public DMeshSO Target {
            get { return target; }
        }

        Frame3f lastBrushPosS;
        ToolIndicatorSet Indicators;
        BrushCursorSphere brushIndicator;
        Frame3f cameraFrameL;

        public MeshEditorTool(FScene scene, DMeshSO target)
        {
            this.Scene = scene;
            this.target = target;

            behaviors = new InputBehaviorSet();

            // TODO is this where we should be doing this??
            behaviors.Add(
                new MeshEditorTool_2DInputBehavior(this, scene.Context) { Priority = 5 });

            // shut off transform gizmo
            scene.Context.TransformManager.PushOverrideGizmoType(TransformManager.NoGizmoType);

            // set up parameters
            initialize_parameters();

            SceneUtil.SetVisible(Target, false);

            Indicators = new ToolIndicatorSet(this, scene);
        }


        protected DMeshSO previewSO;
        protected EditMeshSpatial PreviewSpatial;
        protected DMesh3 PreviewMesh { get { return previewSO.Mesh; } }

        virtual public void Setup()
        {
            // push history stream, so that we can do undo/redo internal to tool,
            // that will not end up in external history
            push_history_stream();

            brushIndicator = new BrushCursorSphere() {
                PositionF = () => { return Scene.ToWorldP(lastBrushPosS.Origin); },
                Radius = fDimension.World(() => { return radius.WorldValue; })
            };
            Indicators.AddIndicator(brushIndicator);
            brushIndicator.material = MaterialUtil.CreateTransparentMaterialF(Colorf.DarkRed, 0.8f);


            if (PreviewMaterial == null)
                PreviewMaterial = SOMaterial.CreateFlatShaded("MeshEditor_generated", Colorf.DimGrey);
            previewSO = new DMeshSO();
            previewSO.EnableSpatial = false;
            previewSO.Create(new DMesh3(Target.Mesh), PreviewMaterial);
            previewSO.Name = "MeshEditorTool_preview";
            previewSO.SetLocalFrame(Target.GetLocalFrame(CoordSpace.ObjectCoords), CoordSpace.ObjectCoords);
            previewSO.SetLocalScale(Target.GetLocalScale());
            Scene.AddSceneObject(previewSO);


            PreviewSpatial = new EditMeshSpatial() {
                SourceMesh = Target.Mesh,
                SourceSpatial = Target.Spatial,
                EditMesh = PreviewMesh
            };
        }


        /*
         * Parameters
         */

        ParameterSet parameters;
        public ParameterSet Parameters {
            get { return parameters; }
        }
        

        int get_active_op_int() { return (int)ActiveOperation; }
        void set_active_op_int(int value) { ActiveOperation = (EditOperations)value; }

        bool get_allow_backface_hits() { return AllowBackfaceHits; }
        void set_allow_backface_hits(bool value) { AllowBackfaceHits = value; }


        protected virtual void initialize_parameters()
        {
            parameters = new ParameterSet();
            parameters.Register("active_op", get_active_op_int, set_active_op_int, (int)EditOperations.DeleteTriangle, false)
                .SetValidRange(0, (int)EditOperations.BridgeEdges);
            parameters.Register("allow_backface_hits", get_allow_backface_hits, set_allow_backface_hits, false, false);
        }




        virtual public void Shutdown()
        {
            begin_shutdown();

            pop_history_stream();

            // restore transform gizmo
            Scene.Context.TransformManager.PopOverrideGizmoType();
            Indicators.Disconnect(true);

            if (previewSO != null) {
                Scene.RemoveSceneObject(previewSO, true);
                previewSO = null;
            }

            SceneUtil.SetVisible(Target, true);
        }


        virtual public void PreRender()
        {
            Indicators.PreRender();

            Frame3f camFrameW = Scene.ActiveCamera.GetWorldFrame();
            cameraFrameL = SceneTransforms.TransformTo(camFrameW, this.target, CoordSpace.WorldCoords, CoordSpace.ObjectCoords);
        }


        virtual public bool HasApply { get { return true; } }
        virtual public bool CanApply { get { return true; } }
        virtual public void Apply() {

            // pop the history stream we pushed
            pop_history_stream();

            if (OnApplyF != null) {
                OnApplyF(this, previewSO);
                previewSO = null;
            }
        }


        bool in_stroke = false;

        protected virtual void begin_stroke(Frame3f vStartFrameL, int nHitElementID, MeshEditorOpType.ElementType elemType)
        {
            activeOp.Editor = this;
            activeOp.BeginStroke(vStartFrameL, nHitElementID, elemType);
        }

        protected virtual void update_stroke(Frame3f vLocalF, int nHitElementID, MeshEditorOpType.ElementType elemType)
        {
            activeOp.UpdateStroke(vLocalF, nHitElementID, elemType);
        }

        protected virtual void end_stroke()
        {
            activeOp.EndStroke();
        }

        protected virtual void update_preview(Frame3f vLocalF, int nHitElementID, MeshEditorOpType.ElementType elemType)
        {
            activeOp.UpdatePreview(vLocalF, nHitElementID, elemType);
        }



        public void BeginStroke(Frame3f vFrameS, int nHitElementID, MeshEditorOpType.ElementType elemType)
        {
            if (in_stroke)
                throw new Exception("MeshEditorTool.BeginBrushStroke: already in brush stroke!");

            Frame3f vFrameL = SceneTransforms.SceneToObject(Target, vFrameS);
            begin_stroke(vFrameL, nHitElementID, elemType);
            
            in_stroke = true;
            lastBrushPosS = vFrameS;
        }

        public void UpdateStroke(Frame3f vFrameS, int nHitElementID, MeshEditorOpType.ElementType elemType)
        {
            if (in_stroke == false)
                throw new Exception("MeshEditorTool.UpdateBrushStroke: not in brush stroke!");

            Frame3f vFrameL = SceneTransforms.SceneToObject(Target, vFrameS);
            update_stroke(vFrameL, nHitElementID, elemType);

            lastBrushPosS = vFrameS;
        }

        public void EndStroke()
        {
            if (in_stroke == false)
                throw new Exception("MeshEditorTool.EndBrushStroke: not in brush stroke!");
            in_stroke = false;
            end_stroke();
        }


        public void UpdateBrushPreview(Frame3f vFrameS, int nHitElementID, MeshEditorOpType.ElementType elemType)
        {
            Frame3f vFrameL = SceneTransforms.SceneToObject(Target, vFrameS);
            update_preview(vFrameL, nHitElementID, elemType);

            lastBrushPosS = vFrameS;
        }



        public bool FindHitElement(Ray3f sceneRay, ref Frame3f hitFrameS, ref int hitID, ref MeshEditorOpType.ElementType elemType)
        {
            if (activeOp.MeshElement == MeshEditorOpType.ElementType.Triangle) {
                elemType = MeshEditorOpType.ElementType.Triangle;
                return FindHitTriangle(sceneRay, activeOp.SnapToElement, ref hitFrameS, ref hitID);
            } else if (activeOp.MeshElement == MeshEditorOpType.ElementType.Edge) {
                elemType = MeshEditorOpType.ElementType.Edge;
                return FindHitEdge(sceneRay, activeOp.BoundaryFilter, activeOp.SnapToElement, ref hitFrameS, ref hitID);
            } else if (activeOp.MeshElement == MeshEditorOpType.ElementType.Vertex) {
                elemType = MeshEditorOpType.ElementType.Vertex;
                return FindHitVertex(sceneRay, activeOp.BoundaryFilter, ref hitFrameS, ref hitID);
            } else {
                return false;
            }
        }



        public bool FindHitTriangle(Ray3f sceneRay, bool snap_to_center, ref Frame3f hitFrameS, ref int hitTID)
        {
            Ray3f objRay = SceneTransforms.SceneToObject(Target, sceneRay);

            hitTID = PreviewSpatial.FindNearestHitTriangle(objRay);
            if (hitTID == DMesh3.InvalidID)
                return false;
            if (allow_backface_hits == false && is_back_facing(hitTID))
                return false;

            var intr = MeshQueries.TriangleIntersection(PreviewMesh, hitTID, objRay);

            if (snap_to_center) {
                Frame3f hitFrameL = new Frame3f(PreviewMesh.GetTriCentroid(hitTID), PreviewMesh.GetTriNormal(hitTID));
                hitFrameS = SceneTransforms.ObjectToScene(previewSO, hitFrameL);
            } else {
                Vector3f hitPoint = objRay.PointAt((float)intr.RayParameter);
                Frame3f hitFrameL = new Frame3f(hitPoint, PreviewMesh.GetTriNormal(hitTID));
                hitFrameS = SceneTransforms.ObjectToScene(previewSO, hitFrameL);
            }

            return true;
        }



        public bool FindHitVertex(Ray3f sceneRay, MeshEditorOpType.BoundaryType boundaryMode, ref Frame3f hitFrameS, ref int hitVID)
        {
            Ray3f objRay = SceneTransforms.SceneToObject(Target, sceneRay);

            int hit_tri = PreviewSpatial.FindNearestHitTriangle(objRay);
            if (hit_tri == DMesh3.InvalidID)
                return false;
            if (allow_backface_hits == false && is_back_facing(hit_tri))
                return false;

            Index3i vt = PreviewMesh.GetTriangle(hit_tri);
            hitVID = -1; double near_sqr = double.MaxValue;
            for ( int j = 0; j < 3; ++j ) {
                Vector3f v = (Vector3f)PreviewMesh.GetVertex(vt[j]);
                if ( objRay.DistanceSquared(v) < near_sqr) {
                    near_sqr = objRay.DistanceSquared(v);
                    hitVID = vt[j];
                }
            }
            if (boundaryMode != MeshEditorOpType.BoundaryType.Any) {
                bool is_boundary = PreviewMesh.IsBoundaryVertex(hitVID);
                if ((is_boundary && boundaryMode == MeshEditorOpType.BoundaryType.OnlyInternal) ||
                     (is_boundary == false && boundaryMode == MeshEditorOpType.BoundaryType.OnlyBoundary))
                    return false;

            }

            Frame3f hitFrameL = new Frame3f(PreviewMesh.GetVertex(hitVID), PreviewMesh.GetTriNormal(hit_tri));
            hitFrameS = SceneTransforms.ObjectToScene(previewSO, hitFrameL);

            return true;
        }




        public bool FindHitEdge(Ray3f sceneRay, MeshEditorOpType.BoundaryType boundaryMode, bool snap_to_center, ref Frame3f hitFrameS, ref int hitEID)
        {
            Ray3f objRay = SceneTransforms.SceneToObject(Target, sceneRay);

            int hit_tri = PreviewSpatial.FindNearestHitTriangle(objRay);
            if (hit_tri == DMesh3.InvalidID)
                return false;
            if (allow_backface_hits == false && is_back_facing(hit_tri))
                return false;

            var intr = MeshQueries.TriangleIntersection(PreviewMesh, hit_tri, objRay);
            int e_idx = -1; double near_sqr = double.MaxValue; DistRay3Segment3 near_dist = null;
            for ( int j = 0; j < 3; ++j ) {
                Segment3d seg = new Segment3d(intr.Triangle[j], intr.Triangle[(j + 1) % 3]);
                DistRay3Segment3 dist = new DistRay3Segment3(objRay, seg);
                if ( dist.GetSquared() < near_sqr ) {
                    near_sqr = dist.GetSquared();
                    near_dist = dist;
                    e_idx = j;
                }
            }
            int eid = PreviewMesh.GetTriEdge(hit_tri, e_idx);
            if (boundaryMode != MeshEditorOpType.BoundaryType.Any) {
                bool is_boundary = PreviewMesh.IsBoundaryEdge(eid);
                if ((is_boundary && boundaryMode == MeshEditorOpType.BoundaryType.OnlyInternal) ||
                     (is_boundary == false && boundaryMode == MeshEditorOpType.BoundaryType.OnlyBoundary))
                    return false;

            }

            if (snap_to_center) {
                Frame3f hitFrameL = new Frame3f(PreviewMesh.GetEdgePoint(eid,0.5), PreviewMesh.GetTriNormal(hit_tri));
                hitFrameS = SceneTransforms.ObjectToScene(previewSO, hitFrameL);
            } else {
                Frame3f hitFrameL = new Frame3f(near_dist.SegmentClosest, PreviewMesh.GetTriNormal(hit_tri));
                hitFrameS = SceneTransforms.ObjectToScene(previewSO, hitFrameL);
            }

            hitEID = eid;
            return true;
        }



        bool is_back_facing(int tid)
        {
            Vector3d n = PreviewMesh.GetTriNormal(tid);
            Vector3d c = PreviewMesh.GetTriCentroid(tid);
            return (c - cameraFrameL.Origin).Dot(n) > -0.1;
        }


        void remove_from_spatial(IEnumerable<int> removed_v, IEnumerable<int> removed_t) {
            foreach (int tid in removed_t)
                PreviewSpatial.RemoveTriangle(tid);
        }
        void add_to_spatial(IEnumerable<int> added_v, IEnumerable<int> added_t) {
            foreach (int tid in added_t)
                PreviewSpatial.AddTriangle(tid);
        }


        void do_remove_triangles(IEnumerable<int> tris, bool bInteractive)
        {
            RemoveTrianglesMeshChange remove = new RemoveTrianglesMeshChange() {
                OnApplyF = remove_from_spatial, OnRevertF = add_to_spatial
            };
            remove.InitializeFromExisting(previewSO.Mesh, tris);
            RemoveTrianglesChange change = new RemoveTrianglesChange(previewSO, remove);
            Scene.History.PushChange(change, false);
            if (bInteractive)
                Scene.History.PushInteractionCheckpoint();
        }
        List<int> get_vtx_tris(DMesh3 mesh, int vid) {
            List<int> tris = new List<int>() { };
            mesh.GetVtxTriangles(vid, tris, false);
            return tris;
        }
        List<int> get_edge_tris(DMesh3 mesh, int eid) {
            Index2i et = mesh.GetEdgeT(eid);
            List<int> tris = new List<int>() { et.a };
            if (et.b != DMesh3.InvalidID)
                tris.Add(et.b);
            return tris;
        }


        public void RemoveTriangle(int tid, bool bInteractive)
        {
            if (PreviewMesh.IsTriangle(tid) == false ) {
                DebugUtil.Log("MeshEditorTool.RemoveTriangle: invalid tid!");
                return;
            }
            do_remove_triangles(new List<int>() { tid }, bInteractive);
        }

        public void RemoveVertex(int vid, bool bInteractive)
        {
            if (PreviewMesh.IsVertex(vid) == false) {
                DebugUtil.Log("MeshEditorTool.RemoveVertex: invalid vid!");
                return;
            }
            List<int> tris = get_vtx_tris(PreviewMesh, vid);
            do_remove_triangles(tris, bInteractive);
        }

        public void RemoveEdge(int eid, bool bInteractive)
        {
            if (PreviewMesh.IsEdge(eid) == false) {
                DebugUtil.Log("MeshEditorTool.RemoveEdge: invalid eid!");
                return;
            }
            List<int> tris = get_edge_tris(PreviewMesh, eid);
            do_remove_triangles(tris, bInteractive);
        }


        public void RemoveComponent(int tid, bool bInteractive)
        {
            if (PreviewMesh.IsTriangle(tid) == false) {
                DebugUtil.Log("MeshEditorTool.RemoveComponent: invalid tid!");
                return;
            }
            var tris = MeshConnectedComponents.FindConnectedT(PreviewMesh, tid);
            do_remove_triangles(tris, bInteractive);
        }


        public void RemoveBorderRing(int vid, bool bInteractive)
        {
            if (PreviewMesh.IsVertex(vid) == false) {
                DebugUtil.Log("MeshEditorTool.RemoveBorderRing: invalid vid!");
                return;
            }
            MeshVertexSelection verts = new MeshVertexSelection(PreviewMesh); verts.SelectConnectedBoundaryV(vid);
            MeshFaceSelection tris = new MeshFaceSelection(PreviewMesh, verts, 1);
            do_remove_triangles(tris, bInteractive);
        }



        void add_change(AddTrianglesMeshChange add, bool bInteractive)
        {
            add.OnApplyF = add_to_spatial; add.OnRevertF = remove_from_spatial;
            AddTrianglesChange  change = new AddTrianglesChange(previewSO, add);
            Scene.History.PushChange(change, true);
            if (bInteractive)
                Scene.History.PushInteractionCheckpoint();
        }
        void add_replace_change(RemoveTrianglesMeshChange remove, AddTrianglesMeshChange add, bool bInteractive)
        {
            remove.OnApplyF = remove_from_spatial; remove.OnRevertF = add_to_spatial;
            add.OnApplyF = add_to_spatial; add.OnRevertF = remove_from_spatial;
            
            ReplaceTrianglesChange change = new ReplaceTrianglesChange(previewSO, remove, add);
            Scene.History.PushChange(change, true);
            if (bInteractive)
                Scene.History.PushInteractionCheckpoint();
        }


        public void FlipEdge(int eid, bool bInteractive)
        {
            if (PreviewMesh.IsEdge(eid) == false) {
                DebugUtil.Log("MeshEditorTool.FlipEdge: invalid eid!");
                return;
            }
            RemoveTrianglesMeshChange removeChange = null;
            AddTrianglesMeshChange addChange = null;
            previewSO.EditAndUpdateMesh((mesh) => {
                removeChange = new RemoveTrianglesMeshChange();
                removeChange.InitializeFromExisting(mesh, get_edge_tris(mesh, eid));

                DMesh3.EdgeFlipInfo flipInfo;
                if (mesh.FlipEdge(eid, out flipInfo) == MeshResult.Ok) {
                    PreviewSpatial.RemoveTriangle(flipInfo.t0);
                    PreviewSpatial.RemoveTriangle(flipInfo.t1);
                    PreviewSpatial.AddTriangle(flipInfo.t0);
                    PreviewSpatial.AddTriangle(flipInfo.t1);

                    addChange = new AddTrianglesMeshChange();
                    addChange.InitializeFromExisting(mesh, null, get_edge_tris(mesh, eid));
                }
            }, GeometryEditTypes.ArbitraryEdit);

            if ( addChange != null ) 
                add_replace_change(removeChange, addChange, bInteractive);
        }


        public void SplitEdge(int eid, Frame3f posL, bool bInteractive)
        {
            if (PreviewMesh.IsEdge(eid) == false) {
                DebugUtil.Log("MeshEditorTool.SplitEdge: invalid eid!");
                return;
            }
            Index2i et = PreviewMesh.GetEdgeT(eid);
            RemoveTrianglesMeshChange removeChange = null;
            AddTrianglesMeshChange addChange = null;

            previewSO.EditAndUpdateMesh((mesh) => {
                removeChange = new RemoveTrianglesMeshChange();
                List<int> input_tris = get_edge_tris(mesh, eid);
                removeChange.InitializeFromExisting(mesh, input_tris);

                DMesh3.EdgeSplitInfo splitInfo;
                if (mesh.SplitEdge(eid, out splitInfo) == MeshResult.Ok) {
                    PreviewSpatial.RemoveTriangle(et.a);
                    if (et.b != DMesh3.InvalidID)
                        PreviewSpatial.RemoveTriangle(et.b);
                    PreviewSpatial.AddTriangle(et.a);
                    if (et.b != DMesh3.InvalidID)
                        PreviewSpatial.AddTriangle(et.b);
                    PreviewSpatial.AddTriangle(splitInfo.eNewT2);
                    input_tris.Add(splitInfo.eNewT2);
                    if (splitInfo.eNewT3 != DMesh3.InvalidID) {
                        PreviewSpatial.AddTriangle(splitInfo.eNewT3);
                        input_tris.Add(splitInfo.eNewT3);
                    }
                    mesh.SetVertex(splitInfo.vNew, posL.Origin);

                    addChange = new AddTrianglesMeshChange();
                    addChange.InitializeFromExisting(mesh,
                        new List<int>() { splitInfo.vNew }, input_tris);
                }
            }, GeometryEditTypes.ArbitraryEdit);

            if (addChange != null) 
                add_replace_change(removeChange, addChange, bInteractive);
        }



        public void CollapseEdge(int eid, Frame3f posL, bool bInteractive)
        {
            if (PreviewMesh.IsEdge(eid) == false) {
                DebugUtil.Log("MeshEditorTool.CollapseEdge: invalid eid!");
                return;
            }
            Index2i ev = PreviewMesh.GetEdgeV(eid);
            int keep = ev.a, discard = ev.b;
            bool boundarya = PreviewMesh.IsBoundaryVertex(keep);
            bool boundaryb = PreviewMesh.IsBoundaryVertex(discard);
            if ( boundaryb && ! boundarya ) {
                keep = ev.b; discard = ev.a;
            }
            HashSet<int> removeT = new HashSet<int>();
            foreach (int tid in PreviewMesh.VtxTrianglesItr(keep))
                removeT.Add(tid);
            foreach (int tid in PreviewMesh.VtxTrianglesItr(discard))
                removeT.Add(tid);

            RemoveTrianglesMeshChange removeChange = null;
            AddTrianglesMeshChange addChange = null;

            previewSO.EditAndUpdateMesh((mesh) => {
                removeChange = new RemoveTrianglesMeshChange();
                removeChange.InitializeFromExisting(mesh, removeT);

                DMesh3.EdgeCollapseInfo collapseInfo;
                if (mesh.CollapseEdge(keep, discard, out collapseInfo) == MeshResult.Ok) {
                    foreach (int tid in removeT)
                        PreviewSpatial.RemoveTriangle(tid);
                    foreach (int tid in PreviewMesh.VtxTrianglesItr(keep))
                        PreviewSpatial.AddTriangle(tid);
                    if (boundarya == false & boundaryb == false)
                        mesh.SetVertex(keep, posL.Origin);

                    addChange = new AddTrianglesMeshChange();
                    addChange.InitializeFromExisting(mesh, new List<int>() { keep }, get_vtx_tris(mesh, keep));
                }
            }, GeometryEditTypes.ArbitraryEdit);

            if (addChange != null)
                add_replace_change(removeChange, addChange, bInteractive);
        }



        public void BridgeEdges(int ea, int eb, bool bInteractive)
        {
            if (PreviewMesh.IsEdge(ea) == false || PreviewMesh.IsEdge(eb) == false) {
                DebugUtil.Log("MeshEditorTool.BridgeEdges: invalid eid!");
                return;
            }
            if (PreviewMesh.IsBoundaryEdge(ea) == false || PreviewMesh.IsBoundaryEdge(eb) == false) {
                DebugUtil.Log("MeshEditorTool.BridgeEdges: edge is not boundary edge!");
                return;
            }
            Index2i eva = PreviewMesh.GetOrientedBoundaryEdgeV(ea);
            Index2i evb = PreviewMesh.GetOrientedBoundaryEdgeV(eb);

            AddTrianglesMeshChange addChange = null;

            previewSO.EditAndUpdateMesh((mesh) => {
                int shared_v = IndexUtil.find_shared_edge_v(ref eva, ref evb);
                if (shared_v == DMesh3.InvalidID) {
                    int t1 = mesh.AppendTriangle(eva.b, eva.a, evb.b);
                    int t2 = mesh.AppendTriangle(evb.b, evb.a, eva.b);
                    PreviewSpatial.AddTriangle(t1);
                    PreviewSpatial.AddTriangle(t2);
                    addChange = new AddTrianglesMeshChange();
                    addChange.InitializeFromExisting(mesh, null, new List<int>() { t1, t2 });
                } else {
                    int ovb = (evb.a == shared_v) ? evb.b : evb.a;
                    int t = mesh.AppendTriangle(eva.b, eva.a, ovb);
                    PreviewSpatial.AddTriangle(t);
                    addChange = new AddTrianglesMeshChange();
                    addChange.InitializeFromExisting(mesh, null, new List<int>() { t });
                }
            }, GeometryEditTypes.ArbitraryEdit);

            if (addChange != null)
                add_change(addChange, bInteractive);
        }



        public void PokeFace(int tid, Frame3f posL, bool bInteractive )
        {
            if (PreviewMesh.IsTriangle(tid) == false) {
                DebugUtil.Log("MeshEditorTool.PokeFace: invalid tid!");
                return;
            }
            Vector3d baryCoords = MeshQueries.BaryCoords(PreviewMesh, tid, posL.Origin);
            RemoveTrianglesMeshChange removeChange = null;
            AddTrianglesMeshChange addChange = null;

            previewSO.EditAndUpdateMesh((mesh) => {
                removeChange = new RemoveTrianglesMeshChange();
                removeChange.InitializeFromExisting(mesh, new List<int>() { tid });

                DMesh3.PokeTriangleInfo pokeInfo;
                if ( mesh.PokeTriangle(tid, baryCoords, out pokeInfo) == MeshResult.Ok ) { 
                    PreviewSpatial.RemoveTriangle(tid);
                    PreviewSpatial.AddTriangle(tid);
                    PreviewSpatial.AddTriangle(pokeInfo.new_t1);
                    PreviewSpatial.AddTriangle(pokeInfo.new_t2);

                    addChange = new AddTrianglesMeshChange();
                    addChange.InitializeFromExisting(mesh, 
                        new List<int>() { pokeInfo.new_vid },
                        new List<int>() { tid, pokeInfo.new_t1, pokeInfo.new_t2 });
                }
            }, GeometryEditTypes.ArbitraryEdit);

            if (addChange != null)
                add_replace_change(removeChange, addChange, bInteractive);
        }


    }






    




    class MeshEditorTool_2DInputBehavior : Any2DInputBehavior
    {
        FContext context;
        MeshEditorTool tool;

        Frame3f lastHitFrameS;
        int lastHitElementID;
        MeshEditorOpType.ElementType lastHitElementType;

        bool update_last_hit(MeshEditorTool tool, Ray3f rayS)
        {
            int hit_id = -1; Frame3f hitFrameS = Frame3f.Identity;
            MeshEditorOpType.ElementType hitType = MeshEditorOpType.ElementType.Triangle;
            if ( tool.FindHitElement(rayS, ref hitFrameS, ref hit_id, ref hitType) ) { 
                lastHitFrameS = hitFrameS;
                lastHitElementID = hit_id;
                lastHitElementType = hitType;
                return true;
            }
            return false;
        }

        public MeshEditorTool_2DInputBehavior(MeshEditorTool tool, FContext s)
        {
            this.tool = tool;
            context = s;
        }

        override public CaptureRequest WantsCapture(InputState input)
        {
            if (context.ToolManager.ActiveRightTool == null || !(context.ToolManager.ActiveRightTool is MeshEditorTool))
                return CaptureRequest.Ignore;
            if ( Pressed(ref input) ) {
                if ( update_last_hit(tool, SceneRay(ref input, tool.Scene)) )
                    return CaptureRequest.Begin(this);
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            update_last_hit(tool, SceneRay(ref input, tool.Scene));

            tool.BeginStroke(lastHitFrameS, lastHitElementID, lastHitElementType);

            return Capture.Begin(this, CaptureSide.Any);
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            if (tool == null)
                throw new Exception("MeshEditorTool_MouseBehavior.UpdateCapture: tool is null, how did we get here?");

            if ( Released(ref input) ) {
                tool.EndStroke();
                return Capture.End;
            } else {
                update_last_hit(tool, SceneRay(ref input, tool.Scene));
                tool.UpdateStroke( lastHitFrameS, lastHitElementID, lastHitElementType);
                return Capture.Continue;
            }
        }

        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            try {
                tool.EndStroke();
            } catch (Exception e) {
                DebugUtil.Log("MeshEditorTool.ForceEndCapture: " + e.Message);
            }
            return Capture.End;
        }


        public override bool EnableHover
        {
            get { return CachedIsMouseInput; }
        }
        public override void UpdateHover(InputState input)
        {
            update_last_hit(tool, SceneRay(ref input, tool.Scene));
            tool.UpdateBrushPreview(lastHitFrameS, lastHitElementID, lastHitElementType);
        }
        public override void EndHover(InputState input)
        {
        }
    }












    /// <summary>
    /// Base-class for DMesh3 surface-paint operation. 
    /// Subclasses must implement abstract Apply() method.
    /// </summary>
    public abstract class MeshEditorOpType
    {
        protected MeshEditorTool editor_tool;
        public MeshEditorTool Editor {
            get { return editor_tool; }
            set { if (editor_tool != value) { editor_tool = value; } }
        }

        public enum ElementType
        {
            Triangle = 1, Edge = 2, Vertex = 4
        }
        public abstract ElementType MeshElement { get; }

        public enum BoundaryType
        {
            OnlyInternal = 1, OnlyBoundary = 2, Any = 4
        }
        public virtual BoundaryType BoundaryFilter { get { return BoundaryType.Any; } }


        public virtual bool SnapToElement { get { return true; } }


        protected Frame3f previous_posL;
        public Frame3f PreviousPosL {
            get { return previous_posL; }
        }

        public virtual void BeginStroke(Frame3f vStartPosL, int tid, ElementType eType)
        {
            previous_posL = vStartPosL;
        }

        public virtual void UpdateStroke(Frame3f vNextPosL, int tid, ElementType eType)
        {
            previous_posL = vNextPosL;
        }

        public virtual void EndStroke()
        {
        }

        public virtual void UpdatePreview(Frame3f vPreviewPosL, int tid, ElementType eType)
        {
        }
    }



    public class MeshEditorDeleteVertexOp : MeshEditorOpType
    {
        public override ElementType MeshElement { get { return ElementType.Vertex; } }

        protected int active_vid = -1;

        public override void BeginStroke(Frame3f vStartPosL, int tid, ElementType eType) {
            base.BeginStroke(vStartPosL, tid, eType);
            active_vid = tid;
        }
        public override void UpdateStroke(Frame3f vNextPosL, int tid, ElementType eType) {
            base.UpdateStroke(vNextPosL, tid, eType);
            active_vid = tid;
        }
        public override void EndStroke() {
            if (active_vid != -1) {
                Editor.RemoveVertex(active_vid, true);
                active_vid = -1;
            }
        }
    }
    public class MeshEditorDeleteRingOp : MeshEditorDeleteVertexOp {
        public override BoundaryType BoundaryFilter { get { return BoundaryType.OnlyBoundary; } }
        public override void EndStroke() {
            if (active_vid != -1) {
                Editor.RemoveBorderRing(active_vid, true);
                active_vid = -1;
            }
        }
    }



    public class MeshEditorDeleteTriOp : MeshEditorOpType
    {
        public override ElementType MeshElement { get { return ElementType.Triangle; } }

        protected int active_tid = -1;

        public override void BeginStroke(Frame3f vStartPosL, int tid, ElementType eType) {
            base.BeginStroke(vStartPosL, tid, eType);
            active_tid = tid;
        }
        public override void UpdateStroke(Frame3f vNextPosL, int tid, ElementType eType) {
            base.UpdateStroke(vNextPosL, tid, eType);
            active_tid = tid;
        }
        public override void EndStroke() {
            if (active_tid != -1) {
                Editor.RemoveTriangle(active_tid, true);
                active_tid = -1;
            }
        }
    }
    public class MeshEditorDeleteComponentOp : MeshEditorDeleteTriOp {
        public override void EndStroke() {
            if (active_tid != -1) {
                Editor.RemoveComponent(active_tid, true);
                active_tid = -1;
            }
        }
    }





    public class MeshEditorPokeTriOp : MeshEditorOpType
    {
        public override ElementType MeshElement { get { return ElementType.Triangle; } }
        public override bool SnapToElement { get { return false; } }

        int active_tid = -1;

        public override void BeginStroke(Frame3f vStartPosL, int tid, ElementType eType)
        {
            base.BeginStroke(vStartPosL, tid, eType);
            active_tid = tid;
        }
        public override void UpdateStroke(Frame3f vNextPosL, int tid, ElementType eType)
        {
            base.UpdateStroke(vNextPosL, tid, eType);
            active_tid = tid;
        }
        public override void EndStroke()
        {
            if (active_tid != -1) {
                Editor.PokeFace(active_tid, PreviousPosL, true);
                active_tid = -1;
            }
        }
    }




    public class MeshEditorDeleteEdgeOp : MeshEditorOpType
    {
        public override ElementType MeshElement { get { return ElementType.Edge; } }

        int active_eid = -1;

        public override void BeginStroke(Frame3f vStartPosL, int eid, ElementType eType) {
            base.BeginStroke(vStartPosL, eid, eType);
            active_eid = eid;
        }
        public override void UpdateStroke(Frame3f vNextPosL, int eid, ElementType eType) {
            base.UpdateStroke(vNextPosL, eid, eType);
            active_eid = eid;
        }
        public override void EndStroke() {
            if (active_eid != -1) {
                Editor.RemoveEdge(active_eid, true);
                active_eid = -1;
            }
        }
    }





    public class MeshEditorFlipEdgeOp : MeshEditorOpType
    {
        public override ElementType MeshElement { get { return ElementType.Edge; } }
        public override BoundaryType BoundaryFilter { get { return BoundaryType.OnlyInternal; } }

        int active_eid = -1;

        public override void BeginStroke(Frame3f vStartPosL, int eid, ElementType eType)
        {
            base.BeginStroke(vStartPosL, eid, eType);
            active_eid = eid;
        }
        public override void UpdateStroke(Frame3f vNextPosL, int eid, ElementType eType)
        {
            base.UpdateStroke(vNextPosL, eid, eType);
            active_eid = eid;
        }
        public override void EndStroke()
        {
            if (active_eid != -1) {
                Editor.FlipEdge(active_eid, true);
                active_eid = -1;
            }
        }
    }



    public class MeshEditorSplitEdgeOp : MeshEditorOpType
    {
        public override ElementType MeshElement { get { return ElementType.Edge; } }
        public override bool SnapToElement { get { return false; } }

        int active_eid = -1;

        public override void BeginStroke(Frame3f vStartPosL, int eid, ElementType eType)
        {
            base.BeginStroke(vStartPosL, eid, eType);
            active_eid = eid;
        }
        public override void UpdateStroke(Frame3f vNextPosL, int eid, ElementType eType)
        {
            base.UpdateStroke(vNextPosL, eid, eType);
            active_eid = eid;
        }
        public override void EndStroke()
        {
            if (active_eid != -1) {
                Editor.SplitEdge(active_eid, PreviousPosL, true);
                active_eid = -1;
            }
        }
    }



    public class MeshEditorCollapseEdgeOp : MeshEditorOpType
    {
        public override ElementType MeshElement { get { return ElementType.Edge; } }
        public override bool SnapToElement { get { return false; } }

        int active_eid = -1;

        public override void BeginStroke(Frame3f vStartPosL, int eid, ElementType eType)
        {
            base.BeginStroke(vStartPosL, eid, eType);
            active_eid = eid;
        }
        public override void UpdateStroke(Frame3f vNextPosL, int eid, ElementType eType)
        {
            base.UpdateStroke(vNextPosL, eid, eType);
            active_eid = eid;
        }
        public override void EndStroke()
        {
            if (active_eid != -1) {
                Editor.CollapseEdge(active_eid, PreviousPosL, true);
                active_eid = -1;
            }
        }
    }



    public class MeshEditorBridgeEdgesOp : MeshEditorOpType
    {
        public override ElementType MeshElement { get { return ElementType.Edge; } }
        public override BoundaryType BoundaryFilter { get { return BoundaryType.OnlyBoundary; } }

        int active_eid = -1;
        int start_eid = -1;

        public override void BeginStroke(Frame3f vStartPosL, int eid, ElementType eType)
        {
            base.BeginStroke(vStartPosL, eid, eType);
            active_eid = eid;
            start_eid = eid;
        }
        public override void UpdateStroke(Frame3f vNextPosL, int eid, ElementType eType)
        {
            base.UpdateStroke(vNextPosL, eid, eType);
            active_eid = eid;
        }
        public override void EndStroke()
        {
            if (active_eid != -1 && active_eid != start_eid) {
                Editor.BridgeEdges(start_eid, active_eid, true);
                active_eid = -1;
                start_eid = -1;
            }
        }
    }






}
