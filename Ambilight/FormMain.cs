using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using Ambilight.Helpers;
using Microsoft.Win32;
using MultiMonitorHelper;
using SlimDX;
using SlimDX.Direct3D9;

namespace Ambilight {
    public partial class FormAmbilight : Form {
        #region Properties

        //-// Serial Port Settings
        int baudRate = 250000;
        int dataBits = 8;
        StopBits stopBits = StopBits.One;
        Parity parity = Parity.None;
        //-//
        int brightness = 255; // Brightness of the output (0-255)
        public int Brightness {
            get { return brightness; }
            set { brightness = value; trackBar2.Value = brightness; }
        }
        int rainbowSpeed = 70; // Speed of the rainbow effect (0-70)
        SerialPortHelper serialPort = new SerialPortHelper(); // Class for serialport methods
        DxScreenCapture sc;
        KeyboardHook onoff = new KeyboardHook(); // Hotkey for turning on and off led
        KeyboardHook flash = new KeyboardHook(); // Hotkey for the flash strobe
        KeyboardHook blind = new KeyboardHook(); // Hotkey for the blinder
        KeyboardHook exit = new KeyboardHook(); // Hotkey for the blinder
        KeyboardHook monitor1 = new KeyboardHook(); // Hotkey for the blinder
        KeyboardHook monitor2 = new KeyboardHook(); // Hotkey for the blinder
        KeyboardHook monitor3 = new KeyboardHook(); // Hotkey for the blinder
        KeyboardHook monitor4 = new KeyboardHook(); // Hotkey for the blinder
        KeyboardHook monitor5 = new KeyboardHook(); // Hotkey for the blinder
        CaptureMode capMode;
        int screen = 0; // ID of the screen
        int delay = 0; // Capture delay
        decimal frametime = 0; // Value of the time taken to each frame
        int onoffHotkeyId = 0; // ID of the onoff hotkey
        int flashHotkeyId = 0; // ID of the flash strobe hotkey
        int blindHotkeyId = 0; // ID of the blinder hotkey
        int exitHotkeyId = 0; // ID of the blinder hotkey
        int mon1HotkeyId = 0; // ID of the blinder hotkey
        int mon2HotkeyId = 0; // ID of the blinder hotkey
        int mon3HotkeyId = 0; // ID of the blinder hotkey
        int mon4HotkeyId = 0; // ID of the blinder hotkey
        int mon5HotkeyId = 0; // ID of the blinder hotkey
        int fpsCount = 0;
        int partyEnergy = 0;
        int partyEnergyInverted = 0;
        double gammaV = 2.8;
        public static byte[] gamma = new byte[256];
        Thread t = null; // Thread for the screen color analysis
        Thread tChecker = null; // Thread of the fullscreen checker
        Thread rainbowT = null;
        Thread flashT = null;
        Thread blindT = null;
        bool continueThread = true; // Bool to check if the analysis is on
        bool continueChecking = false; // Bool to check if the checking is on
        bool running = true; // Dont remember
        bool blackout = false;
        Random random = new Random(); // Random number generator
        private LightSleeper m_lightSleeper = new LightSleeper(); // Class for canceling threads even if it is sleeping
        private GlobalKeyboardHook _globalKeyboardHook; // Class for throwing event on global keypress
        RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true); // Registry key for starting with windows
        bool fastMethod = false; // Bool for when the primary monitor is selected. Uses a faster function that only runs in the primary monitor
        bool pausedFlash = false; // If the strobe is on. Pauses the main screen color analysis
        bool pausedBlind = false; // If the blinder is on. Pauses the main screen color analysis
        int flashPressed = 0; // Check what key for the strobe is presses, for different speeds.
        bool blinderPressed = false; // If the blinder key is pressed
        int r = 0, g = 0, b = 0; // RGB values of the current screen
        bool blindBool = false;
        bool flashBool = false;
        bool defaultComFound = false;
        bool loaded = false;
        #endregion

        int WM_SYSCOMMAND = 0x112;
        int SC_MONITORPOWER = 0xF170;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

        #region Constructor

        public FormAmbilight() {
            InitializeComponent();

        }

        #endregion

        #region Private Methods
        // Function for getting the screens and com ports
        private void GetComPorts() {
            cmbPortName.BindingContext = this.BindingContext;
            cmbPortName.Items.Clear();
            cmbPortName.Items.AddRange(OrderedPortNames());

            comboBox1.Items.Clear();

            // Getting the screens
            int i = 1;
            foreach (Screen scr in Screen.AllScreens) {
                comboBox1.Items.Add("Screen " + i);
                i++;
            }

            comboBox1.SelectedIndex = comboBox1.FindStringExact(Properties.Settings.Default.screen);

            if (comboBox1.SelectedIndex < 0) {
                for (int x = 0; x < comboBox1.Items.Count; x++)
                    if (Screen.AllScreens[x].Primary)
                        comboBox1.SelectedIndex = x; // Selecting the primary monitor
            }

            if (cmbPortName.Items.Count > 0) {

                if (Properties.Settings.Default.comPort == "0" || Properties.Settings.Default.comPort == null) {
                    cmbPortName.SelectedIndex = 0;
                } else {
                    if (cmbPortName.FindStringExact(Properties.Settings.Default.comPort) >= 0) {
                        cmbPortName.SelectedIndex = cmbPortName.FindStringExact(Properties.Settings.Default.comPort);
                        defaultComFound = true;
                    } else {
                        cmbPortName.SelectedIndex = 0;
                        defaultComFound = false;
                    }
                }
            }
        }

