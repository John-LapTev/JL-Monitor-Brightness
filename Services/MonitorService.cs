using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

namespace JL_Monitor_Brightness.Services
{
    public class MonitorService : IDisposable
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

        // Встроенный экран живёт отдельно от DDC/CI, но в общем списке мониторов:
        // человеку неважно, каким каналом крутится яркость.
        private readonly LaptopBrightnessService _laptop = new LaptopBrightnessService();

        public List<PhysicalMonitorInfo> GetMonitors()
        {
            // ⚠️ Именно ReleaseMonitors, а не Clear: хендлы от GetPhysicalMonitorsFromHMONITOR
            // обязан освобождать вызывающий, иначе пул драйвера утекает при каждом обновлении.
            ReleaseMonitors();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnum, IntPtr.Zero);

            // Экран ноутбука первым: он всегда под рукой, а внешние приходят и уходят.
            if (_laptop.IsAvailable)
            {
                int яркость = _laptop.GetBrightness();
                if (яркость >= 0)
                {
                    _monitors.Insert(0, new PhysicalMonitorInfo
                    {
                        IsBuiltIn = true,
                        Handle = IntPtr.Zero,
                        Description = _laptop.Description,
                        MinBrightness = 0,
                        CurrentBrightness = (uint)яркость,
                        MaxBrightness = 100,
                        Index = 0,
                    });

                    // Индексы съехали из-за вставки в начало — пересчитываем.
                    for (int i = 0; i < _monitors.Count; i++)
                    {
                        _monitors[i].Index = i;
                    }
                }
            }
            // Копия, а не внутренний список: иначе следующий вызов очистит список
            // прямо под руками у того, кто держит ссылку.
            return new List<PhysicalMonitorInfo>(_monitors);
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
                        else
                        {
                            // Монитор без поддержки DDC/CI (обычно встроенная матрица ноутбука).
                            // Хендл всё равно выделен — если его не отдать здесь, он утечёт
                            // навсегда: в список он не попадает, и ReleaseMonitors его не увидит.
                            DestroyPhysicalMonitor(monitor.hPhysicalMonitor);
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

            // У встроенного экрана нет хендла DDC/CI — только WMI.
            if (monitor.IsBuiltIn)
            {
                bool ok = _laptop.SetBrightness((int)brightness);
                if (ok)
                {
                    monitor.CurrentBrightness = brightness;
                }
                return ok;
            }

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
            // ⚠️ Без ограничения шага 5u - 10u даёт 4294967291 (uint не уходит в минус),
            // Math.Max выбирает это значение, и яркость молча перестаёт убавляться.
            uint step = Math.Min(decrement, monitor.CurrentBrightness);
            uint newBrightness = Math.Max(monitor.CurrentBrightness - step, monitor.MinBrightness);
            return SetBrightness(monitor, newBrightness);
        }

        public void ReleaseMonitors()
        {
            foreach (var monitor in _monitors)
            {
                // У встроенного экрана хендла нет — освобождать нечего.
                if (!monitor.IsBuiltIn && monitor.Handle != IntPtr.Zero)
                {
                    DestroyPhysicalMonitor(monitor.Handle);
                }
            }
            _monitors.Clear();
        }

        public void Dispose()
        {
            ReleaseMonitors();
        }

        // ⛔ Финализатора здесь нет намеренно. Он работал бы в потоке сборщика мусора,
        // где порядок не определён, и вызывал бы оттуда DestroyPhysicalMonitor. Хендлы
        // и без того возвращаются драйверу при завершении процесса, а Dispose зовётся
        // из Application_Exit — страховка обходилась дороже, чем защищала.
    }

    public class PhysicalMonitorInfo
    {
        /// <summary>
        /// Встроенный экран ноутбука. У него нет хендла DDC/CI — яркость идёт
        /// через WMI, поэтому все операции проверяют этот признак.
        /// </summary>
        public bool IsBuiltIn { get; set; }

        public IntPtr Handle { get; set; }
        public string Description { get; set; }
        public uint MinBrightness { get; set; }
        public uint CurrentBrightness { get; set; }
        public uint MaxBrightness { get; set; }
        public int Index { get; set; }
        // ⚠️ Виртуальные дисплеи и часть KVM отвечают Min == Max. Без проверки это
        // DivideByZeroException прямо в обработчике горячей клавиши, то есть падение программы.
        public int BrightnessPercentage => MaxBrightness > MinBrightness
            ? (int)((CurrentBrightness - MinBrightness) * 100 / (MaxBrightness - MinBrightness))
            : 0;

        public override string ToString()
        {
            return $"{Description} - Brightness: {BrightnessPercentage}%";
        }
    }
}