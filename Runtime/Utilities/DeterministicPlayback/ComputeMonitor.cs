// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using AOT;

namespace Niantic.Lightship.AR.Utilities.DeterministicPlayback
{
    internal sealed class ComputeMonitor : IDisposable
    {
        private IntPtr _nativeHandle;

        public ComputeMonitor(IntPtr unityContext)
        {
            var coreContext = LightshipUnityContext.GetCoreContext(unityContext);
            var componentManagerHandle = PAM.NativeApi.ARDK_CoreContext_GetComponentManagerHandle(coreContext);
            _nativeHandle = Native.ARDK_ComponentManager_ComputeMonitorHandle_Acquire(componentManagerHandle);
            PrepareToCompute();
        }

        ~ComputeMonitor()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (!_nativeHandle.IsValidHandle()) return;
            Native.ARDK_ComponentManager_ComputeMonitorHandle_Release(_nativeHandle);
            _nativeHandle = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }

        public void PrepareToCompute()
        {
            Native.ARDK_ComputeMonitor_PrepareToCompute(_nativeHandle);
        }
        public bool IsDoneComputing()
        {
            return Native.ARDK_ComputeMonitor_IsDoneComputing(_nativeHandle);
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name)]
            public static extern IntPtr ARDK_ComponentManager_ComputeMonitorHandle_Acquire(
                IntPtr componentManagerHandle);

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_ComponentManager_ComputeMonitorHandle_Release(IntPtr computeMonitorHandle);

            [DllImport(LightshipPlugin.Name)]
            public static extern bool ARDK_ComputeMonitor_PrepareToCompute(IntPtr computeMonitorHandle);

            [DllImport(LightshipPlugin.Name)]
            public static extern bool ARDK_ComputeMonitor_IsDoneComputing(IntPtr computeMonitorHandle);


        }
    }
}