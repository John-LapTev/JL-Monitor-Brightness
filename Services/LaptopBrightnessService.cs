using System;
using System.Management;

namespace JL_Monitor_Brightness.Services
{
    /// <summary>
    /// Яркость встроенного экрана ноутбука.
    ///
    /// Матрица ноутбука не отвечает по DDC/CI — это канал для внешних мониторов.
    /// Встроенный экран управляется через WMI, пространство root\WMI:
    /// WmiMonitorBrightness читает, WmiMonitorBrightnessMethods пишет.
    ///
    /// На стационарных компьютерах этих классов нет вовсе — тогда служба просто
    /// сообщает, что экрана нет, и всё работает как раньше.
    /// </summary>
    public class LaptopBrightnessService
    {
        private const string Scope = "root\\WMI";

        /// <summary>Есть ли встроенный экран, которым можно управлять.</summary>
        public bool IsAvailable { get; private set; }

        /// <summary>Имя экрана для показа человеку.</summary>
        public string Description { get; private set; } = "Экран ноутбука";

        private string _instanceName;

        public LaptopBrightnessService()
        {
            Detect();
        }

        private void Detect()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(Scope,
                    "SELECT * FROM WmiMonitorBrightness");
                foreach (ManagementObject item in searcher.Get())
                {
                    _instanceName = item["InstanceName"]?.ToString();
                    IsAvailable = !string.IsNullOrEmpty(_instanceName);
                    item.Dispose();
                    if (IsAvailable)
                    {
                        return;
                    }
                }
            }
            catch (ManagementException)
            {
                // Классов нет — обычный стационарный компьютер.
                IsAvailable = false;
            }
            catch (Exception)
            {
                // Служба WMI может быть выключена политикой — не повод падать.
                IsAvailable = false;
            }
        }

        /// <summary>Текущая яркость, 0-100. Возвращает -1, если прочитать не вышло.</summary>
        public int GetBrightness()
        {
            if (!IsAvailable)
            {
                return -1;
            }

            try
            {
                using var searcher = new ManagementObjectSearcher(Scope,
                    "SELECT CurrentBrightness FROM WmiMonitorBrightness");
                foreach (ManagementObject item in searcher.Get())
                {
                    object value = item["CurrentBrightness"];
                    item.Dispose();
                    if (value != null)
                    {
                        return Convert.ToInt32(value);
                    }
                }
            }
            catch (Exception)
            {
                // Экран мог отвалиться при переключении на внешний.
            }

            return -1;
        }

        /// <summary>Ставит яркость 0-100. Возвращает false, если не приняли.</summary>
        public bool SetBrightness(int percent)
        {
            if (!IsAvailable)
            {
                return false;
            }

            percent = Math.Clamp(percent, 0, 100);

            try
            {
                using var searcher = new ManagementObjectSearcher(Scope,
                    "SELECT * FROM WmiMonitorBrightnessMethods");
                foreach (ManagementObject item in searcher.Get())
                {
                    // Первый аргумент — таймаут в секундах: 1 значит «применить сразу».
                    item.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)percent });
                    item.Dispose();
                    return true;
                }
            }
            catch (Exception)
            {
                // Часть ноутбуков отдаёт класс, но отказывает в записи — например,
                // когда включена аппаратная адаптивная подсветка.
            }

            return false;
        }
    }
}
