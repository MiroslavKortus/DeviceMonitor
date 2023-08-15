using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace DeviceMonitor
{
    internal class DeviceMonitor
    {
        private const string _eom = "<|EOM|>";  // End of message
        private const string _ack = "<|ACK|>";  // Acknowledgment

        private IPEndPoint _ipEndPoint = new IPEndPoint(0,0);
        private bool _isInWorkingMode;
        private List<ReceivedData> _receivedData = new List<ReceivedData>();
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private Dictionary<int,byte> _lastDeviceMessageIndex = new Dictionary<int,byte>();

        #region Constructors

        /// <summary>
        /// DeviceMonitor constructor.
        /// </summary>
        /// <param name="remoteEP">Represents a network endpoint as an IP address and a port number.</param>
        public DeviceMonitor(IPEndPoint ipEndPoint)
        {
            this._ipEndPoint = ipEndPoint;
        }

        /// <summary>
        /// DeviceMonitor constructor
        /// </summary>
        /// <param name="address">Provides an Internet Protocol (IP) address.</param>
        /// <param name="port">Represents a 32-bit signed integer.</param>
        public DeviceMonitor(IPAddress ipAddress, int port)
        {
            try
            {
                _ipEndPoint = new(ipAddress, port);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        /// <summary>
        /// DeviceMonitor constructor
        /// </summary>
        /// <param name="ipAddress">Provides an Internet Protocol (IP) address.</param>
        /// <param name="port">Represents a 32-bit signed integer</param>
        public DeviceMonitor(IPAddress[] ipAddress, int port)
        {
            try
            {
                _ipEndPoint = new IPEndPoint(ipAddress[0], port);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        /// <summary>
        /// DeviceMonitor constructor
        /// </summary>
        /// <param name="hostName">Represents text as a sequence of UTF-16 code units.</param>
        /// <param name="port">Represents a 32-bit signed integer.</param>
        public DeviceMonitor(string hostName, int port)
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(hostName);
            IPAddress ipAddress = ipHostInfo.AddressList[1];
            _ipEndPoint = new IPEndPoint(ipAddress, port);
        }

        #endregion

        #region Communication

        /// <summary>
        /// Start receiving data.
        /// </summary>
        public void StartDataReceiving()
        {
            _logger.Info("Star");
            try
            {
                _lastDeviceMessageIndex.Clear();
                _receivedData.Clear();
                _isInWorkingMode = true;
                DataReceivingAsync();
            }
            catch (SocketException ex)
            {
                _logger.Error(ex);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        /// <summary>
        /// Stop data receiving.
        /// </summary>
        public void StopDataReceiving()
        {
            _isInWorkingMode = false;
        }

        /// <summary>
        /// Receiving data from TCP/IP device.
        /// </summary>
        private async void DataReceivingAsync()
        {
            using Socket listener = new(_ipEndPoint.AddressFamily,
                                        SocketType.Stream,
                                        ProtocolType.Tcp);

            try
            {
                listener.Bind(_ipEndPoint);
                listener.Listen(100);
            }
            catch (SocketException ex)
            {
                _logger.Error(ex.Message);
                return;
            }

            var handler = await listener.AcceptAsync();

            while (_isInWorkingMode)
            {
                byte[] buffer = new byte[1024];
                try
                {
                    var received = await handler.ReceiveAsync(buffer, SocketFlags.None);
                    if (Encoding.UTF8.GetString(buffer, 0, buffer.Length).IndexOf(_eom) == 9)
                    {
                        // Create response: indexMessage + deviceId + _ack
                        byte[] byteDeviceID = GetDeviceIdInBytesFromReceivedData(buffer);
                        byte[] byteAck = Encoding.UTF8.GetBytes(_ack);
                        byte[] ack = new byte[byteDeviceID.Length + byteAck.Length + 1];
                        ack[0] = buffer[0];
                        byteDeviceID.CopyTo(ack, 1);
                        byteAck.CopyTo(ack, byteDeviceID.Length + 1);

                        await handler.SendAsync(ack, 0);
                    }
                }
                catch (SocketException ex)
                {
                    _logger.Error(ex.Message);
                }
                if (!IsThisARepeatedMessage(buffer))
                {
                    ProcessInputStream(buffer);
                }
            }

            listener.Close();
        }

        #endregion

        #region Processing received data

        /// <summary>
        /// Select bytes from received message representing device ID in bytes.
        /// </summary>
        /// <param name="data">Received data on the LAN port</param>
        /// <returns>Device ID</returns>
        private byte[] GetDeviceIdInBytesFromReceivedData(byte[] data)
        {
            return data.Skip(1).Take(4).ToArray();
        }

        /// <summary>
        /// Get the device Id.
        /// </summary>
        /// <param name="data">Received data on the LAN port</param>
        /// <returns>Device ID</returns>
        private int GetDeviceIdFromReceivedData(byte[] data)
        {
            return BitConverter.ToInt32(GetDeviceIdInBytesFromReceivedData(data), 0);
        }

        /// <summary>
        /// Get the measured value.
        /// </summary>
        /// <param name="data">Received data on the LAN port</param>
        /// <returns>Measured value</returns>
        private int GetMeasuredValueFromReceivedData(byte[] data)
        {
            return BitConverter.ToInt32(data.Skip(5).Take(4).ToArray(), 0);
        }

        /// <summary>
        /// Parse inputStream to deviceId and measuredValue and add them to the receivedDataList.
        /// </summary>
        /// <param name="data">Received data on the LAN port (inputStream).</param>
        private void ProcessInputStream(byte[] data)
        {
            try
            {
                _receivedData.Add(new ReceivedData(GetDeviceIdFromReceivedData(data),
                                                   GetMeasuredValueFromReceivedData(data)));
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }
        }

        /// <summary>
        /// Check if the message was repeated.
        /// </summary>
        /// <param name="data">Received data</param>
        /// <returns>True when the device sent the message repeatedly.</returns>
        private bool IsThisARepeatedMessage(byte[] data)
        {
            int deviceId = GetDeviceIdFromReceivedData(data);
            byte deviceMessageIndex;
            if (_lastDeviceMessageIndex.TryGetValue(deviceId, out deviceMessageIndex))
            {
                return deviceMessageIndex == data[0];
            }
            else
            {
                _lastDeviceMessageIndex.Add(deviceId, data[0]);
                return false;
            }
        }

        #endregion

        #region Output

        public XDocument CountOfReceivedMessagesGroupedByDevicess()
        {
            var countOfReceivedMessages = from rd in _receivedData
                                          group rd by rd.deviceId into groupCount
                                          select new { deviceId = groupCount.Key, count = groupCount.Count() };

            XDocument doc = new XDocument();
            doc.Add(new XElement("devices"));
            try
            {
                foreach (var d in countOfReceivedMessages)
                {
                    XElement root = new XElement("device");
                    root.Add(new XElement("device_id", d.deviceId.ToString()));
                    root.Add(new XElement("count", d.count.ToString()));
                    doc.Element("devices").Add(root);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }

            return doc;
        }

        /// <summary>
        /// Count of received data from all devices.
        /// </summary>
        /// <returns>Received data count</returns>
        public int ReceivedDataCount()
        {
            return _receivedData.Count();
        }

        #endregion
    }
}
