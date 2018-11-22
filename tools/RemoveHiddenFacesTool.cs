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

    public class RemoveHiddenFacesToolBuilder : BaseCombineInputSOToolBuilder<RemoveHiddenFacesTool>
    {
        public SOMaterial HiddenPreviewMaterial = null;

        public override RemoveHiddenFacesTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new RemoveHiddenFacesTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial,
                HiddenPreviewMaterial = this.HiddenPreviewMaterial
            };
        }
    }




    public class RemoveHiddenFacesTool : BaseCombineInputSOTool<RemoveHiddenFacesTool>
    {
        static readonly public string Identifier = "remove_hidden_faces";

        override public string Name { get { return "RemoveHiddenFaces"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        /// <summary>
        /// This material is used to show what will be removed (ie hidden faces)
        /// </summary>
        public SOMaterial HiddenPreviewMaterial;


        public RemoveHiddenFacesTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
        }


        /*
         * These will be called by base class 
         */

        protected override BaseDMeshSourceOp edit_op_factory(DMeshSourceOp meshSourceOp)
        {
            return new RemoveHiddenFacesOp() {
                MeshSource = meshSourceOp
            };
        }


        protected DMeshSO RemovedSO;

        public override void postprocess_target_objects()
        {
            if (HiddenPreviewMaterial == null)
                HiddenPreviewMaterial = SOMaterial.CreateTransparent("remove_hidden_generated", new Colorf(Colorf.VideoRed, 0.5f));

            RemovedSO = new DMeshSO();
            RemovedSO.Create(new DMesh3(), HiddenPreviewMaterial);
            RemovedSO.SetLayer(FPlatform.WidgetOverlayLayer);
            RemovedSO.DisableShadows();
            Scene.AddSceneObject(RemovedSO);
            RemovedSO.SetLocalFrame(PreviewSO.GetLocalFrame(CoordSpace.SceneCoords), CoordSpace.SceneCoords);
            RemovedSO.SetLocalScale(PreviewSO.GetLocalScale());
        }


        protected override void process_new_result(DMeshOutputStatus result)
        {
            if (result.Mesh != null) {
                DSubmesh3 submesh = result.Mesh.FindMetadata("removed_submesh") as DSubmesh3;
                if (submesh != null) {
                    RemovedSO.ReplaceMesh(submesh.SubMesh, true);
                    result.Mesh.RemoveMetadata("removed_submesh");
                }
            }
        }


        public override void PreRender()
        {
            base.PreRender();

            if (RemovedSO != null)
                SceneUtil.SetVisible(RemovedSO, show_removed);
        }


        public override void Shutdown()
        {
            if (RemovedSO != null) {
                Scene.RemoveSceneObject(RemovedSO, true);
                RemovedSO = null;
            }
            base.Shutdown();
        }



        protected RemoveHiddenFacesOp RemoveOp {
            get { return base.EditOp as RemoveHiddenFacesOp; }
        }


        public enum CalculationMode
        {
            RayParity = RemoveHiddenFacesOp.CalculationMode.RayParity,
            WindingNumber = RemoveHiddenFacesOp.CalculationMode.WindingNumber,
            OcclusionTest = RemoveHiddenFacesOp.CalculationMode.SimpleOcclusionTest
        }
        public CalculationMode InsideMode {
            get { return (CalculationMode)get_inside_mode_int(); }
            set { set_inside_mode_int((int)value); }
        }
        int get_inside_mode_int() { return (int)RemoveOp.InsideMode; }
        void set_inside_mode_int(int value) { RemoveOp.InsideMode = (RemoveHiddenFacesOp.CalculationMode)value; }


        public bool AllHiddenVertices {
            get { return get_all_hidden_vertices(); }
            set { set_all_hidden_vertices(value); }
        }
        bool get_all_hidden_vertices() { return RemoveOp.AllHiddenVertices; }
        void set_all_hidden_vertices(bool value) { RemoveOp.AllHiddenVertices = value; }


        bool show_removed = true;
        public bool ShowRemovePreview {
            get { return show_removed; }
            set { show_removed = value; }
        }
        bool get_show_removed_preview() { return show_removed; }
        void set_show_removed_preview(bool value) { show_removed = value; }


        protected override void initialize_parameters()
        {
            Parameters.Register("inside_mode", get_inside_mode_int, set_inside_mode_int, (int)CalculationMode.RayParity, false)
                .SetValidRange((int)CalculationMode.RayParity, (int)CalculationMode.OcclusionTest);
            Parameters.Register("all_hidden_vertices", get_all_hidden_vertices, set_all_hidden_vertices, false, false);
            Parameters.Register("show_removed", get_show_removed_preview, set_show_removed_preview, false, false);
        }

    }


}