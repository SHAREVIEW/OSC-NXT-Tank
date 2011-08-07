using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// added for the BT commands
// i used this so i wont guess the COM ports like the other libs do (mindsquall, nxtnet)
using InTheHand.Net.Sockets;
using InTheHand.Net.Bluetooth;
using InTheHand.Net;
using System.Net;
using System.IO;
using System.Net.Sockets;

namespace ToxNXT {
     /// <summary>
     /// Class for connecting to NXT from .Net
     /// </summary>
     public class NXTBluetooth {

          // this is to indicate that we are using the BT as a serial port to send commands to NXT
          private Guid _SERVICENAME = BluetoothService.SerialPort;

          private BluetoothClient _btc; // main bluetooth connection
          private BluetoothDeviceInfo[] _bdi; // enumeration of all bluetooth devices, you can find you NXTs here
          private BluetoothDeviceInfo _nxt; // the NXT info that we are targeting, you will see later
          private BluetoothClient _client; // the established connection to the NXT

          private int _maxTries; // default, tries 3 times

          /// <summary>
          /// Constructor, tries to connect 3x to NXT then fails
          /// </summary>
          /// <param name="nxtName">NXT brick name, case sensitive</param>
          /// <param name="connectionTries">Max failed try to connect</param>
          public NXTBluetooth(string nxtName, int connectionTries = 3) {
               _maxTries = connectionTries;
               try {
                    _btc = new BluetoothClient(); // instantiate bluetooth connection
                    debugMessage(@"Bluetooth is discovering devices");
                    _bdi = _btc.DiscoverDevices(); // get all bluetooth devices
#if DEBUG
                    debugMessage(@"Bluetooth devices:");
                    foreach (BluetoothDeviceInfo info in _bdi) {
                         Console.WriteLine(info.DeviceName);
                    }
#endif
                    // linq to match our NXT name
                    var nxt = from n in this._bdi where n.DeviceName == nxtName select n;
                    if (nxt != null && nxt.Count<BluetoothDeviceInfo>() > 0) {
                         // get the NXT device info
                         _nxt = (BluetoothDeviceInfo)nxt.First<BluetoothDeviceInfo>();
                    } else {
                         // no NXT of that name exists
                         throw new Exception("Cannot find NXT name from Bluetooth device list");
                    }
                    debugMessage(@"Bluetooth Initialized, NXT found");
               } catch (Exception) {
                    throw;
               }
               _client = null;
               int tries = 1;
               do {
                    debugMessage(string.Format(@"Connecting to NXT... on {0} try...", tries));
                    try {
                         // now if we have found our NXT
                         _client = new BluetoothClient();
                         // we will connect to the NXT using the device info we got earlier
                         _client.Connect(_nxt.DeviceAddress, _SERVICENAME);
                    } catch (SocketException se) {
                         if ((tries >= this._maxTries)) {
                              // gave up trying to connect
                              throw se;
                         }
                         debugMessage(string.Format(@"Error establishing contact, retrying {0} time", tries));
                         _client = null;
                    }
                    // if we cant connect, we can try and try, default is 3 times
                    tries = tries + 1;
               } while (_client == null & tries <= this._maxTries);
               // after these, if client is not null then we are connected! we can use _client now
               if ((_client == null)) {
                    //timeout occurred
                    debugMessage(@"Error establishing contact, fail");
                    throw new Exception(@"Cannot connect to NXT... Terminating...");
               }
               debugMessage(@"Connected to NXT...");
          }

          /// <summary>
          /// Destructor, must dispose bluetooth connection
          /// </summary>
          ~NXTBluetooth() {
               if (((_client != null))) {
                    try {
                         // _client.GetStream().Close(); // sometimes this is disposed alreadr
                         _client.Close();
                    } finally { // (bad practice)
                         // just let it close or something
                    }
               }
               if (_btc != null) {
                    _btc.Close();
               }
          }

          /// <summary>
          /// NXT's Bluetooth inboxes, you can add more
          /// </summary>
          public enum NxtInbox : int {
               Inbox1 = 0
          }

          /// <summary>
          /// Sends a string to the NXT, useful for your custom NXT-g program
          /// </summary>
          /// <param name="message">Message to send, kindly limit to 59 characters, you may risk it if you wont :)</param>
          /// <param name="inbox">Inbox to drop the message to NXT, default is inbox 1 on NXT</param>
          public void SendString(string message, NxtInbox inbox = NxtInbox.Inbox1) {
               // byte command derived from my nxtblue phone controller
               // check "Appendix 1-LEGO MINDSTORMS NXT Communication protocol" for reference (MESSAGEWRITE)
               int totalLen = 6 + message.Length + 1; // +6 for the header; +1 for the terminator char later
               byte[] cmd = new byte[totalLen]; 
               int lsb, msb;
               // convert the length to lsb and msb
               lsb = (totalLen-2) & 0x0F; // just get from command type, byte 2
               msb = 0;
               cmd[0] = (byte)lsb; // byte 0 and 1 are for the message size
               cmd[1] = (byte)msb; 
               cmd[2] = 0x80; // command type (this case it is direct command no response
               cmd[3] = 0x09; // command means "messagewrite"
               switch (inbox) {
                    case NxtInbox.Inbox1:
                         // inbox on NXT, in our case this is inbox 0
                         cmd[4] = (byte)0x00;
                         break;
                    default:
                         // default to inbox 0
                         cmd[4] = (byte)0x00;
                         break;
               }
               message += '\0'; // add the terminating \0 to the message
               cmd[5] = (byte)(message.Length); // length of message, plus terminator \0
               Encoding.ASCII.GetBytes(message).CopyTo(cmd, 6); // add the message
               sendMessage(cmd); // send the message finally
          }

          #region [ Utility ]

          private void debugMessage(string message) {
#if DEBUG
               Console.WriteLine(message);
#endif
          }

          /// <summary>
          /// To test connection, you can try to play a tone
          /// </summary>
          /// <param name="tone"></param>
          /// <param name="duration"></param>
          public void PlayTone(int tone, int duration) {
               byte[] buffer;
               int lsb, msb;
               int lsbd, msbd;
               // tone
               tone += 1000; // higher note
               // we are converting the integer to binary values
               if (tone > 255) {
                    lsb = tone & 0x0F; msb = tone >> 8;
               } else if (tone < 255 && tone >= 200) {
                    lsb = tone & 0x0F; msb = 0;
               } else {
                    lsb = 200; msb = 0;
               }
               // duration
               lsbd = duration & 0x0F; msbd = duration >> 8;
               // construct the message, based from NXT bluetooth developer documentation
               buffer = new byte[] { 0x06, 0x00, 0x80, 0x03, 
                    Convert.ToByte(lsb), 
                    Convert.ToByte(msb), 
                    Convert.ToByte(lsbd), 
                    Convert.ToByte(msbd) 
               };
               debugMessage(string.Format(@"Trying to play tone... {0} {1} {2}", tone, lsb, msb));
               sendMessage(buffer); // send the message
          }

          /// <summary>
          /// All messages must be sent using this method
          /// </summary>
          /// <param name="buffer">Byte stream to send</param>
          private void sendMessage(byte[] buffer) {
               System.IO.Stream stream = null;
               try {
                    stream = _client.GetStream(); // get the stream to the NXT
                    stream.Write(buffer, 0, buffer.Length); // send the binary data
                    debugMessage(@"Message sent!");
                    //stream.Close();
               } catch {
                    // just throw if not sent
                    throw;
               }
          }

          protected void SendMessage(byte[] buffer) {
               this.sendMessage(buffer);
          }

          #endregion

     }
}