        // Function for getting the port names ordered
        private string[] OrderedPortNames() {
            int num;
            return SerialPort.GetPortNames().OrderBy(a => a.Length > 3 && int.TryParse(a.Substring(3), out num) ? num : 0).ToArray();
        }

        // Function for starting the color screen analysis
        private void Start() {

            // Open com port
            if (!serialPort.IsOpen)
                if (!serialPort.OpenPort(baudRate, dataBits, stopBits, parity, cmbPortName.Text))
                    return;
            if (capMode == CaptureMode.GDI) {
                // Checking if there's only one screen or the primary screen is selected
                if (comboBox1.Items.Count == 1)
                    fastMethod = true; // Using fastmode

                if (comboBox1.Items.Count == 1)
                    fastMethod = true;

                if (Screen.AllScreens[comboBox1.SelectedIndex].Primary)
                    fastMethod = true;
                else
                    fastMethod = false;

                if (fastMethod)
                    toolStripStatusLabel1.Text = "Primary Monitor Selected. Running in fast mode";
            }
            Color color;
            delay = (int) numericUpDown1.Value;
            screen = comboBox1.SelectedIndex;

            if (capMode == CaptureMode.MirrorDriver) {
                mirror.Load();
                mirror.Connect();
            }

            // Checking if the enable with fullscreen checkbox is checked
            if (checkBox1.Checked) {
                continueThread = false;
                continueChecking = true;
            } else {
                continueThread = true;
                continueChecking = false;
            }
            running = true;

            // Thread for running the analysis
            if (capMode == CaptureMode.DirectX) {
                if (DxScreenCapture.disposed)
                    sc = new DxScreenCapture(comboBox1.SelectedIndex);
                else {
                    sc.Dispose();
                    sc = new DxScreenCapture(comboBox1.SelectedIndex);
                }
                Surface s;
                DataRectangle dr;
                DataStream gs;
                t = new Thread(delegate () {

                    while (running) {
                        if (!pausedFlash) { // Checking if the strobe is off
                            if (continueThread) {
                                m_lightSleeper.Sleep(delay);
                                s = sc.CaptureScreen();
                                dr = s.LockRectangle(LockFlags.None);
                                gs = dr.Data;
                                color = ScreenAnalysisHelper.avcs(gs); // Getting the screen color
                                                                       // Storing current color
                                r = color.R;
                                g = color.G;
                                b = color.B;
                                fpsCount++;
                                if (!pausedBlind)
                                    SendColorToComPort(color); // Sending to arduino
                                                               //Debug.WriteLine(color.R + "-" + color.G + "-" + color.B);
                                s.UnlockRectangle();
                                s.Dispose();
                            }
                        } else
                            Thread.Sleep(20);
                    }
                });
            } else if (capMode == CaptureMode.MirrorDriver) {
                t = new Thread(delegate () {

                    while (running) {
                        if (!pausedFlash) { // Checking if the strobe is off
                            if (continueThread) {
                                m_lightSleeper.Sleep(delay);
                                color = getAverageColorMirror(); // Getting the screen color
                                                                 // Storing current color
                                r = color.R;
                                g = color.G;
                                b = color.B;
                                if (!pausedBlind)
                                    SendColorToComPort(color); // Sending to arduino
                            }
                        } else
                            Thread.Sleep(20);
                    }
                });
            } else if (capMode == CaptureMode.GDI) {
                t = new Thread(delegate () {

                    while (running) {
                        if (!pausedFlash) { // Checking if the strobe is off
                            if (continueThread) {
                                m_lightSleeper.Sleep(delay);
                                color = getAverageColorGDI(); // Getting the screen color
                                                              // Storing current color
                                r = color.R;
                                g = color.G;
                                b = color.B;
                                if (!pausedBlind)
                                    SendColorToComPort(color); // Sending to arduino
                            }
                        } else
                            Thread.Sleep(20);
                    }
                });
            }

            tChecker = new Thread(delegate () {

                while (running) {
                    if (!pausedBlind && !pausedFlash)
                        if (continueChecking) {
                            if (!continueThread)
                                m_lightSleeper.Sleep(600);
                            if (FullScreenHelper.IsForegroundWwindowFullScreen()) { // Checking if there's any fullscreen app running
                                if (continueChecking) {
                                    continueThread = true;
                                    continueChecking = false;
                                }
                            } else {
                                if (continueChecking)
                                    continueThread = false;
                            }
                        }
                }


            });
            t.Start();
            if (checkBox1.Checked)
                tChecker.Start();
            btStart.Enabled = false;
            btStop.Enabled = true;
            checkBox1.Enabled = false;
            checkBox2.Enabled = false;
            checkBox3.Enabled = false;
            checkBox3.Checked = false;
            checkBox2.Checked = false;
            comboBox1.Enabled = true;
            cmbPortName.Enabled = false;
            button1.Enabled = false;
            button2.Enabled = false;
        }

        // Funcion for stopping all analysis proccess
        private void Stop() {
            checkBox2.Checked = false;
            btStart.Enabled = true;
            btStop.Enabled = false;
            checkBox1.Enabled = true;
            checkBox2.Checked = false;
            checkBox2.Enabled = true;
            comboBox1.Enabled = true;
            cmbPortName.Enabled = false;
            button2.Enabled = true;
            button1.Enabled = true;
            toolStripStatusLabel1.Text = "";
            m_lightSleeper.Cancel();

            running = false;
            Thread.Sleep(100);
            if (t != null)
                t.Interrupt();
            if (tChecker != null)
                tChecker.Interrupt();

            Thread.Sleep(20);
            TurnLightsOff();
            mirror.Disconnect();
            mirror.Stop();
            mirror.Dispose();
        }

