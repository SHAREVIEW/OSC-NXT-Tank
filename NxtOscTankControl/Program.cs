using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
// Referenced for the other functionalities
using System.Net;
using ToxNXT;
using Ventuz.OSC;

namespace NxtOscTankControl {
     class Program {
          static void Main(string[] args) {
               // 1. Instantiate BT connection to NXT
               //    You can peek in the class file for more documentation
               NXTBluetooth bt = new NXTBluetooth(@"Tox NXT");
               // 2. Instantiate the OSC receiver
               NetReader stream = new UdpReader(8000);
               // this is the main handler when an OSC message is recieved, per 100ms to not clog the BT
               Timer events = new Timer(new TimerCallback(
                    delegate(object state) {
                         object o;
                         o = stream.Receive(); // 3. receive the info
                         if (o != null) {
                              // 4. convert the information
                              OscElement element = (OscElement)o;
                              //Console.WriteLine(string.Format("Message Received [{0}]: {1}",
                              //     element.Address,
                              //     element.Args[0]));
                              object arg;
                              // get the value passed
                              if (element.Args != null &&
                                   element.Args.Length > 0 &&
                                   (arg = element.Args[0]) != null) {
                                   // 5. check on which value it is then send the message to BT
                                   int value = 0;
                                   switch (element.Address) {
                                        case "/1/leftTrack":
                                             value = 10000 + (int)((float)element.Args[0] * 100);
                                             bt.SendString(value.ToString());
                                             break;
                                        case "/1/rightTrack":
                                             value = 20000 + (int)((float)element.Args[0] * 100);
                                             bt.SendString(value.ToString());
                                             break;
                                        case "/1/turret":
                                             value = 30000 + (int)((float)element.Args[0] * 100);
                                             bt.SendString(value.ToString());
                                             break;
                                        case "/1/stop":
                                             value = 40000;
                                             bt.SendString(value.ToString());
                                             break;
                                        default:
                                             // ignore
                                             break;
                                   }
                                   Console.WriteLine(value.ToString());
                              }
                         }
                    }), null, 0, 1);
               // timer starts immediately, ticks every 1ms
               Console.ReadKey(); // press any key to quit
          }
     }
}
