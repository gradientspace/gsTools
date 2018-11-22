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


    public class ReduceToolBuilder : BaseMultipleDMeshSOToolBuilder<ReduceTool>
    {
        public override ReduceTool build_tool(FScene scene, List<DMeshSO> meshes)
        {
            return new ReduceTool(scene, meshes) {
                OnApplyF = this.OnApplyF,
                PreviewMaterial = this.PreviewMaterial
            };
        }
    }


    


    public class ReduceTool : BaseMultipleDMeshSOTool<ReduceTool>
    {
        static readonly public string Identifier = "Reduce";

        override public string Name { get { return "Reduce"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        public ReduceTool(FScene scene, List<DMeshSO> meshSOs) : base(scene,meshSOs)
        {
        }


        /*
         * These will be called by base class 
         */


        protected override BaseDMeshSourceOp edit_op_factory(TargetObject o) {
            return new ReduceOp() {
                MeshSource = o.MeshSourceOp
            };
        }

        int MaxVertexCount = 0;
        int MaxTriangleCount = 0;
        public override void postprocess_target_objects() {
            foreach ( var target in TargetObjects ) {
                MaxVertexCount = Math.Max(MaxVertexCount, target.SO.Mesh.VertexCount);
                MaxTriangleCount = Math.Max(MaxTriangleCount, target.SO.Mesh.TriangleCount);
            }
            foreach ( var op in Operators ) {
                op.VertexCount = MaxVertexCount / 2;
                op.TriangleCount = MaxTriangleCount / 2;
            }

            double max_scene_dim = 0;
            foreach (var target in TargetObjects)
                max_scene_dim = Math.Max(max_scene_dim, target.SO.Mesh.CachedBounds.MaxDim / target.sceneToObjUnitScale);
            double scene_length = 3 * ToolDefaults.DefaultTargetEdgeLengthF(max_scene_dim);
            set_min_edge_length(scene_length);
        }


        /*
         * Parameters
         */

        ReduceOp mainOp;
        IEnumerable<ReduceOp> Operators {
            get { foreach (var obj in TargetObjects) yield return obj.EditOp as ReduceOp; }
        }

        public ReduceOp.TargetModes ReduceMode {
            get { return get_target_mode(); }
            set { set_target_mode(value); }
        }
        ReduceOp.TargetModes get_target_mode() { return mainOp.TargetMode; }
        void set_target_mode(ReduceOp.TargetModes value) { foreach ( var op in Operators) op.TargetMode = value; }
        int get_target_mode_int() { return (int)get_target_mode(); }
        void set_target_mode_int(int value) { set_target_mode((ReduceOp.TargetModes)value); }


        public int TriangleCount {
            get { return get_triangle_count(); }
            set { set_triangle_count(value); }
        }
        int get_triangle_count() { return mainOp.TriangleCount; }
        void set_triangle_count(int value) { foreach (var op in Operators) op.TriangleCount = value; }


        public int VertexCount {
            get { return get_vertex_count(); }
            set { set_vertex_count(value); }
        }
        int get_vertex_count() { return mainOp.VertexCount; }
        void set_vertex_count(int value) { foreach (var op in Operators) op.VertexCount = value; }


        public double MinEdgeLength {
            get { return get_min_edge_length(); }
            set { set_min_edge_length(value); }
        }
        double scene_min_edge_length = 1.0;
        double get_min_edge_length() { return scene_min_edge_length; }
        void set_min_edge_length(double value) {
            scene_min_edge_length = value;
            foreach (var target in TargetObjects)
                (target.EditOp as ReduceOp).MinEdgeLength = scene_min_edge_length * target.sceneToObjUnitScale;
        }


        public bool ReprojectToInput {
            get { return get_reproject(); }
            set { set_reproject(value); }
        }
        bool get_reproject() { return mainOp.ReprojectToInput; }
        void set_reproject(bool value) { foreach (var op in Operators) op.ReprojectToInput = value; }



        protected override void initialize_parameters()
        {
            mainOp = TargetObjects[0].EditOp as ReduceOp;

            Parameters.Register("target_mode", get_target_mode_int, set_target_mode_int, (int)ReduceOp.TargetModes.TriangleCount, false)
                .SetValidRange(0, (int)ReduceOp.TargetModes.MinEdgeLength);
            Parameters.Register("triangle_count", get_triangle_count, set_triangle_count, MaxTriangleCount / 2, false)
                .SetValidRange(0, MaxTriangleCount);
            Parameters.Register("vertex_count", get_vertex_count, set_vertex_count, MaxVertexCount / 2, false)
                .SetValidRange(0, MaxVertexCount);
            Parameters.Register("min_edge_length", get_min_edge_length, set_min_edge_length, 1.0, false)
                .SetValidRange(0.0001, 1000.0);
            Parameters.Register("reproject", get_reproject, set_reproject, true, false);
        }



        /*
         *  Utility functions
         */
        public void CurrentTriStats(out int verts, out int tris, out int edges)
        {
            verts = tris = edges = 0;
            foreach (var obj in TargetObjects) {
                if (obj.Preview != null) {
                    verts += obj.Preview.Mesh.VertexCount;
                    tris += obj.Preview.Mesh.TriangleCount;
                    edges += obj.Preview.Mesh.EdgeCount;
                }
            }
        }

    }

}