        // Sending black color to arduino
        private void TurnLightsOff() {
            SendColorToComPort(Color.Black);
            picColor.BackColor = Color.White;
        }

        private void SendColorToComPort(Color col) {
            col = ColorFader.Darken(col, scale(0, 255, 0, 1, brightness)); // Decrease brightness by the value of the trackbar
            if (blackout)
                col = Color.Black;
            serialPort.SendColorToComPort(col);
            picColor.BackColor = col;
        }

        private void ClosePort() {
            picColor.BackColor = Color.Black;
            if (serialPort.IsOpen)
                serialPort.ClosePort();
        }

        // Function for the rainbow ,
        private void Rainbow() {
            Color oldcol = Color.Black;
            Color newcol;
            rainbowT = new Thread(delegate () {
                while (trackBar1.Enabled) {
                    newcol = Color.FromArgb(random.Next(255), random.Next(255), random.Next(255)); // Creating random color
                    ColorFader fade = new ColorFader(oldcol, newcol, (uint) rainbowSpeed + 5); // Creating a fade effect

                    foreach (Color color in fade.Fade()) {
                        if (!pausedBlind && !pausedFlash) {
                            SendColorToComPort(color);
                            oldcol = color;
                            Thread.Sleep(rainbowSpeed + 2);
                        }
                    }
                }
            });
            rainbowT.Start();
        }

        private void Party() {
            Color oldcol = Color.Black;
            ColorFader fade;
            Color newcol;
            int checker = 0;
            int flash = 0;
            rainbowT = new Thread(delegate () {
                while (trackBar4.Enabled) {
                    newcol = Color.FromArgb(random.Next(255), random.Next(255), random.Next(255)); // Creating random color


                    checker = random.Next(partyEnergyInverted);
                    if (checker % 2 == 0) {
                        fade = new ColorFader(oldcol, newcol, (uint) random.Next(1, Math.Abs(partyEnergy) + 1));
                        foreach (Color color in fade.Fade()) {
                            if (!pausedBlind && !pausedFlash) {
                                SendColorToComPort(color);
                                oldcol = color;
                                Thread.Sleep(partyEnergy + 8);
                            }
                        }
                    } else {
                        fade = new ColorFader(oldcol, newcol, 1); // Creating a fade effect
                        foreach (Color color in fade.Fade()) {
                            if (!pausedBlind && !pausedFlash) {
                                SendColorToComPort(color);
                                oldcol = color;
                                Thread.Sleep(partyEnergy + 8);
                            }
                        }
                    }

                    if (checker % 5 == 0) {
                        flashPressed = random.Next(1, 4);
                        Thread.Sleep(checker / 10);
                        flashPressed = random.Next(1, 4);
                        Thread.Sleep(random.Next(partyEnergyInverted) / 10);
                        flashPressed = random.Next(1, 4);
                        Thread.Sleep(random.Next(partyEnergyInverted) / 10);
                        flashPressed = 0;
                        

                    }
                }
            });
            rainbowT.Start();
        }

        // Arduino Map
        public float scale(float OldMin, float OldMax, float NewMin, float NewMax, float OldValue) {

            float OldRange = (OldMax - OldMin);
            float NewRange = (NewMax - NewMin);
            float NewValue = (((OldValue - OldMin) * NewRange) / OldRange) + NewMin;

            return (NewValue);
        }

        #endregion

        #region Private Events

