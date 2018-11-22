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
    public abstract class BaseMultipleDMeshSOToolBuilder<T> : IToolBuilder where T : BaseMultipleDMeshSOTool<T>
    {
        public Action<T, Dictionary<DMeshSO, DMeshSO>> OnApplyF = null;
        public SOMaterial PreviewMaterial = null;
        public SOMaterial ErrorMaterial = null;

        public Action<T> BuildCustomizeF = null;

        public virtual bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
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
            T tool = build_tool(scene, meshes);
            tool.OnApplyF = this.OnApplyF;
            tool.PreviewMaterial = this.PreviewMaterial;
            tool.ErrorMaterial = this.ErrorMaterial;
            if (BuildCustomizeF != null)
                BuildCustomizeF(tool);
            return tool;
        }

        public abstract T build_tool(FScene scene, List<DMeshSO> meshes);
    }





    /// <summary>
    /// This is a base Tool for handling the case where we want to apply the same 
    /// background-compute op to multiple DMeshSOs. EG in a plane cut, we want to
    /// cut all selected objects with same plane, but we want to keep them separate
    /// (as opposed to make closed, which combines inputs).
    /// </summary>
    public abstract class BaseMultipleDMeshSOTool<T> : BaseToolCore, ITool where T : class, ITool
    {
        abstract public string Name { get; }
        abstract public string TypeIdentifier { get; }


        protected List<DMeshSO> InputSOs;
        public IEnumerable<DMeshSO> Targets { get { return InputSOs; } }


        /// <summary>
        /// This is the material set on the DMeshSO during the preview
        /// </summary>
        public SOMaterial PreviewMaterial;

        /// <summary>
        /// This is the material set on the DMeshSO during the preview if there is an error
        /// </summary>
        public SOMaterial ErrorMaterial;

        /// <summary>
        /// Called on Apply(). By default, does nothing, as the DMeshSO is already
        /// in scene as preview. Override to implement your own behavior
        /// </summary>
        public Action<T, Dictionary<DMeshSO, DMeshSO>> OnApplyF;


        /// <summary> If true, exception messages are printed </summary>
        public bool VerboseOutput = true;

        /// <summary> If false, spatial data structures are disabled on preview SOs, so they are not hit/nearest testable </summary>
        public bool EnablePreviewSpatial = false;



        InputBehaviorSet behaviors;
        virtual public InputBehaviorSet InputBehaviors {
            get { return behaviors; }
            set { behaviors = value; }
        }


        public BaseMultipleDMeshSOTool(FScene scene, List<DMeshSO> meshSOs)
        {
            this.Scene = scene;
            if (meshSOs == null || meshSOs.Count == 0)
                this.InputSOs = scene.FindSceneObjectsOfType<DMeshSO>(false, true, true);
            else
                this.InputSOs = meshSOs;

            // no behaviors..
            behaviors = new InputBehaviorSet();

            // hide target objects
            set_targets_visibility(false);

            // disable transformations
            Scene.Context.TransformManager.PushOverrideGizmoType(TransformManager.NoGizmoType);

            // set up parameters
            parameters = new ParameterSet();
        }


        // clients must implement these internal functions
        protected abstract BaseDMeshSourceOp edit_op_factory(TargetObject o);
        public virtual void postprocess_target_objects() { }
        protected abstract void initialize_parameters();




        protected class TargetObject
        {
            public DMeshSO SO;
            public DMeshSO Preview;
            public WrapDMeshSourceOp MeshSourceOp;
            public BaseDMeshSourceOp EditOp;
            public ThreadedMeshComputeOp Compute;
            public bool current_result_ok;

            public Frame3f sceneFrame;
            public double sceneToObjUnitScale;
        }
        List<TargetObject> objects;
        protected IList<TargetObject> TargetObjects {
            get { return objects; }
        }

        List<SceneObject> inputSelection;

        public virtual void Setup()
        {
            // push history stream, so that we can do undo/redo internal to tool,
            // that will not end up in external history
            push_history_stream();

            if (OnApplyF == null)
                OnApplyF = this.add_so_to_scene;

            if (PreviewMaterial == null)
                PreviewMaterial = SOMaterial.CreateMesh("tool_generated", Colorf.DimGrey);
            if (ErrorMaterial == null)
                ErrorMaterial = SOMaterial.CreateMesh("tool_generated_error", Colorf.VideoRed);

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
                o.sceneFrame = so.GetLocalFrame(CoordSpace.SceneCoords);
                o.sceneToObjUnitScale = SceneTransforms.SceneToObject(o.SO, 1.0f);

                o.MeshSourceOp = new WrapDMeshSourceOp() {
                    MeshSourceF = () => { return so.Mesh; },
                    SpatialSourceF = () => { return so.Spatial; }
                };
                o.EditOp = edit_op_factory(o);
                o.Compute = new ThreadedMeshComputeOp() {
                    MeshSource = o.EditOp
                };

                o.Preview = new DMeshSO() { EnableSpatial = EnablePreviewSpatial };
                o.Preview.Create(new DMesh3(), PreviewMaterial);
                o.Preview.SetLocalFrame(so.GetLocalFrame(CoordSpace.ObjectCoords), CoordSpace.ObjectCoords);
                o.Preview.SetLocalScale(so.GetLocalScale());
                Scene.AddSceneObject(o.Preview);

                objects.Add(o);
            }

            postprocess_target_objects();
            base_initialize_parameters();
        }



        virtual public void PreRender()
        {
            if (in_shutdown())
                return;

            try {
                is_computing = false;
                foreach (var obj in objects) {
                    List<ModelingOpException> exceptions = null;

                    DMeshOutputStatus result = obj.Compute.CheckForNewMesh();
                    is_computing |= (result.State == DMeshOutputStatus.States.Computing);
                    if (result.State == DMeshOutputStatus.States.Ready) {

                        process_new_result(obj, result);
                        obj.current_result_ok = (result.IsErrorOutput() == false);

                        var setMesh = result.Mesh;
                        if (result.Mesh.CompactMetric < 0.8)
                            setMesh = new DMesh3(result.Mesh, true);
                        obj.Preview.ReplaceMesh(setMesh, true);
                        obj.Preview.AssignSOMaterial((obj.current_result_ok) ? PreviewMaterial : ErrorMaterial);
                        exceptions = result.ComputeExceptions;
                    }

                    if (obj.Compute.HaveBackgroundException) {
                        Exception e = obj.Compute.ExtractBackgroundException();
                        DebugUtil.Log(2, GetType().ToString() + ".PreRender: exception in background compute: " + e.Message);
                        DebugUtil.Log(2, e.StackTrace);
                    }
                    if (exceptions != null) {
                        foreach (var mopex in exceptions) {
                            DebugUtil.Log(2, GetType().ToString() + ".PreRender: exception in background compute " + mopex.op.GetType().ToString() + " : " + mopex.e.Message);
                            DebugUtil.Log(2, mopex.e.StackTrace);
                        }
                    }
                }

            } catch (Exception e) {
                DebugUtil.Log(2, Name + "Tool.PreRender: caught exception! " + e.Message);
            }
        }


        /// <summary>
        /// subclasses can override this to implement custom behavior
        /// </summary>
        protected virtual void process_new_result(TargetObject obj, DMeshOutputStatus result)
        {
        }



        virtual public void Shutdown()
        {
            begin_shutdown();

            // terminate any computes
            foreach (var obj in objects) {
                if (obj.Compute.IsComputing) {
                    obj.EditOp.ForceInvalidate();
                }
            }

            // if we did not Apply(), this history stream is still here...
            pop_history_stream();

            foreach (var obj in objects) {
                if (obj.Preview != null) {
                    Scene.RemoveSceneObject(obj.Preview, true);
                    obj.Preview = null;
                }
            }

            Scene.Context.TransformManager.PopOverrideGizmoType();

            set_targets_visibility(true);
        }



        virtual public bool HasApply { get { return true; } }
        virtual public bool CanApply {
            get {
                foreach (var obj in objects) {
                    if (obj.Compute.IsComputing || obj.Compute.ResultConsumed == false || obj.current_result_ok == false)
                        return false;
                }
                return true;
            }
        }
        virtual public void Apply()
        {
            if (OnApplyF != null) {

                set_allow_selection_changes(true);

                // pop the history stream we pushed
                pop_history_stream();

                // restore input selection
                Scene.ClearSelection();
                foreach (var so in inputSelection)
                    Scene.Select(so, false);

                Dictionary<DMeshSO, DMeshSO> result = new Dictionary<DMeshSO, DMeshSO>();
                foreach (var so in objects) {
                    so.Preview.EnableSpatial = true;
                    result.Add(so.SO, so.Preview);
                }

                // apply
                OnApplyF(this as T, result);

                set_allow_selection_changes(false);

                // these have been consumed...
                foreach (var obj in objects) {
                    obj.Preview = null;
                }
            }
        }




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


        protected virtual void base_initialize_parameters()
        {
            parameters.Register("computing", get_is_computing, set_is_computing, false, false);
            initialize_parameters();
        }



        void add_so_to_scene(T tool, Dictionary<DMeshSO, DMeshSO> result)
        {
            // already added
        }


        protected virtual void set_targets_visibility(bool bVisible) {
            foreach (var so in this.InputSOs) {
                SceneUtil.SetVisible(so, bVisible);
            }
        }


        protected virtual void set_preview_visibility(bool bVisible) {
            foreach (var obj in objects) {
                if (obj.Preview != null)
                    SceneUtil.SetVisible(obj.Preview, bVisible);
            }
        }






    }








}