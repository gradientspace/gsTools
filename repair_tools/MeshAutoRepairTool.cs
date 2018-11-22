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
    public class MeshAutoRepairToolBuilder : BaseCombineInputSOToolBuilder<MeshAutoRepairTool>
    {
        public override MeshAutoRepairTool build_tool(FScene scene, List<DMeshSO> meshes) {
            return new MeshAutoRepairTool(scene, meshes);
        }
    }


    public class MeshAutoRepairTool : BaseCombineInputSOTool<MeshAutoRepairTool>
    {
        static readonly public string Identifier = "mesh_autorepair";

        override public string Name { get { return "MeshAutoRepair"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        public MeshAutoRepairTool(FScene scene, List<DMeshSO> meshSOs) : base(scene, meshSOs)
        {
        }


        /*
         * These will be called by base class 
         */


        protected override BaseDMeshSourceOp edit_op_factory(DMeshSourceOp meshSourceOp) {
            return new MeshAutoRepairOp() {
                MeshSource = meshSourceOp
            };
        }


        public override void postprocess_target_objects()
        {
        }

        protected MeshAutoRepairOp RepairOp {
            get { return base.EditOp as MeshAutoRepairOp; }
        }


        public enum RemoveInsideModes
        {
            None = MeshAutoRepairOp.RemoveInsideModes.None,
            Interior = MeshAutoRepairOp.RemoveInsideModes.Interior,
            Occluded = MeshAutoRepairOp.RemoveInsideModes.Occluded
        }
        public RemoveInsideModes InsideMode {
            get { return (RemoveInsideModes)get_remove_inside_mode_int(); }
            set { set_remove_inside_mode_int((int)value); }
        }
        int get_remove_inside_mode_int() { return (int)RepairOp.InsideMode; }
        void set_remove_inside_mode_int(int value) { RepairOp.InsideMode = (MeshAutoRepairOp.RemoveInsideModes)value; }


        public double MinEdgeLength {
            get { return get_min_edge_length(); }
            set { set_min_edge_length(value); }
        }
        double get_min_edge_length() { return RepairOp.MinEdgeLength; }
        void set_min_edge_length(double value) { RepairOp.MinEdgeLength = value; }


        public int ErosionRounds {
            get { return get_erosion_rounds(); }
            set { set_erosion_rounds(value); }
        }
        int get_erosion_rounds() { return RepairOp.ErosionIterations; }
        void set_erosion_rounds(int value) { RepairOp.ErosionIterations = value; }



        public bool InvertResult {
            get { return get_invert_result(); }
            set { set_invert_result(value); }
        }
        bool get_invert_result() { return RepairOp.InvertResult; }
        void set_invert_result(bool value) { RepairOp.InvertResult = value; }




        protected override void initialize_parameters()
        {
            Parameters.Register("invert_result", get_invert_result, set_invert_result, false, false);
            Parameters.Register("erosion_rounds", get_erosion_rounds, set_erosion_rounds, 5, false)
                .SetValidRange(0, 10000);
            Parameters.Register("min_edge_length", get_min_edge_length, set_min_edge_length, 0.0001, false)
                .SetValidRange(0.0, 1.0);
            Parameters.Register("remove_inside_mode", get_remove_inside_mode_int, set_remove_inside_mode_int, (int)RemoveInsideModes.None, false)
                .SetValidRange((int)RemoveInsideModes.None, (int)RemoveInsideModes.Occluded);

        }

    }

}