using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;

namespace gs
{
    public static class ToolDefaults
    {
        public static Func<AxisAlignedBox3d, double> DefaultVoxelSceneSizeF = (bounds) => {
            return (int)(bounds.MaxDim / 100.0) + 1;
        };


        public static Func<double, double> DefaultTargetEdgeLengthF = (scene_dimension) => {
            return (int)(scene_dimension / 50.0) + 1;
        };

    }
}
