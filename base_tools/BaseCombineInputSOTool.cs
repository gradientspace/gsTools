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
    public abstract class BaseCombineInputSOToolBuilder<T> : IToolBuilder where T : BaseCombineInputSOTool<T>
    {
        public Action<T, DMeshSO> OnApplyF = null;
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
    /// This is a base Tool for handling the case where we want to apply a single
    /// background-compute op to multiple DMeshSOs. To do this, we combine the input SOs.
    /// </summary>
    public abstract class BaseCombineInputSOTool<T> : BaseToolCore, ITool where T : class, ITool
    {
        abstract public string Name { get; }
        abstract public string TypeIdentifier { get; }

        protected List<DMeshSO> InputSOs;
        public IEnumerable<DMeshSO> Targets { get { return InputSOs; } }


        /// <summary>
        /// Called on Apply(). By default, does nothing, as the DMeshSO is already
        /// in scene as preview. Override to implement your own behavior
        /// </summary>
        public Action<T, DMeshSO> OnApplyF;


        /// <summary>
        /// This is the material set on the DMeshSO during the preview
        /// </summary>
        public SOMaterial PreviewMaterial;

        /// <summary>
        /// This is the material set on the DMeshSO during the preview if there is an error
        /// </summary>
        public SOMaterial ErrorMaterial;


        /// <summary> If true, exception messages are printed </summary>
        public bool VerboseOutput = true;


        InputBehaviorSet behaviors;
        virtual public InputBehaviorSet InputBehaviors {
            get { return behaviors; }
            set { behaviors = value; }
        }

        ParameterSet parameters;
        public ParameterSet Parameters {
            get { return parameters; }
        }


        public bool ForceSceneSpaceComputation = false;

        /// <summary> If false, spatial data structures are disabled on preview SOs, so they are not hit/nearest testable </summary>
        public bool EnablePreviewSpatial = false;


        public BaseCombineInputSOTool(FScene scene, List<DMeshSO> meshSOs)
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
        protected abstract BaseDMeshSourceOp edit_op_factory(DMeshSourceOp sourceOp);
        public virtual void postprocess_target_objects() { }
        protected abstract void initialize_parameters();




        protected DMeshSO PreviewSO;
        protected List<SceneObject> inputSelection;

        protected DMesh3 combineMesh;
        protected ConstantMeshSourceOp MeshSourceOp;
        protected BaseDMeshSourceOp EditOp;
        protected ThreadedMeshComputeOp ComputeOp;
        protected bool current_result_ok = false;

        protected Frame3f sceneFrame;
        protected double sceneToObjUnitScale;

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

            if (InputSOs.Count == 1 && ForceSceneSpaceComputation == false ) {
                combineMesh = new DMesh3(InputSOs[0].Mesh);
                sceneToObjUnitScale = SceneTransforms.SceneToObject(InputSOs[0], 1.0f);
            } else {
                combineMesh = new DMesh3();
                MeshEditor editor = new MeshEditor(combineMesh);
                foreach (var so in InputSOs) {
                    TransformSequence xform = SceneTransforms.ObjectToSceneXForm(so);
                    DMesh3 inputMesh = so.Mesh;
                    int[] mapV;
                    if (editor.AppendMesh(so.Mesh, out mapV)) {
                        MeshTransforms.PerVertexTransform(combineMesh, inputMesh, mapV, (v, old_id, new_id) => {
                            return xform.TransformP(v);
                        });
                    }
                };
                sceneToObjUnitScale = 1.0;
            }


            MeshSourceOp = new ConstantMeshSourceOp(combineMesh, true, true);
            EditOp = edit_op_factory(MeshSourceOp);
            ComputeOp = new ThreadedMeshComputeOp() {
                MeshSource = EditOp
            };

            PreviewSO = new DMeshSO() { EnableSpatial = EnablePreviewSpatial };
            PreviewSO.Create(new DMesh3(), PreviewMaterial);
            if (InputSOs.Count == 1 && ForceSceneSpaceComputation == false) {
                PreviewSO.SetLocalFrame(InputSOs[0].GetLocalFrame(CoordSpace.ObjectCoords), CoordSpace.ObjectCoords);
                PreviewSO.SetLocalScale(InputSOs[0].GetLocalScale());
            }
            Scene.AddSceneObject(PreviewSO);

            postprocess_target_objects();
            base_initialize_parameters();
        }



        virtual public void PreRender()
        {
            if (in_shutdown())
                return;

            List<ModelingOpException> exceptions = null;

            try {
                DMeshOutputStatus result = ComputeOp.CheckForNewMesh();
                is_computing = (result.State == DMeshOutputStatus.States.Computing);
                if (result.State == DMeshOutputStatus.States.Ready) {

                    process_new_result(result);
                    current_result_ok = (result.IsErrorOutput() == false);

                    var setMesh = result.Mesh;
                    if (result.Mesh.CompactMetric < 0.8)
                        setMesh = new DMesh3(result.Mesh, true);
                    PreviewSO.ReplaceMesh(setMesh, true);
                    PreviewSO.AssignSOMaterial((current_result_ok) ? PreviewMaterial : ErrorMaterial);

                    exceptions = result.ComputeExceptions;
                }
            } catch (Exception e) {
                DebugUtil.Log(2, Name + "Tool.PreRender: caught exception! " + e.Message);
            }

            if (ComputeOp.HaveBackgroundException) {
                Exception e = ComputeOp.ExtractBackgroundException();
                DebugUtil.Log(2, GetType().ToString() + ".PreRender: exception in background compute: " + e.Message);
                DebugUtil.Log(2, e.StackTrace);
            }

            if ( exceptions != null ) {
                foreach ( var mopex in exceptions ) {
                    DebugUtil.Log(2, GetType().ToString() + ".PreRender: exception in background compute " + mopex.op.GetType().ToString() + " : " + mopex.e.Message);
                    DebugUtil.Log(2, mopex.e.StackTrace);
                }
            }
        }

        /// <summary>
        /// subclasses can override this to implement custom behavior
        /// </summary>
        protected virtual void process_new_result(DMeshOutputStatus result)
        {

        }



        virtual public void Shutdown()
        {
            begin_shutdown();

            // terminate any computes
            if ( ComputeOp.IsComputing )
                EditOp.ForceInvalidate();

            pop_history_stream();

            if (PreviewSO != null) {
                Scene.RemoveSceneObject(PreviewSO, true);
                PreviewSO = null;
            }

            Scene.Context.TransformManager.PopOverrideGizmoType();

            set_targets_visibility(true);
        }



        virtual public bool HasApply { get { return true; } }
        virtual public bool CanApply {
            get {
                return ComputeOp.IsComputing == false && ComputeOp.ResultConsumed && current_result_ok;
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

                if (InputSOs.Count != 1 || ForceSceneSpaceComputation ) {
                    Frame3f sceneF = estimate_frame();
                    PreviewSO.RepositionPivot(sceneF);
                }

                // apply
                PreviewSO.EnableSpatial = true;
                OnApplyF(this as T, PreviewSO);

                set_allow_selection_changes(false);

                PreviewSO = null;
            }
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



        void add_so_to_scene(T tool, DMeshSO result)
        {
            // already added
        }



        protected virtual Frame3f estimate_frame()
        {
            if (InputSOs.Count == 1)
                return InputSOs[0].GetLocalFrame(CoordSpace.SceneCoords);

            Vector3d center = Vector3d.Zero;
            foreach (var so in InputSOs) {
                center += so.GetLocalFrame(CoordSpace.SceneCoords).Origin;
            }
            center /= InputSOs.Count;

            return new Frame3f(center);
        }



        protected virtual void set_targets_visibility(bool bVisible)
        {
            // hide target objects
            foreach (var so in this.InputSOs) {
                SceneUtil.SetVisible(so, bVisible);
            }
        }


        protected virtual void set_preview_visibility(bool bVisible)
        {
            if ( PreviewSO != null )
                SceneUtil.SetVisible(PreviewSO, bVisible);
        }





    }









}