        private void FormAmbilight_Load(object sender, EventArgs e) {
            GetComPorts();

            string onOffkey = Ambilight.Properties.Settings.Default.onoffHot.Split('+')[1];
            string onOffmodifier = Ambilight.Properties.Settings.Default.onoffHot.Split('+')[0];
            Keys onff;
            Helpers.ModifierKeys modifier;
            Enum.TryParse(onOffkey, out onff);
            Enum.TryParse(onOffmodifier, out modifier);
            onoff.KeyPressed +=
            new EventHandler<KeyPressedEventArgs>(hook_KeyPressed);
            onoffHotkeyId = onoff.RegisterHotKey(onff, modifier);

            string blindKey = Ambilight.Properties.Settings.Default.blinderHot.Split('+')[1];
            string blindModifier = Ambilight.Properties.Settings.Default.blinderHot.Split('+')[0];
            Keys blinde;
            Helpers.ModifierKeys modifierBlind;
            Enum.TryParse(blindKey, out blinde);
            Enum.TryParse(blindModifier, out modifierBlind);
            blind.KeyPressed +=
            new EventHandler<KeyPressedEventArgs>(blind_KeyPressed);
            blindHotkeyId = blind.RegisterHotKey(blinde, modifierBlind);

            string flashKey = Ambilight.Properties.Settings.Default.flashHot.Split('+')[1];
            string flashModifier = Ambilight.Properties.Settings.Default.flashHot.Split('+')[0];
            Keys flashe;
            Helpers.ModifierKeys modifierFlash;
            Enum.TryParse(flashKey, out flashe);
            Enum.TryParse(flashModifier, out modifierFlash);
            flash.KeyPressed +=
            new EventHandler<KeyPressedEventArgs>(flash_KeyPressed);
            flashHotkeyId = flash.RegisterHotKey(flashe, modifierFlash);

            string exitKey = Ambilight.Properties.Settings.Default.exitHot.Split('+')[1];
            string exitModifier = Ambilight.Properties.Settings.Default.exitHot.Split('+')[0];
            Keys exite;
            Helpers.ModifierKeys modifierExit;
            Enum.TryParse(exitKey, out exite);
            Enum.TryParse(exitModifier, out modifierExit);
            exit.KeyPressed +=
            new EventHandler<KeyPressedEventArgs>(exit_KeyPressed);
            exitHotkeyId = exit.RegisterHotKey(exite, modifierExit);

            monitor1.KeyPressed +=
            new EventHandler<KeyPressedEventArgs>(mon1_KeyPressed);
            mon1HotkeyId = monitor1.RegisterHotKey(Keys.D1, Helpers.ModifierKeys.Alt);

            monitor2.KeyPressed +=
            new EventHandler<KeyPressedEventArgs>(mon2_KeyPressed);
            mon2HotkeyId = monitor2.RegisterHotKey(Keys.D2, Helpers.ModifierKeys.Alt);

            monitor3.KeyPressed +=
            new EventHandler<KeyPressedEventArgs>(mon3_KeyPressed);
            mon3HotkeyId = monitor3.RegisterHotKey(Keys.D3, Helpers.ModifierKeys.Alt);

            monitor4.KeyPressed +=
            new EventHandler<KeyPressedEventArgs>(mon4_KeyPressed);
            mon4HotkeyId = monitor4.RegisterHotKey(Keys.D4, Helpers.ModifierKeys.Alt);

            monitor5.KeyPressed +=
            new EventHandler<KeyPressedEventArgs>(mon5_KeyPressed);
            mon5HotkeyId = monitor5.RegisterHotKey(Keys.D5, Helpers.ModifierKeys.Alt);

            _globalKeyboardHook = new GlobalKeyboardHook();
            _globalKeyboardHook.KeyboardPressed += OnKeyPressed;

            textBox1.Text = Properties.Settings.Default.onoffHot;
            textBox2.Text = Properties.Settings.Default.flashHot;
            textBox7.Text = Properties.Settings.Default.blinderHot;
            textBox4.Text = Properties.Settings.Default.strobespd1KeyName;
            textBox3.Text = Properties.Settings.Default.strobespd2KeyName;
            textBox6.Text = Properties.Settings.Default.strobespd3KeyName;
            textBox5.Text = Properties.Settings.Default.blinderKeyName;
            if (Properties.Settings.Default.captureMode == CaptureMode.GDI)
                radioButton1.Checked = true;
            else if (Properties.Settings.Default.captureMode == CaptureMode.DirectX)
                radioButton2.Checked = true;
            else if (Properties.Settings.Default.captureMode == CaptureMode.MirrorDriver)
                radioButton3.Checked = true;
            checkBox1.Checked = Properties.Settings.Default.enableWithFull;

            capMode = Properties.Settings.Default.captureMode;

            trackBar3.Value = Properties.Settings.Default.gamma;

            double gammaV = trackBar3.Value * gammafactor;
            for (int i = 0; i <= 255; i++)
                gamma[i] = (byte) (Math.Pow((float) i / 255.0, gammaV) * 255 + 0.5);

            lblGamma.Text = gammaV.ToString();

            if (gammaV == 1)
                lblGamma.ForeColor = Color.Green;
            else
                lblGamma.ForeColor = Color.Black;

            this.notifyIcon1.Icon = new Icon("led-diode-ativado.ico");
            this.notifyIcon1.Visible = true;

            if (rkApp.GetValue("AmbientLight") == null)
                chbxStartWithWindows.Checked = false;
            else {
                chbxStartWithWindows.Checked = true;
                if (defaultComFound) {
                    button3.PerformClick();
                    //btStart.PerformClick();
                }
            }
            loaded = true;
        }

        private void mon5_KeyPressed(object sender, KeyPressedEventArgs e) {
            if (comboBox1.Items.Count >= 5)
                comboBox1.SelectedIndex = 4;
        }

        private void mon4_KeyPressed(object sender, KeyPressedEventArgs e) {
            if (comboBox1.Items.Count >= 4)
                comboBox1.SelectedIndex = 3;
        }

        private void mon3_KeyPressed(object sender, KeyPressedEventArgs e) {
            if (comboBox1.Items.Count >= 3)
                comboBox1.SelectedIndex = 2;
        }

        private void mon2_KeyPressed(object sender, KeyPressedEventArgs e) {
            if (comboBox1.Items.Count >= 2)
                comboBox1.SelectedIndex = 1;
        }

        private void mon1_KeyPressed(object sender, KeyPressedEventArgs e) {
            if (comboBox1.Items.Count > 1)
                comboBox1.SelectedIndex = 0;
        }

        private void exit_KeyPressed(object sender, KeyPressedEventArgs e) {
            if (continueThread && checkBox1.Checked) {
                continueThread = false;
                Thread.Sleep(200);
                SendColorToComPort(Color.Black);
                continueChecking = true;
            }
        }

        private void btStart_Click(object sender, EventArgs e) {
            Start();
            timer1.Enabled = true;
        }

        private void btStop_Click(object sender, EventArgs e) {
            Stop();
            timer1.Enabled = false;
        }

        private void FormAmbilight_FormClosing(object sender, FormClosingEventArgs e) {
            if (button4.Enabled)
                button4.PerformClick();
            Stop();
            mirror.Dispose();
            notifyIcon1.Visible = false;
            notifyIcon1.Dispose();
        }
        Bitmap myBitmap;
        readonly DesktopMirror mirror = new DesktopMirror();
        public Color getAverageColorMirror() {
            myBitmap = mirror.GetScreen();
            Color screenColor = ScreenAnalysisHelper.GetColorMirror(myBitmap);
            pictureBox1.Image = myBitmap;
            fpsCount++;
            return screenColor;
        }

