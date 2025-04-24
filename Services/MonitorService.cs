using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

namespace JL_Monitor_Brightness.Services
{
    public class MonitorService
    {
        // Win32 API для работы с физическими мониторами
        [DllImport("dxva2.dll", EntryPoint = "GetNumberOfPhysicalMonitorsFromHMONITOR")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", EntryPoint = "GetPhysicalMonitorsFromHMONITOR")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", EntryPoint = "GetMonitorBrightness")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorBrightness(IntPtr hMonitor, ref uint pdwMinimumBrightness, ref uint pdwCurrentBrightness, ref uint pdwMaximumBrightness);

        [DllImport("dxva2.dll", EntryPoint = "SetMonitorBrightness")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

        [DllImport("dxva2.dll", EntryPoint = "DestroyPhysicalMonitor")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

        [DllImport("dxva2.dll", EntryPoint = "DestroyPhysicalMonitors")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private List<PhysicalMonitorInfo> _monitors = new List<PhysicalMonitorInfo>();

        public List<PhysicalMonitorInfo> GetMonitors()
        {
            _monitors.Clear();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnum, IntPtr.Zero);
            return _monitors;
        }

        private bool MonitorEnum(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData)
        {
            uint physicalMonitorCount = 0;

            if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref physicalMonitorCount))
            {
                PHYSICAL_MONITOR[] physicalMonitors = new PHYSICAL_MONITOR[physicalMonitorCount];

                if (GetPhysicalMonitorsFromHMONITOR(hMonitor, physicalMonitorCount, physicalMonitors))
                {
                    for (int i = 0; i < physicalMonitorCount; i++)
                    {
                        var monitor = physicalMonitors[i];
                        uint minBrightness = 0, currentBrightness = 0, maxBrightness = 0;

                        if (GetMonitorBrightness(monitor.hPhysicalMonitor, ref minBrightness, ref currentBrightness, ref maxBrightness))
                        {
                            _monitors.Add(new PhysicalMonitorInfo
                            {
                                Handle = monitor.hPhysicalMonitor,
                                Description = monitor.szPhysicalMonitorDescription,
                                MinBrightness = minBrightness,
                                CurrentBrightness = currentBrightness,
                                MaxBrightness = maxBrightness,
                                Index = _monitors.Count
                            });
                        }
                    }
                }
            }

            return true;
        }

        public bool SetBrightness(PhysicalMonitorInfo monitor, uint brightness)
        {
            if (brightness < monitor.MinBrightness || brightness > monitor.MaxBrightness)
                return false;

            bool result = SetMonitorBrightness(monitor.Handle, brightness);
            if (result)
            {
                monitor.CurrentBrightness = brightness;
            }
            return result;
        }

        public bool IncreaseBrightness(PhysicalMonitorInfo monitor, uint increment = 10)
        {
            uint newBrightness = Math.Min(monitor.CurrentBrightness + increment, monitor.MaxBrightness);
            return SetBrightness(monitor, newBrightness);
        }

        public bool DecreaseBrightness(PhysicalMonitorInfo monitor, uint decrement = 10)
        {
            uint newBrightness = Math.Max(monitor.CurrentBrightness - decrement, monitor.MinBrightness);
            return SetBrightness(monitor, newBrightness);
        }

        public void ReleaseMonitors()
        {
            foreach (var monitor in _monitors)
            {
                DestroyPhysicalMonitor(monitor.Handle);
            }
            _monitors.Clear();
        }
    }

    public class PhysicalMonitorInfo
    {
        public IntPtr Handle { get; set; }
        public string Description { get; set; }
        public uint MinBrightness { get; set; }
        public uint CurrentBrightness { get; set; }
        public uint MaxBrightness { get; set; }
        public int Index { get; set; }
        public int BrightnessPercentage => (int)((CurrentBrightness - MinBrightness) * 100 / (MaxBrightness - MinBrightness));

        public override string ToString()
        {
            return $"{Description} - Brightness: {BrightnessPercentage}%";
        }
    }
}