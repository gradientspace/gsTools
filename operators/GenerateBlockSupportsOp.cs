// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;

namespace gs
{
    public class GenerateBlockSupportsOp : BaseDMeshSourceOp
    {

        double overhang_angle = 30.0;
        public double OverhangAngleDeg {
            get { return overhang_angle; }
            set {
                double angle = MathUtil.Clamp(value, 0, 90);
                if (Math.Abs(overhang_angle - angle) > MathUtil.ZeroTolerancef) { overhang_angle = angle; invalidate(); }
            }
        }


        double grid_cell_size = 1.0;
        public double GridCellSize {
            get { return grid_cell_size; }
            set {
                double set_size = MathUtil.Clamp(value, 0.0001, 10000.0);
                if (Math.Abs(grid_cell_size - set_size) > MathUtil.ZeroTolerancef) { grid_cell_size = value; invalidate(); }
            }
        }


        double min_y = 0.0;
        public double MinY {
            get { return min_y; }
            set {
                if (Math.Abs(min_y - value) > MathUtil.ZeroTolerancef) { min_y = value; invalidate(); }
            }
        }


        bool subtract_input = false;
        public virtual bool SubtractInput {
            get { return subtract_input; }
            set { if (subtract_input != value) { subtract_input = value; invalidate(); } }
        }


        double subtract_offset_distance = 0.0;
        public double SubtractOffsetDistance {
            get { return subtract_offset_distance; }
            set {
                if (Math.Abs(subtract_offset_distance - value) > MathUtil.ZeroTolerancef) { subtract_offset_distance = value; invalidate(); }
            }
        }


        DMeshSourceOp mesh_source;
        public DMeshSourceOp MeshSource {
            get { return mesh_source; }
            set {
                if (mesh_source != null)
                    mesh_source.OperatorModified -= on_input_modified;
                mesh_source = value;
                if (mesh_source != null)
                    mesh_source.OperatorModified += on_input_modified;
                invalidate();
            }
        }


        protected virtual void on_input_modified(ModelingOperator op)
        {
            base.invalidate();
        }


        DMesh3 ResultMesh;


        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            if (MeshSource == null)
                throw new Exception("GenerateBlockSupportsOp: must set valid MeshSource to compute!");

            try {
                ResultMesh = null;

                DMesh3 mesh = MeshSource.GetDMeshUnsafe();

                BlockSupportGenerator supportgen = new BlockSupportGenerator(mesh, GridCellSize);
                supportgen.OverhangAngleDeg = this.overhang_angle;
                supportgen.ForceMinY = (float)this.min_y;
                supportgen.SubtractMesh = this.subtract_input;
                supportgen.SubtractMeshOffset = this.SubtractOffsetDistance;
                supportgen.Generate();

                if (is_invalidated())
                    goto skip_to_end;

                ResultMesh = supportgen.SupportMesh;
                Reducer reducer = new Reducer(ResultMesh);
                reducer.Progress = new ProgressCancel(is_invalidated);
                reducer.ReduceToEdgeLength(GridCellSize / 2);

                skip_to_end:
                if (is_invalidated())
                    ResultMesh = null;
                base.complete_update();

            } catch (Exception e) {
                PostOnOperatorException(e);
                ResultMesh = base.make_failure_output(MeshSource.GetDMeshUnsafe());
                base.complete_update();
            }

        }




        /*
         * IMeshSourceOp / DMeshSourceOp interface
         */

        public override IMesh GetIMesh()
        {
            if (base.requires_update())
                Update();
            return ResultMesh;
        }

        public override DMesh3 GetDMeshUnsafe() {
            return (DMesh3)GetIMesh();
        }

        public override bool HasSpatial {
            get { return false; }
        }
        public override ISpatial GetSpatial()
        {
            return null;
        }

        public override DMesh3 ExtractDMesh()
        {
            Update();
            var result = ResultMesh;
            ResultMesh = null;
            base.result_consumed();
            return result;
        }



    }


}