        public Color getAverageColorGDI() {
            Bitmap myBitmap = Win32APICall.GetDesktop(screen, fastMethod);
            Color screenColor = ScreenAnalysisHelper.GetColorGDI(myBitmap);

            if (!fastMethod)
                Win32APICall.screenshot.Dispose();
            fpsCount++;
            return screenColor;
        }

        private void FormAmbilight_Resize(object sender, EventArgs e) {
            if (WindowState == FormWindowState.Minimized) { this.Hide(); }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e) {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
            this.Focus();
        }

        private void button1_Click(object sender, EventArgs e) {
            if (colorDialog1.ShowDialog() == DialogResult.OK) {
                if (!serialPort.IsOpen)
                    if (!serialPort.OpenPort(baudRate, dataBits, stopBits, parity, cmbPortName.Text))
                        return;
                SendColorToComPort(colorDialog1.Color);
            }

        }

        private void button2_Click(object sender, EventArgs e) {
            Color color;
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
            if (capMode == CaptureMode.GDI) {
                if (comboBox1.Items.Count == 1)
                    fastMethod = true;

                if (Screen.AllScreens[comboBox1.SelectedIndex].Primary)
                    fastMethod = true;
                else
                    fastMethod = false;

                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 100; i++) {
                    color = getAverageColorGDI();
                }

                watch.Stop();
                this.Cursor = System.Windows.Forms.Cursors.Default;
                delay = (int) numericUpDown1.Value;
                var elapsedMs = (watch.ElapsedMilliseconds / 100) + delay;
                decimal fps = Math.Round(((decimal) 1000 / elapsedMs), 2);
                frametime = (decimal) elapsedMs;
                int delayOnBench = delay;
                label3.Text = "FPS: " + Math.Round((decimal) (1000 / (frametime)), 2);
                MessageBox.Show("Time Taken to Each frame: " + elapsedMs + "ms.\nFPS: " + fps);
            } else if (capMode == CaptureMode.DirectX) {

                if (radioButton2.Checked)
                    if (DxScreenCapture.disposed)
                        sc = new DxScreenCapture(comboBox1.SelectedIndex);
                    else {
                        sc.Dispose();
                        sc = new DxScreenCapture(comboBox1.SelectedIndex);
                    }

                Surface s;
                DataRectangle dr;
                DataStream gs;
                Bitmap b;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 100; i++) {
                    s = sc.CaptureScreen();
                    dr = s.LockRectangle(LockFlags.None);
                    gs = dr.Data;
                    color = ScreenAnalysisHelper.avcs(gs);
                    s.UnlockRectangle();
                    s.Dispose();
                }

