// See https://aka.ms/new-console-template for more information


using System.Net;
using System.Xml.Linq;

DeviceMonitor.DeviceMonitor deviceMonitor = new DeviceMonitor.DeviceMonitor(IPAddress.Parse("127.0.0.1"), 6666);
deviceMonitor.StartDataReceiving();

while (deviceMonitor.ReceivedDataCount() <= 10)
{
        Thread.Sleep(1000);
}

deviceMonitor.StopDataReceiving();
XDocument doc = deviceMonitor.CountOfReceivedMessagesGroupedByDevicess();
Console.WriteLine(doc.ToString());

Console.WriteLine("hotovo");
Console.ReadKey();