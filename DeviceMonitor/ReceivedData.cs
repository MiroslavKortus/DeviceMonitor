using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceMonitor
{
    internal class ReceivedData
    {
        public int deviceId { get; private set; }
        public int measuredValue { get; private set; }

        public ReceivedData(int deviceId, int measuredValue)
        {
            this.deviceId = deviceId;
            this.measuredValue = measuredValue;
        }
    }
}