                watch.Stop();
                this.Cursor = System.Windows.Forms.Cursors.Default;
                delay = (int) numericUpDown1.Value;
                var elapsedMs = (watch.ElapsedMilliseconds / 100) + delay;
                decimal fps = Math.Round(((decimal) 1000 / elapsedMs), 2);
                frametime = (decimal) elapsedMs;
                int delayOnBench = delay;
                label3.Text = "FPS: " + Math.Round((decimal) (1000 / (frametime)), 2);
                MessageBox.Show("Time Taken to Each frame: " + elapsedMs + "ms.\nFPS: " + fps);
            } else if (capMode == CaptureMode.MirrorDriver) {
                mirror.Load();
                mirror.Connect();
                Bitmap b;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 100; i++) {
                    color = getAverageColorMirror();
                }

                watch.Stop();
                this.Cursor = System.Windows.Forms.Cursors.Default;
                delay = (int) numericUpDown1.Value;
                var elapsedMs = (watch.ElapsedMilliseconds / 100) + delay;
                decimal fps = Math.Round(((decimal) 1000 / elapsedMs), 2);
                frametime = (decimal) elapsedMs;
                int delayOnBench = delay;
                label3.Text = "FPS: " + Math.Round((decimal) (1000 / (frametime)), 2);
                mirror.Disconnect();
                mirror.Unload();
                MessageBox.Show("Time Taken to Each frame: " + elapsedMs + "ms.\nFPS: " + fps);
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e) {
            delay = (int) numericUpDown1.Value;
            Properties.Settings.Default.delay = delay;
            Properties.Settings.Default.Save();
        }

        private void trackBar2_ValueChanged(object sender, EventArgs e) {
            brightness = trackBar2.Value;
            Properties.Settings.Default.brightness = brightness;
            Properties.Settings.Default.Save();
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e) {
            rainbowSpeed = trackBar1.Value - 70;
            rainbowSpeed = Math.Abs(rainbowSpeed);
        }

        private void chbxStartWithWindows_CheckedChanged(object sender, EventArgs e) {
            if (chbxStartWithWindows.Checked)
                rkApp.SetValue("AmbientLight", Application.ExecutablePath);
            else
                rkApp.DeleteValue("AmbientLight", false);
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e) {
            if (checkBox2.Checked) {
                btStart.Enabled = false;
                btStop.Enabled = false;
                cmbPortName.Enabled = false;
                button1.Enabled = false;
                button2.Enabled = false;
                trackBar1.Enabled = true;
                if (!serialPort.IsOpen)
                    if (!serialPort.OpenPort(baudRate, dataBits, stopBits, parity, cmbPortName.Text)) {
                        checkBox2.Checked = false;
                        return;
                    }
                Rainbow();
            } else {
                btStart.Enabled = true;
                btStop.Enabled = true;
                button1.Enabled = true;
                button2.Enabled = true;
                trackBar1.Enabled = false;
            }
        }

        private void textBox1_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) {

            HotKeyCls h = new HotKeyCls() { Ctrl = e.Control, Alt = e.Alt, Shift = e.Shift, Key = e.KeyCode };
            textBox1.Text = h.ToString();

            if ((e.KeyCode != Keys.ControlKey) && (e.KeyCode != Keys.Alt) && (e.KeyCode != Keys.ShiftKey) && (e.KeyCode != Keys.LWin) && (e.KeyCode != Keys.RWin)) {
                onoff.UnregisterHotKey(onoffHotkeyId);

                if (e.Alt) {
                    onoffHotkeyId = onoff.RegisterHotKey(e.KeyCode, Helpers.ModifierKeys.Alt);
                    Properties.Settings.Default.onoffHot = h.ToString();
                    Properties.Settings.Default.Save();
                } else if (e.Control) {
                    onoffHotkeyId = onoff.RegisterHotKey(e.KeyCode, Helpers.ModifierKeys.Control);
                    Properties.Settings.Default.onoffHot = "ControlKey+" + e.KeyCode;
                    Properties.Settings.Default.Save();
                } else if (e.Shift) {
                    onoffHotkeyId = onoff.RegisterHotKey(e.KeyCode, Helpers.ModifierKeys.Shift);
                    Properties.Settings.Default.onoffHot = h.ToString();
                    Properties.Settings.Default.Save();
                }
            }

        }

        private void textBox2_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) {
            HotKeyCls h = new HotKeyCls() { Ctrl = e.Control, Alt = e.Alt, Shift = e.Shift, Key = e.KeyCode };
            textBox2.Text = h.ToString();

            if ((e.KeyCode != Keys.ControlKey) && (e.KeyCode != Keys.Alt) && (e.KeyCode != Keys.ShiftKey) && (e.KeyCode != Keys.LWin) && (e.KeyCode != Keys.RWin)) {
                flash.UnregisterHotKey(flashHotkeyId);

                if (e.Alt) {
                    flashHotkeyId = flash.RegisterHotKey(e.KeyCode, Helpers.ModifierKeys.Alt);
                    Properties.Settings.Default.flashHot = h.ToString();
                    Properties.Settings.Default.Save();
                } else if (e.Control) {
                    flashHotkeyId = flash.RegisterHotKey(e.KeyCode, Helpers.ModifierKeys.Control);
                    Properties.Settings.Default.flashHot = "ControlKey+" + e.KeyCode;
                    Properties.Settings.Default.Save();
                } else if (e.Shift) {
                    flashHotkeyId = flash.RegisterHotKey(e.KeyCode, Helpers.ModifierKeys.Shift);
                    Properties.Settings.Default.flashHot = h.ToString();
                    Properties.Settings.Default.Save();
                }

            }
        }

        private void OnKeyPressed(object sender, GlobalKeyboardHookEventArgs e) {
            // F2 - 113
            // F3 - 114
            if (e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyUp) {
                if (e.KeyboardData.VirtualCode.ToString() == Properties.Settings.Default.strobespd1Key) {  // Flash Key (F1)
                    flashPressed = 0;
                } else if (e.KeyboardData.VirtualCode.ToString() == Properties.Settings.Default.strobespd2Key) { // Flash Key (F2)
                    flashPressed = 0;
                } else if (e.KeyboardData.VirtualCode.ToString() == Properties.Settings.Default.strobespd3Key) { // Flash Key (F3)
                    flashPressed = 0;
                } else if (e.KeyboardData.VirtualCode.ToString() == Properties.Settings.Default.blinderKey) // Blinder Key (F4)
                    blinderPressed = false;

            } else {
                if (flashBool)
                    if (e.KeyboardData.VirtualCode.ToString() == Properties.Settings.Default.strobespd1Key) { // Flash Key (F1)
                        flashPressed = 1;
                        e.Handled = true;
                    } else if (e.KeyboardData.VirtualCode.ToString() == Properties.Settings.Default.strobespd2Key) {  // Flash Key (F2)
                        flashPressed = 2;
                        e.Handled = true;
                    } else if (e.KeyboardData.VirtualCode.ToString() == Properties.Settings.Default.strobespd3Key) {  // Flash Key (F3)
                        flashPressed = 3;
                        e.Handled = true;
                    }

                if ((e.KeyboardData.VirtualCode.ToString() == Properties.Settings.Default.blinderKey) && (blindBool)) { // Blinder Key (F4)
                    blinderPressed = true;
                    e.Handled = true;
                }
            }
        }

        private void flash_KeyPressed(object sender, KeyPressedEventArgs e) {
            if (!serialPort.IsOpen)
                if (!serialPort.OpenPort(baudRate, dataBits, stopBits, parity, cmbPortName.Text))
                    return;

            if (flashBool)
                flashBool = false;
            else
                flashBool = true;

            flashT = new Thread(delegate () {
                while (flashBool) {
                    if (flashPressed == 1) {
                        pausedFlash = true;
                        if (flashPressed != 0)
                            SendColorToComPort(Color.White);
                        if (flashPressed != 0)
                            Thread.Sleep(20);
                        if (flashPressed != 0)
                            SendColorToComPort(Color.Black);
                        if (flashPressed != 0)
                            Thread.Sleep(50);
                    } else if (flashPressed == 2) {
                        pausedFlash = true;
                        if (flashPressed != 0)
                            SendColorToComPort(Color.White);
                        if (flashPressed != 0)
                            Thread.Sleep(30);
                        if (flashPressed != 0)
                            SendColorToComPort(Color.Black);
                        if (flashPressed != 0)
                            Thread.Sleep(60);
                    } else if (flashPressed == 3) {
                        pausedFlash = true;
                        if (flashPressed != 0)
                            SendColorToComPort(Color.White);
                        if (flashPressed != 0)
                            Thread.Sleep(35);
                        if (flashPressed != 0)
                            SendColorToComPort(Color.Black);
                        if (flashPressed != 0)
                            Thread.Sleep(80);
                    } else if (flashPressed == 0) {
                        pausedFlash = false;
                        Thread.Sleep(20);
                    }
                }
            });
            flashT.Start();
        }

        int nR, nG, nB;
        private void blind_KeyPressed(object sender, KeyPressedEventArgs e) {

            if (!serialPort.IsOpen)
                if (!serialPort.OpenPort(baudRate, dataBits, stopBits, parity, cmbPortName.Text))
                    return;

            if (blindBool)
                blindBool = false;
            else
                blindBool = true;

            nR = r;
            nG = g;
            nB = b;

            bool updated = false;
            blindT = new Thread(delegate () {
                while (blindBool) {
                    if (!updated) {
                        nR = r;
                        nG = g;
                        nB = b;
                        updated = true;
                    }
                    if (blinderPressed) {
                        pausedBlind = true;
                        nR += 80;
                        nG += 80;
                        nB += 80;

                        if (nR > 255)
                            nR = 255;
                        if (nG > 255)
                            nG = 255;
                        if (nB > 255)
                            nB = 255;
                        Color col = Color.FromArgb(255, nR, nG, nB);
                        SendColorToComPort(col);
                        Thread.Sleep(30);
                    } else {
                        if ((nR == r) && (nG == g) && (nB == b)) {
                            updated = false;
                            pausedBlind = false;
                            Thread.Sleep(20);
                        } else {
                            nR -= 40;
                            nG -= 40;
                            nB -= 40;

                            if (nR < r)
                                nR = r;
                            if (nG < g)
                                nG = g;
                            if (nB < b)
                                nB = b;

                            Color col = Color.FromArgb(255, nR, nG, nB);
                            SendColorToComPort(col);
                            Thread.Sleep(30);
                        }
                    }
                }
            });
            blindT.Start();
        }

        private void textBox4_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) {
            textBox4.Text = e.KeyCode.ToString();
            Properties.Settings.Default.strobespd1Key = e.KeyValue.ToString();
            Properties.Settings.Default.strobespd1KeyName = e.KeyCode.ToString();
            Properties.Settings.Default.Save();
        }

        private void textBox3_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) {
            textBox3.Text = e.KeyCode.ToString();
            Properties.Settings.Default.strobespd2Key = e.KeyValue.ToString();
            Properties.Settings.Default.strobespd2KeyName = e.KeyCode.ToString();
            Properties.Settings.Default.Save();
        }

        private void textBox6_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) {
            textBox6.Text = e.KeyCode.ToString();
            Properties.Settings.Default.strobespd3Key = e.KeyValue.ToString();
            Properties.Settings.Default.strobespd3KeyName = e.KeyCode.ToString();
            Properties.Settings.Default.Save();
        }

        private void textBox5_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) {
            textBox5.Text = e.KeyCode.ToString();
            Properties.Settings.Default.blinderKey = e.KeyValue.ToString();
            Properties.Settings.Default.blinderKeyName = e.KeyCode.ToString();
            Properties.Settings.Default.Save();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e) {
            Properties.Settings.Default.enableWithFull = checkBox1.Checked;
            Properties.Settings.Default.Save();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e) {
            if (btStop.Enabled) {
                bool yes = continueThread;
                btStop.PerformClick();
                btStart.PerformClick();
                continueThread = yes;
                continueChecking = !yes;
            }
            Properties.Settings.Default.screen = comboBox1.SelectedItem.ToString();
            Properties.Settings.Default.Save();
            if (loaded) {
                ScreenID dlg = new ScreenID();
                Screen[] screens = Screen.AllScreens;
                System.Drawing.Rectangle bounds = screens[comboBox1.SelectedIndex].Bounds;
                dlg.SetBounds(bounds.X + Math.Abs(((screens[comboBox1.SelectedIndex].WorkingArea.Width / 2) - (dlg.Width / 2))), bounds.Y + (screens[comboBox1.SelectedIndex].WorkingArea.Height / 2), dlg.Width, dlg.Height);
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Show();
                this.Activate();
                this.Focus();
            }
        }

        private void cmbPortName_SelectedValueChanged(object sender, EventArgs e) {
            if (loaded) {
                Properties.Settings.Default.comPort = cmbPortName.SelectedItem.ToString();
                Properties.Settings.Default.Save();
            }
        }

        private void FormAmbilight_Shown(object sender, EventArgs e) {
            if (chbxStartWithWindows.Checked)
                this.Hide();
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e) {

        }

        private void button5_Click(object sender, EventArgs e) {
            if (!blackout) {
                blackout = true;
                button5.BackColor = Color.Black;
                SendColorToComPort(Color.Black);
            } else {
                blackout = false;
                button5.BackColor = Color.Empty;
            }

        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e) {
            Properties.Settings.Default.captureMode = radioButton2.Checked ? CaptureMode.DirectX : radioButton1.Checked ? CaptureMode.GDI : CaptureMode.MirrorDriver;
            Properties.Settings.Default.Save();
            capMode = Properties.Settings.Default.captureMode;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e) {
            Properties.Settings.Default.captureMode = radioButton2.Checked ? CaptureMode.DirectX : radioButton1.Checked ? CaptureMode.GDI : CaptureMode.MirrorDriver;
            Properties.Settings.Default.Save();
            capMode = Properties.Settings.Default.captureMode;
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e) {
            Properties.Settings.Default.captureMode = radioButton2.Checked ? CaptureMode.DirectX : radioButton1.Checked ? CaptureMode.GDI : CaptureMode.MirrorDriver;
            Properties.Settings.Default.Save();
            capMode = Properties.Settings.Default.captureMode;
            if (loaded)
                if (radioButton3.Checked)
                    if (Screen.AllScreens.Count() > 1)
                        MessageBox.Show("Using the Mirror Driver only works with the first monitor (most left).\nYou can just put the monitor you want to capture as the first left, enable the AL, and them move it back.");
        }

        private void timer1_Tick(object sender, EventArgs e) {
            label3.Text = "FPS: " + fpsCount.ToString();
            fpsCount = 0;
        }

        const double gammafactor = 0.05;
        private void trackBar3_ValueChanged(object sender, EventArgs e) {
            double gammaV = trackBar3.Value * gammafactor;
            for (int i = 0; i <= 255; i++)
                gamma[i] = (byte) (Math.Pow((float) i / 255.0, gammaV) * 255 + 0.5);

            lblGamma.Text = gammaV.ToString();

            if (gammaV == 1)
                lblGamma.ForeColor = Color.Green;
            else
                lblGamma.ForeColor = Color.Black;

            if (loaded) {
                Properties.Settings.Default.gamma = trackBar3.Value;
                Properties.Settings.Default.Save();
            }
        }

        private void checkBox3_CheckedChanged_1(object sender, EventArgs e) {
            if (checkBox3.Checked) {
                btStart.Enabled = false;
                btStop.Enabled = false;
                cmbPortName.Enabled = false;
                button1.Enabled = false;
                button2.Enabled = false;
                trackBar4.Enabled = true;
                trackBar1.Enabled = false;
                checkBox2.Enabled = false;
                if (!serialPort.IsOpen)
                    if (!serialPort.OpenPort(baudRate, dataBits, stopBits, parity, cmbPortName.Text)) {
                        checkBox3.Checked = false;
                        return;
                    }
                //Rainbow();
                Party();
            } else {
                btStart.Enabled = true;
                btStop.Enabled = true;
                button1.Enabled = true;
                button2.Enabled = true;
                checkBox2.Enabled = true;
                trackBar4.Enabled = false;
                trackBar1.Enabled = false;
            }
        }

        private void trackBar4_ValueChanged(object sender, EventArgs e) {
            partyEnergy = trackBar4.Value - 50;
            partyEnergyInverted = trackBar4.Value;
            partyEnergy = Math.Abs(partyEnergy);
        }

        private void tabPage1_Click(object sender, EventArgs e)
        {

        }

        private void hook_KeyPressed(object sender, KeyPressedEventArgs e) {
            if (btStart.Enabled) {
                Start();
                this.notifyIcon1.Icon = new Icon("led-diode-ativado.ico");
                this.notifyIcon1.Text = "Ambient Light\nEnabled";
                this.notifyIcon1.BalloonTipText = "Enabled";
                this.notifyIcon1.BalloonTipTitle = "Ambient Light";
                this.notifyIcon1.ShowBalloonTip(0);
            } else if (btStop.Enabled) {
                Stop();
                this.notifyIcon1.Icon = new Icon("led-diode-desativado.ico");
                this.notifyIcon1.Text = "Ambient Light\nDisabled";
                this.notifyIcon1.BalloonTipText = "Disabled";
                this.notifyIcon1.BalloonTipTitle = "Ambient Light";
                this.notifyIcon1.ShowBalloonTip(0);
            }
        }

        private void notifyIcon1_Click(object sender, EventArgs e) {
            TrayForm form = new TrayForm(this, trackBar2.Enabled);
            form.Show();
            form.Activate();
        }

        private void button3_Click(object sender, EventArgs e) {
            if (!serialPort.IsOpen)
                if (cmbPortName.Text.Length > 0) {
                    if (!serialPort.OpenPort(baudRate, dataBits, stopBits, parity, cmbPortName.Text))
                        return;
                } else
                    return;

            flash_KeyPressed(null, null);
            blind_KeyPressed(null, null);

            btStart.Enabled = true;
            btStop.Enabled = false;
            cmbPortName.Enabled = false;
            button1.Enabled = true;
            button2.Enabled = true;
            button4.Enabled = true;
            button3.Enabled = false;
            trackBar2.Enabled = true;
            checkBox2.Enabled = true;
            checkBox3.Enabled = true;
        }

        private void button4_Click(object sender, EventArgs e) {
            flashBool = false;
            blindBool = false;
            checkBox2.Checked = false;
            if (btStop.Enabled)
                btStop.PerformClick();
            if (serialPort.IsOpen)
                ClosePort();
            btStart.Enabled = false;
            btStop.Enabled = false;
            cmbPortName.Enabled = true;
            button1.Enabled = false;
            button2.Enabled = false;
            button4.Enabled = false;
            button3.Enabled = true;
            trackBar2.Enabled = false;
            checkBox2.Enabled = false;
            checkBox3.Enabled = false;
        }

        #endregion
    }

    public enum CaptureMode {
        GDI = 0,
        DirectX = 1,
        MirrorDriver = 3
    }
}
