﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils;
using Valve.VR;
using VVVV.Utils.VMath;
using SlimDX;
using SlimDX.Direct3D11;
using System.Runtime.InteropServices;
using System.ComponentModel.Composition;

namespace VVVV.Nodes.ValveOpenVR
{
    [PluginInfo(Name = "Poser", Category = "OpenVR", AutoEvaluate = true, Tags = "vr, htc, vive, oculus, rift", Author = "tonfilm")]
    public class ValveOpenVRInputNode : OpenVRProducerNode, IPluginEvaluate, IDisposable, IPartImportsSatisfiedNotification
    {
        [Input("Sync After Frame", IsSingle = true, DefaultBoolean = true)]
        ISpread<bool> FSyncAfterFrame;

        [Input("Wait For Sync", IsSingle = true, DefaultBoolean = true)]
        ISpread<bool> FWaitForSync;

        [Input("Refresh Serials", IsBang = true, IsSingle = true)]
        ISpread<bool> FRefreshSerials;

        [Input("Get Timing", IsSingle = true)]
        ISpread<bool> FGetTiming;

        [Output("HMD Pose", IsSingle = true)]
        ISpread<Matrix> FHMDPoseOut;

        [Output("Lighthouse Poses")]
        ISpread<Matrix> FLighthousePosesOut;

        [Output("Controller Poses")]
        ISpread<Matrix> FControllerPosesOut;

        [Output("Tracker Poses")]
        ISpread<Matrix> FTrackerPosesOut;

        [Output("Render Poses")]
        ISpread<Matrix> FRenderPosesOut;

        [Output("Game Poses")]
        ISpread<Matrix> FGamePosesOut;

        [Output("Tracker Serial")]
        ISpread<string> FTrackerSerialOut;

        [Output("Device Class")]
        ISpread<string> FDeviceClassOut;

        [Output("Device Serial")]
        ISpread<string> FDeviceSerialOut;

        [Output("Remaining Frame Time Pre")]
        ISpread<float> FRemainingTimePre;

        [Output("Remaining Frame Time Post")]
        ISpread<float> FRemainingTimePost;

        [Import]
        IHDEHost FHDEHost;

        public void OnImportsSatisfied()
        {
            FHDEHost.MainLoop.OnPrepareGraph += MainLoop_OnPrepareGraph;
            FHDEHost.MainLoop.OnResetCache += MainLoop_OnResetCache;
        }

        private void MainLoop_OnPrepareGraph(object sender, EventArgs e)
        {
            if (FSystem != null && !FSyncAfterFrame[0])
                GetPoses();
        }

        private void MainLoop_OnResetCache(object sender, EventArgs e)
        {
            if (FSystem != null && FSyncAfterFrame[0])
                GetPoses();
        }

        void GetPoses()
        {
            //poses
            var poseCount = (int)OpenVR.k_unMaxTrackedDeviceCount;
            var renderPoses = new TrackedDevicePose_t[poseCount];
            var gamePoses = new TrackedDevicePose_t[poseCount];

            if (FGetTiming[0])
                FRemainingTimePre[0] = OpenVR.Compositor.GetFrameTimeRemaining();
            else
                FRemainingTimePre[0] = 0;

            var error = default(EVRCompositorError);

            if (FWaitForSync[0])
                error = OpenVR.Compositor.WaitGetPoses(renderPoses, gamePoses);
            else
                error = OpenVR.Compositor.GetLastPoses(renderPoses, gamePoses);

            SetStatus(error);
            if (error != EVRCompositorError.None) return;

            if (FGetTiming[0])
                FRemainingTimePost[0] = OpenVR.Compositor.GetFrameTimeRemaining();
            else
                FRemainingTimePost[0] = 0;

            OpenVRManager.RenderPoses = renderPoses;
            OpenVRManager.GamePoses = gamePoses;
        }

        const int CSerialBuilderSize = 64;
        StringBuilder FSerialBuilder = new StringBuilder(CSerialBuilderSize);
        CVRSystem FSystem;
        public override void Evaluate(int SpreadMax, CVRSystem system)
        {
            FSystem = system;

            if (OpenVRManager.RenderPoses == null)
                return;

            //poses
            var poseCount = (int)OpenVR.k_unMaxTrackedDeviceCount;
            var renderPoses = OpenVRManager.RenderPoses;
            var gamePoses = OpenVRManager.GamePoses;
            var refreshSerials = FRefreshSerials[0] || FFirstFrame;

            FRenderPosesOut.SliceCount = poseCount;
            FGamePosesOut.SliceCount = poseCount;
            FDeviceClassOut.SliceCount = poseCount;
            FDeviceSerialOut.SliceCount = poseCount;
            FLighthousePosesOut.SliceCount = 0;
            FControllerPosesOut.SliceCount = 0;
            FTrackerPosesOut.SliceCount = 0;
            if (refreshSerials)
                FTrackerSerialOut.SliceCount = 0;

            for (int i = 0; i < poseCount; i++)
            {
                FRenderPosesOut[i] = renderPoses[i].mDeviceToAbsoluteTracking.ToMatrix();
                FGamePosesOut[i] = gamePoses[i].mDeviceToAbsoluteTracking.ToMatrix();
                var deviceClass = system.GetTrackedDeviceClass((uint)i);
                FDeviceClassOut[i] = deviceClass.ToString();
                if (refreshSerials)
                    FDeviceSerialOut[i] = GetSerial(i);

                if (deviceClass == ETrackedDeviceClass.TrackingReference)
                {
                    FLighthousePosesOut.Add(FGamePosesOut[i]);
                }

                if (deviceClass == ETrackedDeviceClass.Controller)
                {
                    FControllerPosesOut.Add(FGamePosesOut[i]);
                }

                if (deviceClass == ETrackedDeviceClass.GenericTracker)
                {
                    FTrackerPosesOut.Add(FGamePosesOut[i]);
                    if (refreshSerials)
                        FTrackerSerialOut.Add(FDeviceSerialOut[i]);
                }
            }

            FHMDPoseOut[0] = FRenderPosesOut[0];
        }

        string GetSerial(int i)
        {
            var error = ETrackedPropertyError.TrackedProp_Success;
            FSerialBuilder.Clear();
            FSystem.GetStringTrackedDeviceProperty((uint)i, ETrackedDeviceProperty.Prop_SerialNumber_String, FSerialBuilder, CSerialBuilderSize, ref error);
            if (error == ETrackedPropertyError.TrackedProp_Success)
                return FSerialBuilder.ToString();
            else
                return "";
        }

        public void Dispose()
        {
            OpenVRManager.ShutDownOpenVR();
        }
    }
}
