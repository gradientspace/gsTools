// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;
using f3;

namespace gs
{
    public class SetDimensionsToolBuilder : IToolBuilder
    {
        public Action<SetDimensionsTool, List<DMeshSO>> OnApplyF = null;

        public virtual bool IsSupported(ToolTargetType type, List<SceneObject> targets) {
            if (type == ToolTargetType.Scene)
                targets = new List<SceneObject>(targets.Where((so) => { return so is DMeshSO; }));
            foreach (var target in targets) {
                if (target is DMeshSO == false)
                    return false;
            }
            return true;
        }

        public virtual ITool Build(FScene scene, List<SceneObject> targets)
        {
            List<DMeshSO> meshes = SceneUtil.FindObjectsOfType<DMeshSO>(targets, true);
            SetDimensionsTool tool = build_tool(scene, meshes);
            tool.OnApplyF = this.OnApplyF;
            return tool;
        }

        public virtual SetDimensionsTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new SetDimensionsTool(scene, meshes);
        }
    }



    /// <summary>
    /// Tool to change dimensions of target objects. Currently only supports scaling relative
    /// to scene. There is commented out code for scaling in object coords.
    /// TODO: add modes.
    /// </summary>
    public class SetDimensionsTool : BaseToolCore, ITool
    {
        static readonly public string Identifier = "set_dimensions";

        virtual public string Name { get { return "SetDimensions"; } }
        virtual public string TypeIdentifier { get { return Identifier; } }

        protected List<DMeshSO> InputSOs;
        public IEnumerable<DMeshSO> Targets { get { return InputSOs; } }


        /// <summary>
        /// This is the material set on the DMeshSO during the preview
        /// </summary>
        public SOMaterial PreviewMaterial;

        /// <summary>
        /// Called on Apply(). By default, does nothing, as the DMeshSO is already
        /// in scene as preview. Override to implement your own behavior
        /// </summary>
        public Action<SetDimensionsTool, List<DMeshSO>> OnApplyF;


        InputBehaviorSet behaviors;
        virtual public InputBehaviorSet InputBehaviors {
            get { return behaviors; }
            set { behaviors = value; }
        }


        public SetDimensionsTool(FScene scene, List<DMeshSO> meshSOs)
        {
            this.Scene = scene;
            if (meshSOs == null || meshSOs.Count == 0)
                this.InputSOs = scene.FindSceneObjectsOfType<DMeshSO>(false, true, true);
            else
                this.InputSOs = meshSOs;

            // no behaviors..
            behaviors = new InputBehaviorSet();

            // disable transformations
            Scene.Context.TransformManager.PushOverrideGizmoType(TransformManager.NoGizmoType);

            // set up parameters
            parameters = new ParameterSet();
        }


        protected class TargetObject
        {
            public DMeshSO SO;
            public DVector<double> InputMeshV;
            public DVector<float> InputMeshN;
            public Frame3f objFrame;
            public Frame3f sceneFrame;
            public double sceneToObjUnitScale;

            public Vector3f localScale;
            public AxisAlignedBox3d localBounds;
            public AxisAlignedBox3d sceneBounds;

            public Frame3f curSceneFrame;
            public Vector3f curLocalScale;
        }
        List<TargetObject> objects;
        protected IList<TargetObject> TargetObjects {
            get { return objects; }
        }


        AxisAlignedBox3d originalDims;
        AxisAlignedBox3d currentDims;
        Vector3f sharedOriginS;

        List<SceneObject> inputSelection;

        public virtual void Setup()
        {
            // push history stream, so that we can do undo/redo internal to tool,
            // that will not end up in external history
            push_history_stream();

            // clear selection here so that multi-select GroupSO goes away, otherwise
            // when we copy frmaes below, they are relative to that GroupSO, and things move
            inputSelection = new List<SceneObject>(Scene.Selected);
            set_allow_selection_changes(true);
            Scene.ClearSelection();
            set_allow_selection_changes(false);

            objects = new List<TargetObject>();
            foreach (var so in InputSOs) {
                TargetObject o = new TargetObject();

                o.SO = so;
                o.InputMeshV = new DVector<double>(o.SO.Mesh.VerticesBuffer);
                if ( o.SO.Mesh.HasVertexNormals )
                    o.InputMeshN = new DVector<float>(o.SO.Mesh.NormalsBuffer);
                o.objFrame = so.GetLocalFrame(CoordSpace.ObjectCoords);
                o.sceneFrame = so.GetLocalFrame(CoordSpace.SceneCoords);
                o.sceneToObjUnitScale = SceneTransforms.SceneToObject(o.SO, 1.0f);

                o.localScale = so.GetLocalScale();
                o.localBounds = so.GetLocalBoundingBox();
                Box3f sb = so.GetBoundingBox(CoordSpace.SceneCoords);
                o.sceneBounds = sb.ToAABB();

                o.curLocalScale = o.localScale;
                o.curSceneFrame = o.sceneFrame;

                objects.Add(o);
            }

            if ( false && objects.Count == 1 ) {
                originalDims = objects[0].localBounds;
            } else {
                originalDims = objects[0].sceneBounds;
                for (int k = 1; k < objects.Count; ++k)
                    originalDims.Contain(objects[k].sceneBounds);
            }
            currentDims = originalDims;
            sharedOriginS = (Vector3f)originalDims.Point(0, -1, 0);

            initialize_parameters();
        }



        protected void update_dimensions()
        {

        }


        bool scale_needs_update = true;

        virtual public void PreRender()
        {
            if (in_shutdown())
                return;

            if ( scale_needs_update ) {
                currentDims = originalDims;
                currentDims.Scale(scale_x, scale_y, scale_z);
                Vector3f s = new Vector3f((float)scale_x, (float)scale_y, (float)scale_z);

                if (use_object_frame) {
                    foreach (var obj in objects)
                        apply_object_scale(obj, s);
                } else {
                    foreach (var obj in objects)
                        apply_scene_scale(obj, s);
                }

                //if (objects.Count == 1) {
                //    objects[0].curLocalScale = s * objects[0].localScale;

                //    Scene.History.PushChange(
                //        new TransformSOChange(objects[0].SO, objects[0].curLocalScale), false);
                //} else {
                //    foreach (var obj in objects) {
                //        Frame3f f = obj.sceneFrame;
                //        f.Origin = s * (f.Origin - sharedOriginS) + sharedOriginS;
                //        obj.curSceneFrame = f;
                //        obj.curLocalScale = s * obj.localScale;

                //        Scene.History.PushChange(
                //            new TransformSOChange(obj.SO, obj.curSceneFrame, CoordSpace.SceneCoords, obj.curLocalScale), false);
                //    }
                //}
                scale_needs_update = false;
            }

        }




        void apply_scene_scale(TargetObject obj, Vector3f scale)
        {
            // construct scaled scene frame and update SO
            Frame3f f = obj.sceneFrame;
            f.Origin = scale * (f.Origin - sharedOriginS) + sharedOriginS;
            obj.curSceneFrame = f;
            obj.SO.SetLocalFrame(f, CoordSpace.SceneCoords);

            Frame3f fL = obj.SO.GetLocalFrame(CoordSpace.ObjectCoords);

            // transform is to map from original obj frame into scene, scale, and then map into scaled obj frame
            TransformSequence seq = new TransformSequence();
            seq.AppendFromFrame(obj.objFrame);
            seq.AppendScale(scale, sharedOriginS);
            seq.AppendToFrame(fL);

            obj.SO.EditAndUpdateMesh((mesh) => {
                // restore original positions
                mesh.VerticesBuffer.copy(obj.InputMeshV);
                if (obj.InputMeshN != null && mesh.HasVertexNormals)
                    mesh.NormalsBuffer.copy(obj.InputMeshN);
                // apply xform
                MeshTransforms.PerVertexTransform(mesh, seq);
            }, GeometryEditTypes.VertexDeformation);
        }



        void apply_object_scale(TargetObject obj, Vector3f scale)
        {
            obj.SO.SetLocalScale(scale);
        }



        virtual public void Shutdown()
        {
            // if we did not apply, discard xforms
            if (did_apply == false) {
                scale_x = scale_y = scale_z = 1.0;
                scale_needs_update = true;
                PreRender();
            }

            begin_shutdown();

            // if we did not Apply(), this history stream is still here...
            pop_history_stream();

            Scene.Context.TransformManager.PopOverrideGizmoType();
        }


        public void PushFinalChanges()
        {
            Vector3f s = new Vector3f((float)scale_x, (float)scale_y, (float)scale_z);
            foreach (var obj in objects) {
                var vtxChange = new SetVerticesMeshChange() {
                    OldPositions = obj.InputMeshV,
                    NewPositions = new DVector<double>(obj.SO.Mesh.VerticesBuffer)
                };
                if ( obj.InputMeshN != null ) {
                    vtxChange.OldNormals = obj.InputMeshN;
                    vtxChange.NewNormals = new DVector<float>(obj.SO.Mesh.NormalsBuffer);
                }

                Scene.History.PushChange(new SetVerticesChange(obj.SO, vtxChange), true);
                Scene.History.PushChange(
                    new TransformSOChange(obj.SO,
                        obj.sceneFrame, obj.curSceneFrame, CoordSpace.SceneCoords,
                        obj.localScale, obj.curLocalScale), true);
            }
        }


        bool did_apply = false;

        virtual public bool HasApply { get { return true; } }
        virtual public bool CanApply { get { return true; } }
        virtual public void Apply()
        {
            if (OnApplyF != null) {
                did_apply = true;

                set_allow_selection_changes(true);

                // pop the history stream we pushed
                pop_history_stream();

                // restore input selection
                Scene.ClearSelection();
                foreach (var so in inputSelection)
                    Scene.Select(so, false);

                // apply
                OnApplyF(this, this.InputSOs);

                set_allow_selection_changes(false);
            }
        }


        void restore_vertices(TargetObject obj)
        {
            obj.SO.EditAndUpdateMesh((mesh) => {
                mesh.VerticesBuffer.copy(obj.InputMeshV);
                if ( obj.InputMeshN != null && mesh.HasVertexNormals )
                    mesh.NormalsBuffer.copy(obj.InputMeshN);
            }, GeometryEditTypes.VertexDeformation);
        }




        ParameterSet parameters;
        public ParameterSet Parameters {
            get { return parameters; }
        }


        double scale_x = 1.0;
        public double ScaleX {
            get { return get_scale_x(); }
            set { set_scale_x(value); }
        }
        double get_scale_x() { return scale_x; }
        void set_scale_x(double value) { scale_x = value; update_uniform(scale_x); scale_needs_update = true; }

        double scale_y = 1.0;
        public double ScaleY {
            get { return get_scale_y(); }
            set { set_scale_y(value); }
        }
        double get_scale_y() { return scale_y; }
        void set_scale_y(double value) { scale_y = value; update_uniform(scale_y); scale_needs_update = true; }

        double scale_z = 1.0;
        public double ScaleZ {
            get { return get_scale_z(); }
            set { set_scale_z(value); }
        }
        double get_scale_z() { return scale_z; }
        void set_scale_z(double value) { scale_z = value; update_uniform(scale_z); scale_needs_update = true; }


        public double DimensionX {
            get { return get_dimension_x(); }
            set { set_scale_x(value); }
        }
        double get_dimension_x() { return scale_x * originalDims.Width; }
        void set_dimension_x(double value) { scale_x = value / originalDims.Width; update_uniform(scale_x); scale_needs_update = true; }

        public double DimensionY {
            get { return get_dimension_y(); }
            set { set_dimension_y(value); }
        }
        double get_dimension_y() { return scale_y * originalDims.Height; }
        void set_dimension_y(double value) { scale_y = value / originalDims.Height; update_uniform(scale_y); scale_needs_update = true; }

        public double DimensionZ {
            get { return get_dimension_z(); }
            set { set_dimension_z(value); }
        }
        double get_dimension_z() { return scale_z * originalDims.Depth; }
        void set_dimension_z(double value) { scale_z = value / originalDims.Depth; update_uniform(scale_z); scale_needs_update = true; }


        bool uniform_scaling = true;
        public bool UniformScaling {
            get { return get_uniform_scaling(); }
            set { set_uniform_scaling(value); }
        }
        bool get_uniform_scaling() { return uniform_scaling; } 
        void set_uniform_scaling(bool value) { uniform_scaling = value; }

        void update_uniform(double new_scale) {
            if ( uniform_scaling ) {
                scale_x = scale_y = scale_z = new_scale;
            }
        }


        bool use_object_frame = false;
        public bool UseObjectFrame {
            get { return get_use_object_frame(); }
            set { set_use_object_frame(value); }
        }
        bool get_use_object_frame() { return use_object_frame; }
        void set_use_object_frame(bool value) { use_object_frame = value; scale_needs_update = true; }



        protected virtual void initialize_parameters()
        {
            Parameters.Register("scale_x", get_scale_x, set_scale_x, 1.0, false)
                .SetValidRange(0.0, 1000000.0);
            Parameters.Register("scale_y", get_scale_y, set_scale_y, 1.0, false)
                .SetValidRange(0.0, 1000000.0);
            Parameters.Register("scale_z", get_scale_z, set_scale_z, 1.0, false)
                .SetValidRange(0.0, 1000000.0);

            Parameters.Register("dimension_x", get_dimension_x, set_dimension_x, 1.0, false)
                .SetValidRange(0.0, 1000000.0);
            Parameters.Register("dimension_y", get_dimension_y, set_dimension_y, 1.0, false)
                .SetValidRange(0.0, 1000000.0);
            Parameters.Register("dimension_z", get_dimension_z, set_dimension_z, 1.0, false)
                .SetValidRange(0.0, 1000000.0);

            Parameters.Register("uniform", get_uniform_scaling, set_uniform_scaling, true, false);
            Parameters.Register("use_object_frame", get_use_object_frame, set_use_object_frame, true, false);

        }




    }

}
