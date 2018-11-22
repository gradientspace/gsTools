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

    public class SeparateSolidsToolBuilder : IToolBuilder
    {
        public Action<SeparateSolidsTool, List<DMeshSO>> OnApplyF = null;
        public SOMaterial KeepPreviewMaterial = null;
        public SOMaterial HiddenPreviewMaterial = null;

        public Action<SeparateSolidsTool> BuildCustomizeF = null;

        public bool IsSupported(ToolTargetType type, List<SceneObject> targets)
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
            SeparateSolidsTool tool = new SeparateSolidsTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                KeepPreviewMaterial = this.KeepPreviewMaterial,
                HiddenPreviewMaterial = this.HiddenPreviewMaterial
            };
            if (BuildCustomizeF != null)
                BuildCustomizeF(tool);
            return tool;
        }
    }




    public class SeparateSolidsTool : BaseToolCore, ITool
    {
        static readonly public string Identifier = "separate_solids";

        virtual public string Name { get { return "SeparateSolids"; } }
        virtual public string TypeIdentifier { get { return Identifier; } }

        protected List<DMeshSO> InputSOs;
        public IEnumerable<DMeshSO> Targets { get { return InputSOs; } }

        /// <summary>
        /// Called on Apply(). By default, does nothing, as the DMeshSO is already
        /// in scene as preview. Override to implement your own behavior
        /// </summary>
        public Action<SeparateSolidsTool, List<DMeshSO>> OnApplyF;

        /// <summary>
        /// This material is used to show what will be removed (ie hidden faces)
        /// </summary>
        public SOMaterial HiddenPreviewMaterial;

        /// <summary>
        /// This material is used to show what will be kept
        /// </summary>
        public SOMaterial KeepPreviewMaterial;

        /// <summary> If false, spatial data structures are disabled on preview SOs, so they are not hit/nearest testable </summary>
        public bool EnablePreviewSpatial = false;

        InputBehaviorSet behaviors;
        virtual public InputBehaviorSet InputBehaviors {
            get { return behaviors; }
            set { behaviors = value; }
        }


        public SeparateSolidsTool(FScene scene, List<DMeshSO> meshSOs)
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
            initialize_parameters();
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


        public bool GroupNestedShells {
            get { return get_group_nested(); }
            set { set_group_nested(value); }
        }
        bool get_group_nested() { return SeparateOp.GroupNestedShells; }
        void set_group_nested(bool value) { SeparateOp.GroupNestedShells = value; }

        public bool OrientNestedShells {
            get { return get_orient_nested(); }
            set { set_orient_nested(value); }
        }
        bool get_orient_nested() { return SeparateOp.OrientNestedShells; }
        void set_orient_nested(bool value) { SeparateOp.OrientNestedShells = value; }


        bool show_removed = true;
        public bool ShowRemovePreview {
            get { return show_removed; }
            set { show_removed = value; }
        }
        bool get_show_removed_preview() { return show_removed; }
        void set_show_removed_preview(bool value) { show_removed = value; }


        protected virtual void initialize_parameters()
        {
            parameters = new ParameterSet();
            parameters.Register("computing", get_is_computing, set_is_computing, false, false);
            parameters.Register("group_nested", get_group_nested, set_group_nested, false, false);
            parameters.Register("orient_nested", get_orient_nested, set_orient_nested, true, false);
            parameters.Register("show_removed", get_show_removed_preview, set_show_removed_preview, false, false);
        }



        protected List<DMeshSO> PreviewSOs = new List<DMeshSO>();

        DMesh3 combineMesh;
        ConstantMeshSourceOp MeshSourceOp;
        SeparateSolidsOp SeparateOp;
        ThreadedResultComputeOp<List<DMesh3>> ComputeOp;

        public virtual void Setup()
        {
            // push history stream, so that we can do undo/redo internal to tool,
            // that will not end up in external history
            push_history_stream();

            if (OnApplyF == null)
                OnApplyF = this.add_to_scene;

            combineMesh = new DMesh3();
            MeshEditor editor = new MeshEditor(combineMesh);
            foreach (var so in InputSOs) {
                DMesh3 inputMesh = so.Mesh;
                int[] mapV;
                if ( editor.AppendMesh(so.Mesh, out mapV) ) {
                    MeshTransforms.PerVertexTransform(combineMesh, inputMesh, mapV, (v, old_id, new_id) => {
                        return SceneTransforms.ObjectToSceneP(so, v);
                    });
                }
            };

            MeshSourceOp = new ConstantMeshSourceOp(combineMesh, true, true);
            SeparateOp = new SeparateSolidsOp() {
                MeshSource = MeshSourceOp
            };

            ComputeOp = new ThreadedResultComputeOp<List<DMesh3>>() {
                ResultSource = SeparateOp
            };

            if (HiddenPreviewMaterial == null)
                HiddenPreviewMaterial = SOMaterial.CreateTransparent("remove_hidden_generated", new Colorf(Colorf.DimGrey, 0.5f));
            if (KeepPreviewMaterial == null)
                KeepPreviewMaterial = SOMaterial.CreateFlatShaded("remove_keep_generated", Colorf.DimGrey);
        }


        void update_preview(List<DMesh3> meshes)
        {
            if (PreviewSOs != null) {
                foreach (var so in PreviewSOs) {
                    Scene.RemoveSceneObject(so, true);
                }
                PreviewSOs.Clear();
            }

            foreach ( var mesh in meshes ) {
                var so = new DMeshSO() { EnableSpatial = EnablePreviewSpatial };
                so.Create(mesh, KeepPreviewMaterial);
                Scene.AddSceneObject(so);
                PreviewSOs.Add(so);
            }
        }


        virtual public void PreRender()
        {
            if (in_shutdown())
                return;

            try {
                var result = ComputeOp.CheckForNewResult();
                is_computing = (result.State == OpResultState.Computing);
                if (result.State == OpResultState.Ready) {
                    List<DMesh3> resultMeshes = result.Result;
                    update_preview(resultMeshes);
                }
            } catch (Exception e) {
                DebugUtil.Log(2, Name + "Tool.PreRender: caught exception! " + e.Message);
            }

            if (ComputeOp.HaveBackgroundException) {
                Exception e = ComputeOp.ExtractBackgroundException();
                DebugUtil.Log(2, Name + "Tool.PreRender: exception in background compute: " + e.Message);
                DebugUtil.Log(2, e.StackTrace);
            }
        }


        virtual public void Shutdown()
        {
            begin_shutdown();

            if (ComputeOp.IsComputing)
                SeparateOp.ForceInvalidate();

            pop_history_stream();

            update_preview(new List<DMesh3>());

            Scene.Context.TransformManager.PopOverrideGizmoType();

            set_targets_visibility(true);
        }



        virtual public bool HasApply { get { return true; } }
        virtual public bool CanApply {
            get {
                return ComputeOp.IsComputing == false && ComputeOp.ResultConsumed;
            }
        }
        virtual public void Apply() {
            if (OnApplyF != null) {

                // center pivots
                foreach ( var so in PreviewSOs ) {
                    var bounds = so.GetBoundingBox(CoordSpace.SceneCoords);
                    Frame3f f = new Frame3f(bounds.Center);
                    so.RepositionPivot(f);

                    so.EnableSpatial = true;
                }

                set_allow_selection_changes(true);

                // pop the history stream we pushed
                pop_history_stream();

                OnApplyF(this, PreviewSOs);

                set_allow_selection_changes(false);

                PreviewSOs = null;
            }
        }



        void add_to_scene(SeparateSolidsTool tool, List<DMeshSO> previews)
        {
            // already added
        }



        void set_targets_visibility(bool bVisible)
        {
            // hide target objects
            foreach (var so in this.InputSOs) {
                SceneUtil.SetVisible(so, bVisible);
            }
        }


    }

}