using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading;

namespace Ambilight.Helpers
{
    public class SerialPortHelper
    {
        private SerialPort comport = new SerialPort();
        public bool IsOpen { get { return comport.IsOpen; } }


        public bool OpenPort(int baudRate, int dataBits, StopBits stopBits, Parity parity, string comPortName)
        {


            if (comport.IsOpen) comport.Close();
            else
            {
                comport.BaudRate = baudRate;
                comport.DataBits = dataBits;
                comport.StopBits = stopBits;
                comport.Parity = parity;
                comport.PortName = comPortName;

                try
                {
                    comport.Open();
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
                catch (ArgumentException) { }

            }

            try
            {
                SendColorToComPort(Color.Black);
                return true;
            }
            catch
            {
                MessageBox.Show("Couldn't send data thru serial port\nPerhaps it is not an Arduino Board?", "System.IOException", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                comport.Close();
                return false;
            }
        }
        public void ClosePort()
        {
            if (comport.IsOpen)
            {
                SendColorToComPort(Color.Black);
                Thread.Sleep(100);
                comport.Close();
            }
        }
        public void SendColorToComPort(Color color)
        {
            if (comport.IsOpen) {
                comport.Write(color.R + "-" + color.G + "-" + color.B);
                Debug.WriteLine(color.R + "-" + color.G + "-" + color.B);
            }
        }
    }


}
