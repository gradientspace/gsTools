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
    public class SurfacePointToolBuilder : MultiPointToolBuilder
    {
        public Action<SurfacePointTool, SceneObject> OnApplyF = null;

        protected override MultiPointTool new_tool(FScene scene, SceneObject target)
        {
            return new SurfacePointTool(scene, target) {
                OnApplyF = this.OnApplyF
            };
        }
    }


    /// <summary>
    /// Simplification of MultiPointTool for a single surface point that is created on click-down
    /// and can be optionally repositioned on click-down
    /// </summary>
    public class SurfacePointTool : MultiPointTool
    {
        static readonly public string Identifier = "surface_point_tool";

        override public string Name { get { return "SurfacePointTool"; } }
        override public string TypeIdentifier { get { return Identifier; } }

        public const string SurfacePointName = "surface_point";


        public Action<SurfacePointTool, SceneObject> OnApplyF = null;

        public Colorf PointColor = Colorf.PivotYellow;
        public float PointSceneRadius = 2.5f;

        /// <summary>
        /// if false, then you must click on surface handle after it is created
        /// </summary>
        public bool UpdatePositionOnSurfaceClick = true;

        public SurfacePointTool(FScene scene, SceneObject target) : base(scene,target)
        {
            InputBehaviors.Add(
                new SurfacePointTool_2DBehavior(scene.Context, this) { Priority = 4 } );

            scene.Context.TransformManager.PushOverrideGizmoType(TransformManager.NoGizmoType);
        }


        public override void Shutdown()
        {
            Scene.Context.TransformManager.PopOverrideGizmoType();
            base.Shutdown();
        }



        int surface_point = -1;
        public bool IsSurfacePointInitialized { get { return surface_point != -1; } }

        public Frame3f SurfacePointSceneFrame {
            get { return GetPointPosition(surface_point); }
        }

        public void InitializeSurfacePoint(SORayHit hitPoint)
        {
            surface_point = AppendSurfacePoint(SurfacePointName, PointColor, PointSceneRadius);
            Frame3f hitFrameW = new Frame3f(hitPoint.hitPos, hitPoint.hitNormal);
            Frame3f hitFrameS = Scene.ToSceneFrame(hitFrameW);
            SetPointPosition(surface_point, hitFrameS, CoordSpace.SceneCoords);
        }
        public void UpdateSurfacePoint(SORayHit hitPoint)
        {
            Frame3f hitFrameW = new Frame3f(hitPoint.hitPos, hitPoint.hitNormal);
            Frame3f hitFrameS = Scene.ToSceneFrame(hitFrameW);
            SetPointPosition(surface_point, hitFrameS, CoordSpace.SceneCoords);
        }

        override public bool HasApply { get { return OnApplyF != null; } }
        override public bool CanApply { get { return IsSurfacePointInitialized; } }
        override public void Apply() {
            OnApplyF(this, TargetSO);
        }
    }


    class SurfacePointTool_2DBehavior : Any2DInputBehavior
    {
        FContext context;
        SurfacePointTool tool;

        public SurfacePointTool_2DBehavior(FContext s, SurfacePointTool tool)
        {
            context = s;
            this.tool = tool;
        }

        override public CaptureRequest WantsCapture(InputState input)
        {
            if (context.ToolManager.ActiveRightTool != tool)
                return CaptureRequest.Ignore;
            if (tool.UpdatePositionOnSurfaceClick == false && tool.IsSurfacePointInitialized)
                return CaptureRequest.Ignore;
            if (Pressed(input)) {
                SORayHit hit;
                if ( tool.TargetSO.FindRayIntersection(WorldRay(input), out hit)) {
                    return CaptureRequest.Begin(this);
                }
            }
            return CaptureRequest.Ignore;
        }

        override public Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            if (Pressed(input)) {
                SORayHit hit;
                if (tool.TargetSO.FindRayIntersection(WorldRay(input), out hit)) {
                    if ( tool.IsSurfacePointInitialized == false )
                        tool.InitializeSurfacePoint(hit);
                    else
                        tool.UpdateSurfacePoint(hit);
                    return Capture.Begin(this);
                }
            }
            return Capture.Ignore;
        }


        override public Capture UpdateCapture(InputState input, CaptureData data)
        {
            if (Released(input)) {
                return Capture.End;
            } else {
                SORayHit hit;
                if (tool.TargetSO.FindRayIntersection(WorldRay(input), out hit)) {
                    tool.UpdateSurfacePoint(hit);
                }
                return Capture.Continue;
            }
        }


        override public Capture ForceEndCapture(InputState input, CaptureData data)
        {
            return Capture.End;
        }
    }


}
