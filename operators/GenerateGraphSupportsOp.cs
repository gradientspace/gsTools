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
    public class GenerateGraphSupportsOp : BaseDMeshSourceOp
    {

        double overhang_angle = 30.0;
        public double OverhangAngleDeg {
            get { return overhang_angle; }
            set {
                double angle = MathUtil.Clamp(value, 5, 90);
                if (Math.Abs(overhang_angle - angle) > MathUtil.ZeroTolerancef) { overhang_angle = angle; invalidate(); }
            }
        }


        double surface_offset_distance = 0.3;
        public double SurfaceOffsetDistance {
            get { return surface_offset_distance; }
            set {
                if (Math.Abs(surface_offset_distance - value) > MathUtil.ZeroTolerancef) { surface_offset_distance = value; invalidate(); }
            }
        }


        double support_min_angle = 30.0;
        public double SupportMinAngleDeg {
            get { return support_min_angle; }
            set {
                double angle = MathUtil.Clamp(value, 0, 90);
                if (Math.Abs(support_min_angle - angle) > MathUtil.ZeroTolerancef) { support_min_angle = angle; invalidate(); }
            }
        }


        int optimize_rounds = 20;
        public int OptimizeRounds {
            get { return optimize_rounds; }
            set {
                int rounds = MathUtil.Clamp(value, 0, 1000);
                if (optimize_rounds != rounds) { optimize_rounds = rounds; invalidate(); }
            }
        }


        double grid_cell_size = 1.0;
        public double GridCellSize {
            get { return grid_cell_size; }
            set {
                double set_size = MathUtil.Clamp(value, 0.0001, 10000.0);
                if (Math.Abs(grid_cell_size - set_size) > MathUtil.ZeroTolerancef) { grid_cell_size = set_size; invalidate(); }
            }
        }





        double min_y = 0.0;
        public double MinY {
            get { return min_y; }
            set {
                if (Math.Abs(min_y - value) > MathUtil.ZeroTolerancef) { min_y = value; invalidate(); }
            }
        }


        double post_diam = 2.5;
        public double PostDiameter {
            get { return post_diam; }
            set {
                double diam = MathUtil.Clamp(value, 0.1, 100.0);
                if (Math.Abs(post_diam - diam) > MathUtil.ZeroTolerancef) { post_diam = diam; invalidate(); }
            }
        }


        double tip_diam = 1.0;
        public double TipDiameter {
            get { return tip_diam; }
            set {
                double diam = MathUtil.Clamp(value, 0.1, 100.0);
                if (Math.Abs(tip_diam - diam) > MathUtil.ZeroTolerancef) { tip_diam = diam; invalidate(); }
            }
        }


        double base_diam = 6.5;
        public double BaseDiameter {
            get { return base_diam; }
            set {
                double diam = MathUtil.Clamp(value, 0.1, 100.0);
                if (Math.Abs(base_diam - diam) > MathUtil.ZeroTolerancef) { base_diam = diam; invalidate(); }
            }
        }


        bool bottom_up = false;
        public virtual bool BottomUp {
            get { return bottom_up; }
            set { if (bottom_up != value) { bottom_up = value; invalidate(); } }
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



        DMeshAABBTree3 get_cached_spatial() {
            if ( MeshSource.HasSpatial ) {
                if (MeshSource.GetSpatial() is DMeshAABBTree3)
                    return MeshSource.GetSpatial() as DMeshAABBTree3;
            }
            DMesh3 mesh = MeshSource.GetDMeshUnsafe();
            if (internal_spatial != null && spatial_timestamp == mesh.ShapeTimestamp)
                return internal_spatial;
            internal_spatial = new DMeshAABBTree3(mesh, true);
            spatial_timestamp = mesh.ShapeTimestamp;
            return internal_spatial;
        }
        DMeshAABBTree3 internal_spatial;
        int spatial_timestamp;



        DMesh3 ResultMesh;


        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            if (MeshSource == null)
                throw new Exception("GenerateGraphSupportsOp: must set valid MeshSource to compute!");

            try {
                ResultMesh = null;

                DMesh3 mesh = MeshSource.GetDMeshUnsafe();

                GraphSupportGenerator supportgen = new GraphSupportGenerator(mesh, get_cached_spatial(), GridCellSize);
                supportgen.OverhangAngleDeg = this.overhang_angle;
                supportgen.ForceMinY = (float)this.min_y;
                supportgen.ProcessBottomUp = this.bottom_up;
                supportgen.OverhangAngleOptimizeDeg = this.support_min_angle;
                supportgen.OptimizationRounds = this.optimize_rounds;
                supportgen.GraphSurfaceDistanceOffset = this.post_diam/2 + surface_offset_distance;

                supportgen.Progress = new ProgressCancel(is_invalidated);
                supportgen.Generate();
                DGraph3 graph = supportgen.Graph;

                if (is_invalidated())
                    goto skip_to_end;

                GraphTubeMesher mesher = new GraphTubeMesher(supportgen);
                mesher.TipRadius = this.tip_diam/2;
                mesher.PostRadius = this.post_diam/2;
                mesher.GroundRadius = this.base_diam / 2;
                mesher.SamplerCellSizeHint = supportgen.CellSize / 2;
                mesher.Progress = new ProgressCancel(is_invalidated);
                mesher.Generate();

                if (is_invalidated())
                    goto skip_to_end;

                ResultMesh = mesher.ResultMesh;
                Reducer reducer = new Reducer(ResultMesh);
                reducer.Progress = new ProgressCancel(is_invalidated);
                reducer.ReduceToEdgeLength(mesher.ActualCellSize / 2);

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
