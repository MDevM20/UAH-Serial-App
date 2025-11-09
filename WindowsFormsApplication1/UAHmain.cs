using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ZedGraph;
using System.Xml.Linq;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using System.Timers;



namespace WindowsFormsApplication1
{
    public partial class UAHmain : Form
    {
        static UAHmain mainForm;

        public UAHmain()
        {
            mainForm = this;
            InitializeComponent();
            counter_type.SelectedIndex = 1;
            sample_type.SelectedIndex = 1;
            LoadConfig();
        }
        
        private StreamWriter CompleteLog { get; set; }

        static long Received = 0;
        static long Dropped = 0;
        static object locker = new Object();

        static System.Timers.Timer Clock;
        static double voltage_out = 0;
        long sample_count = 0;
       
        static long time = 0;
        int read_byte_start = 0;
        static bool flag = false;
        Thread drow, loaddata;


        byte[] recvBytes = new byte[200000];
        static long[] b1_sample = new long[1500];
        static double[] b1_current = new double[1500];
        static double[] b1_voltage = new double[1500];

        static long[] b2_sample = new long[1500];
        static double[] b2_current = new double[1500];
        static double[] b2_voltage = new double[1500];

        static Queue<element> values = new Queue<element>(10000);

        static byte []message;

        static Queue<byte> recived_bytes = new Queue<byte>(100000);
        static byte[] byte_read = new byte[1000000];


        static System.IO.StreamWriter file, configFile;
        static System.IO.StreamReader configFileLoad;
        static long turn = 0;
        static string load_data_file_name;

        static bool isChartEnabled = false, isSecondChannel = true, existsSampleCounter=false;

        static double xMin, xMax, y1Min, y1Max, y2Min, y2Max;
        static int xStart, xStop;


        static double diplayWindow, timeStep;

        static double xSample, y1Sample, y2Sample, y1SampleMin=0, y1SampleMax=0, y2SampleMin=0, y2SampleMax=0, xScaling=1, y1Scaling=1, y2Scaling=1;


        static byte startByte, checkSum;
        static int packetSize, firstSamplePos, secondSamplePos, sampleConterPos, sampleCounterType, sampleType, counterTurn = 0, previousCount = 0, numOfChannels, checksumEnd, checksumStart;
        static long localCounter = 0;
        static int pointToDraw = 1, pointToDraw_temp = 1;
        static LineItem curve1, curve2;
        static IPointListEdit list1, list2;
        static int index = 0;
        
        private void setProtocol()
        {
            try
            {
                startByte = (byte)start_byte.Value;
                packetSize = (int)packet_size.Value;
                firstSamplePos = (int)first_sample_pos.Value;
                secondSamplePos = (int)second_sample_pos.Value;
                sampleConterPos = (int)counter_position.Value;
                sampleCounterType = (int)counter_type.SelectedIndex;
                sampleType = (int)sample_type.SelectedIndex;
                existsSampleCounter = exists_Sample_Counter.Checked;
                xScaling = Double.Parse(x_Scaling.Text);
                y1Scaling = Double.Parse(y1_Scaling.Text);
                y2Scaling = Double.Parse(y2_Scaling.Text);
                pointToDraw = (int)point_To_Draw.Value;
                pointToDraw_temp = pointToDraw;
                diplayWindow = (double)display_window.Value;
                timeStep = (double)graphStep.Value;
                checksumEnd = (int)checksum_End.Value;
                checksumStart = (int)checksum_Start.Value;
                isSecondChannel = enableSecondChannel.Checked;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void setMaxMinY1()
        {
            mainForm.BeginInvoke((ThreadStart)delegate()
            {
                mainY1min.Value = (decimal)y1SampleMin;
            });
            mainForm.BeginInvoke((ThreadStart)delegate()
            {
                mainY1max.Value = (decimal)y1SampleMax;
            });
            
        }
        private void setMaxMinY2()
        {
            mainForm.BeginInvoke((ThreadStart)delegate()
            {
                mainY2min.Value = (decimal)y2SampleMin;
            });
            mainForm.BeginInvoke((ThreadStart)delegate()
            {
                mainY2max.Value = (decimal)y2SampleMax;
            });
        }
        private void initMainGraph()
        {
            ////////////////////////////


            // Get a reference to the GraphPane
            GraphPane myPane = zedGraphControl1.GraphPane;

            // Set the titles and axis labels
            myPane.Title.IsVisible = false;
            myPane.XAxis.Title.Text = xLabel.Text;
            myPane.YAxis.Title.Text = y1Label.Text;
            if (isSecondChannel)
            {
                myPane.Y2Axis.Title.Text = y2Label.Text;
            }

            // Make up some data points based on the Sine function
            RollingPointPairList cList = new RollingPointPairList(500000);
            RollingPointPairList cList2 = new RollingPointPairList(500000);
            //RollingPointPairList fList = new RollingPointPairList(500000);

            myPane.CurveList.Clear();
            // Generate a red curve with diamond symbols, and "Velocity" in the legend
            LineItem myCurve = myPane.AddCurve(y1Label.Text,
               cList, Color.Red, SymbolType.None);

            if (isSecondChannel)
            {
                LineItem myCurve22 = myPane.AddCurve(y2Label.Text,
                    cList2, Color.Blue, SymbolType.None);
                myCurve22.IsY2Axis = true;
            }
            zedGraphControl1.IsAntiAlias = true;

            myPane.YAxis.Scale.Min = (double)mainY1min.Value;
            myPane.YAxis.Scale.Max = (double)mainY1max.Value;

            if (isSecondChannel)
            {
                myPane.Y2Axis.IsVisible = true;
                myPane.Y2Axis.Scale.Min = (double)mainY2min.Value;
                myPane.Y2Axis.Scale.Max = (double)mainY2max.Value;
            }
            else
            {
                myPane.Y2Axis.IsVisible = false;
            }
            // Fill the symbols with white


            // Show the x axis grid
            myPane.XAxis.MajorGrid.IsVisible = true;

            // Make the Y axis scale red
            myPane.YAxis.Scale.FontSpec.FontColor = Color.Red;
            myPane.YAxis.Title.FontSpec.FontColor = Color.Red;

            myPane.YAxis.Scale.FormatAuto = false;
            if (isSecondChannel)
            {
                myPane.Y2Axis.Scale.FormatAuto = false;
                myPane.Y2Axis.Scale.FontSpec.FontColor = Color.Blue;
                myPane.Y2Axis.Title.FontSpec.FontColor = Color.Blue;
            }
            // turn off the opposite tics so the Y tics don't show up on the Y2 axis
            myPane.YAxis.MajorTic.IsOpposite = false;
            myPane.YAxis.MinorTic.IsOpposite = false;
            // Don't display the Y zero line
            myPane.YAxis.MajorGrid.IsZeroLine = false;
            // Align the Y axis labels so they are flush to the axis
            myPane.YAxis.Scale.Align = AlignP.Inside;

            myPane.Chart.Fill = new Fill(Color.White, Color.LightGoldenrodYellow, 45.0f);

            zedGraphControl1.AxisChange();
            Refresh();

            ////////////////////////////
        }

        private void reInitMainGraph()
        {
            ////////////////////////////


            // Get a reference to the GraphPane
            GraphPane myPane = zedGraphControl1.GraphPane;

            // Set the titles and axis labels
            myPane.Title.IsVisible = false;
            myPane.XAxis.Title.Text = xLabel.Text;
            myPane.YAxis.Title.Text = y1Label.Text;
            if (isSecondChannel)
            {
                myPane.Y2Axis.Title.Text = y2Label.Text;
            }

            // Make up some data points based on the Sine function
            RollingPointPairList cList = new RollingPointPairList(500000);
            RollingPointPairList cList2 = new RollingPointPairList(500000);
            //RollingPointPairList fList = new RollingPointPairList(500000);

            myPane.CurveList.Clear();
            // Generate a red curve with diamond symbols, and "Velocity" in the legend
            LineItem myCurve = myPane.AddCurve(y1Label.Text,
               cList, Color.Red, SymbolType.None);

            if (isSecondChannel)
            {
                LineItem myCurve22 = myPane.AddCurve(y2Label.Text,
                    cList2, Color.Blue, SymbolType.None);
                myCurve22.IsY2Axis = true;
            }
            zedGraphControl1.IsAntiAlias = true;

            myPane.YAxis.Scale.Min = (double)mainY1min.Value;
            myPane.YAxis.Scale.Max = (double)mainY1max.Value;
            myPane.XAxis.Scale.Min = 0;
            myPane.XAxis.Scale.Max = (double)display_window.Value * xScaling;

            if (isSecondChannel)
            {
                myPane.Y2Axis.IsVisible = true;
                myPane.Y2Axis.Scale.Min = (double)mainY2min.Value;
                myPane.Y2Axis.Scale.Max = (double)mainY2max.Value;
            }
            else
            {
                myPane.Y2Axis.IsVisible = false;
            }
            // Fill the symbols with white


            // Show the x axis grid
            myPane.XAxis.MajorGrid.IsVisible = true;

            // Make the Y axis scale red
            myPane.YAxis.Scale.FontSpec.FontColor = Color.Red;
            myPane.YAxis.Title.FontSpec.FontColor = Color.Red;

            myPane.YAxis.Scale.FormatAuto = false;
            if (isSecondChannel)
            {
                myPane.Y2Axis.Scale.FormatAuto = false;
                myPane.Y2Axis.Scale.FontSpec.FontColor = Color.Blue;
                myPane.Y2Axis.Title.FontSpec.FontColor = Color.Blue;
            }
            // turn off the opposite tics so the Y tics don't show up on the Y2 axis
            myPane.YAxis.MajorTic.IsOpposite = false;
            myPane.YAxis.MinorTic.IsOpposite = false;
            // Don't display the Y zero line
            myPane.YAxis.MajorGrid.IsZeroLine = false;
            // Align the Y axis labels so they are flush to the axis
            myPane.YAxis.Scale.Align = AlignP.Inside;

            myPane.Chart.Fill = new Fill(Color.White, Color.LightGoldenrodYellow, 45.0f);

            if (mainForm.zedGraphControl1.GraphPane.CurveList.Count <= 0)
                return;
            curve1 = mainForm.zedGraphControl1.GraphPane.CurveList[0] as LineItem;
            if (curve1 == null)
                return;
            list1 = curve1.Points as IPointListEdit;
            if (list1 == null)
                return;
            if (isSecondChannel)
            {
                curve2 = mainForm.zedGraphControl1.GraphPane.CurveList[1] as LineItem;
                if (curve2 == null)
                    return;
                list2 = curve2.Points as IPointListEdit;
                if (list2 == null)
                    return;
            }

            zedGraphControl1.AxisChange();
            Refresh();

            ////////////////////////////
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
            try
            {               
                Clock = new System.Timers.Timer();
                Clock.Elapsed += new ElapsedEventHandler(Timer_Tick);
                Clock.Interval = 1000;

                string str = string.Empty;
                foreach (string s in SerialPort.GetPortNames())
                {
                    comboComPortNames.Items.Add(s);
                }
                foreach (string s in Enum.GetNames(typeof(Parity)))
                {
                    comboParity.Items.Add(s);
                }
                foreach (string s in Enum.GetNames(typeof(StopBits)))
                {
                    comboStopBits.Items.Add(s);
                }

                foreach (string s in Enum.GetNames(typeof(Handshake)))
                {
                    comboHandshake.Items.Add(s);
                }

                x_start.Maximum = 100000000;
                x_stop.Maximum = 100000000;
         
                diplayWindow = (double)display_window.Value;
              
                initMainGraph();

                comboComPortNames.SelectedIndex = comboComPortNames.Items.Count - 1;

                if (mainForm.zedGraphControl1.GraphPane.CurveList.Count <= 0)
                    return;
                curve1 = mainForm.zedGraphControl1.GraphPane.CurveList[0] as LineItem;
                if (curve1 == null)
                    return;
                list1 = curve1.Points as IPointListEdit;
                if (list1 == null)
                    return;

                curve2 = mainForm.zedGraphControl1.GraphPane.CurveList[1] as LineItem;
                if (curve2 == null)
                    return;
                list2 = curve2.Points as IPointListEdit;
                if (list2 == null)
                    return;
                                
            }
            catch (Exception ex)
            {
            }                      

        }
    
        private void button1_Click(object sender, EventArgs e)
        {
            
            if (connect_button.Text == "Connect")
            {
                setProtocol();
                reInitMainGraph();

                time = 0;
                y1SampleMin = 1000;
                y1SampleMax = 0;
                y2SampleMin = 1000;
                y2SampleMax = 0;
                Received = 0;
                Dropped = 0;
                
               // initMainGraph();
                xSample = 0;
                counterTurn = 0;
                message = new byte[packetSize];
                
                if (serialPort.IsOpen == false)
                {
                    try
                    {
                        connect_button.Enabled = false;

                        serialPort.PortName = comboComPortNames.Text; //port name of the serial port
                        serialPort.BaudRate = Int32.Parse(comboBaudRate.Text); //convert baud rate to int
                        serialPort.Parity = (Parity)Enum.Parse(typeof(Parity), comboParity.Text);
                        serialPort.DataBits = Int32.Parse(comboDataBits.Text);
                        serialPort.StopBits = (StopBits)Enum.Parse(typeof(StopBits), comboStopBits.Text);
                        if (comboHandshake.Text == "None")
                        {
                            serialPort.Handshake = Handshake.None;
                        }
                        else
                        {
                            serialPort.Handshake = (Handshake)Enum.Parse(typeof(Handshake), comboHandshake.Text);
                        }
                        serialPort.ReadTimeout = Timeout.Infinite;
                        serialPort.WriteTimeout = Timeout.Infinite;
                        serialPort.Encoding = Encoding.Default;

                        serialPort.Open();
                     
                        serialPort.DiscardInBuffer();

                        byte[] send_message = new byte[1];

                        send_message[0] = (byte)0x01;
                        
                        serialPort.Write(send_message, 0, 1);
                        
                        comboParity.Enabled = false;
                        comboHandshake.Enabled = false;
                        comboBaudRate.Enabled = false;
                        comboStopBits.Enabled = false;
                        comboDataBits.Enabled = false;
                        comboComPortNames.Enabled = false;

                        connect_button.Enabled = true;
                        connect_button.Text = "Stop";

                        file = new System.IO.StreamWriter(System.DateTime.Now.ToString("yyyy_M_d_HH_mm_ss") + "_debug.txt");
                        file.WriteLine("Start time: " + DateTime.Now.ToString("yyyy-M-d HH:mm:ss"));
                        if (isSecondChannel)
                        {
                            file.WriteLine("Channels:2");
                            file.WriteLine(xLabel.Text + "," + y1Label.Text + "," + y2Label.Text);
                        }
                        else
                        {
                            file.WriteLine("Channels:1");
                            file.WriteLine(xLabel.Text + "," + y1Label.Text);
                        }

                                                
                        Clock.Start();
                        toolStripStatusLabelMessage.Text = "Serial port opened.";
                    }
                    catch (Exception)
                    {
                        toolStripStatusLabelMessage.Text = "Could not open serial port.";
                        connect_button.Enabled = true;
                    }
                }

            }
            else
            {
                connect_button.Text = "Connect";
                Clock.Stop();
                
                try
                {
                                        
                    if (serialPort.IsOpen)
                    {
                        serialPort.DiscardInBuffer();
                        
                        serialPort.Close();
                        
                    }
                    Clock.Stop();
                    if (file != null)
                     {
                        file.WriteLine("END");
                        file.Write("Total time: "+(time / 3600).ToString() + " h " + (time / 60 - 60 * (time / 3600)).ToString() + " m " + (time - 60*(time / 60 - 60 * (time / 3600)) - 3600*(time/3600)).ToString() + " s");
                        file.Close();                     
                     }      

                    comboParity.Enabled = true;
                    comboHandshake.Enabled = true;
                    comboBaudRate.Enabled = true;
                    comboStopBits.Enabled = true;
                    comboDataBits.Enabled = true;
                    comboComPortNames.Enabled = true;
                    toolStripStatusLabelMessage.Text = "Serial port closed.";
                }
                catch (Exception ex)
                {
                    toolStripStatusLabelMessage.Text = "Could not open serial port.";
                    connect_button.Enabled = true;
                }
            }
        }

        private void serialPort_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            
            if (serialPort.IsOpen)
            {
                try
                {
                    int bytesToRead = serialPort.BytesToRead;


                    // reading data from serial port
                    serialPort.Read(recvBytes, read_byte_start, bytesToRead);

                    for (index = 0; index < bytesToRead; index++)
                    {
                        // placing data from serial port in queue for decoding
                        recived_bytes.Enqueue(recvBytes[index]);
                    }
                }
                catch (Exception ex)
                {
                    mainForm.BeginInvoke((ThreadStart)delegate()
                    {
                        mainForm.toolStripStatusLabelMessage.Text = ex.Message;
                    });
                }
            }

           
            while (recived_bytes.Count >= packetSize)
            {
                try
                {
                    //checking if the first byte in queue is begining of the message
                    if (recived_bytes.ElementAt(0) == startByte)
                    {
                        checkSum = 0;
                        for (index = 0; index < packetSize - 1; index++)
                        {
                            if ((index >= checksumStart) && (index <= checksumEnd)) checkSum = (byte)(checkSum + recived_bytes.ElementAt(index));
                            message[index] = recived_bytes.ElementAt(index);
                        }

                        //checking if the checksum is ok
                        if (recived_bytes.ElementAt(packetSize - 1) == checkSum)
                        {
                            if (existsSampleCounter)
                            {
                                switch (sampleCounterType)
                                {
                                    case 0:
                                        xSample = (double)((uint)message[sampleConterPos]);
                                        if (previousCount > xSample)
                                        {
                                            counterTurn++;
                                        }
                                        previousCount = (int)xSample;
                                        xSample += counterTurn * 256;
                                        xSample *= xScaling;
                                        break;
                                    case 1:
                                        xSample = BitConverter.ToUInt16(message, sampleConterPos);
                                        if (previousCount > xSample)
                                        {
                                            counterTurn++;
                                        }
                                        previousCount = (int)xSample;
                                        xSample += counterTurn * 65536;
                                        xSample *= xScaling;
                                        break;
                                    case 2:
                                        xSample = BitConverter.ToUInt32(message, sampleConterPos);
                                        if (previousCount > xSample)
                                        {
                                            counterTurn++;
                                        }
                                        previousCount = (int)xSample;
                                        xSample += counterTurn * 4294967296;
                                        xSample *= xScaling;
                                        break;
                                }
                            }
                            else
                            {
                                xSample = (double)localCounter++;
                            }
                            switch (sampleType)
                            {
                                case 0:
                                    y1Sample = (double)((uint)message[firstSamplePos]);
                                    y1Sample *= y1Scaling;
                                    if (isSecondChannel)
                                    {
                                        y2Sample = (double)((uint)message[secondSamplePos]);
                                        y2Sample *= y2Scaling;
                                    }
                                    break;
                                case 1:
                                    y1Sample = BitConverter.ToInt16(message, firstSamplePos);
                                    y1Sample *= y1Scaling;
                                    if (isSecondChannel)
                                    {
                                        y2Sample = BitConverter.ToInt16(message, secondSamplePos);
                                        y2Sample *= y2Scaling;
                                    }
                                    break;
                                case 2:
                                    y1Sample = BitConverter.ToInt32(message, firstSamplePos);
                                    y1Sample *= y1Scaling;
                                    if (isSecondChannel)
                                    {
                                        y2Sample = BitConverter.ToInt32(message, secondSamplePos);
                                        y2Sample *= y2Scaling;
                                    }
                                    break;
                                case 3:
                                    y1Sample = BitConverter.ToInt64(message, (byte)firstSamplePos);
                                    y1Sample *= y1Scaling;
                                    if (isSecondChannel)
                                    {
                                        y2Sample = BitConverter.ToInt64(message, secondSamplePos);
                                        y2Sample *= y2Scaling;
                                    }
                                    break;
                                case 4:
                                    y1Sample = BitConverter.ToUInt16(message, firstSamplePos);
                                    y1Sample *= y1Scaling;
                                    if (isSecondChannel)
                                    {
                                        y2Sample = BitConverter.ToUInt16(message, secondSamplePos);
                                        y2Sample *= y2Scaling;
                                    }
                                    break;
                                case 5:
                                    y1Sample = BitConverter.ToUInt32(message, firstSamplePos);
                                    y1Sample *= y1Scaling;
                                    if (isSecondChannel)
                                    {
                                        y2Sample = BitConverter.ToUInt32(message, secondSamplePos);
                                        y2Sample *= y2Scaling;
                                    }
                                    break;
                                case 6:
                                    y1Sample = BitConverter.ToUInt64(message, firstSamplePos);
                                    y1Sample *= y1Scaling;
                                    if (isSecondChannel)
                                    {
                                        y2Sample = BitConverter.ToUInt64(message, secondSamplePos);
                                        y2Sample *= y2Scaling;
                                    }
                                    break;
                                case 7:
                                    y1Sample = BitConverter.ToSingle(message, firstSamplePos);
                                    y1Sample *= y1Scaling;
                                    if (isSecondChannel)
                                    {
                                        y2Sample = BitConverter.ToSingle(message, secondSamplePos);
                                        y2Sample *= y2Scaling;
                                    }
                                    break;
                                case 8:
                                    y1Sample = BitConverter.ToDouble(message, firstSamplePos);
                                    y1Sample *= y1Scaling;
                                    if (isSecondChannel)
                                    {
                                        y2Sample = BitConverter.ToDouble(message, secondSamplePos);
                                        y2Sample *= y2Scaling;
                                    }
                                    break;
                            }                                                    
                            
                            if (file != null)
                            {
                                try
                                {
                                    file.Write(xSample);
                                    file.Write(",");
                                    file.Write(y1Sample.ToString("f3"));
                                    file.Write(",");
                                    file.WriteLine(y2Sample.ToString("f3"));
                                }
                                catch (Exception ex)
                                {
                                }
                            }

                            ////////////////////////////
                            // Drawing on the graph
                            ////////////////////////////
                            if (isChartEnabled)
                            {
                                try
                                {
                                    list1.Add(xSample, y1Sample);
                                    if (y1Sample > y1SampleMax) y1SampleMax = y1Sample;
                                    if (y1Sample < y1SampleMin) y1SampleMin = y1Sample;

                                    if (isSecondChannel)
                                    {
                                        if (y2Sample > y2SampleMax) y2SampleMax = y2Sample;
                                        if (y2Sample < y2SampleMin) y2SampleMin = y2Sample;
                                        list2.Add(xSample, y2Sample);
                                    }                                 

                                    pointToDraw_temp--;
                                }
                                catch (Exception)
                                {
                                }

                                if (pointToDraw_temp == 0)
                                {
                                    try
                                    {
                                        Scale xScale = mainForm.zedGraphControl1.GraphPane.XAxis.Scale;
                                        if (xSample > xScale.Max - timeStep * xScaling)
                                        {
                                            xScale.Max = xSample + timeStep * xScaling;
                                            xScale.Min = xScale.Max - diplayWindow * xScaling;
                                        }

                                        if (y1Auto.Checked)
                                        {
                                            mainForm.zedGraphControl1.GraphPane.YAxis.Scale.Min = y1SampleMin;
                                            mainForm.zedGraphControl1.GraphPane.YAxis.Scale.Max = y1SampleMax;
                                            setMaxMinY1();
                                        }
                                        else
                                        {
                                            mainForm.zedGraphControl1.GraphPane.YAxis.Scale.Min = y1Min;
                                            mainForm.zedGraphControl1.GraphPane.YAxis.Scale.Max = y1Max;
                                        }
                                                                             

                                        mainForm.zedGraphControl1.GraphPane.XAxis.Title.Text = xLabel.Text;
                                        mainForm.zedGraphControl1.GraphPane.YAxis.Title.Text = y1Label.Text;

                                        if (isSecondChannel)
                                        {
                                            if (y2Auto.Checked)
                                            {
                                                mainForm.zedGraphControl1.GraphPane.Y2Axis.Scale.Min = y2SampleMin;
                                                mainForm.zedGraphControl1.GraphPane.Y2Axis.Scale.Max = y2SampleMax;
                                                setMaxMinY2();
                                            }
                                            else
                                            {
                                                mainForm.zedGraphControl1.GraphPane.Y2Axis.Scale.Min = y2Min;
                                                mainForm.zedGraphControl1.GraphPane.Y2Axis.Scale.Max = y2Max;
                                            }
                                            mainForm.zedGraphControl1.GraphPane.Y2Axis.Title.Text = y2Label.Text;
                                         }

                                                              
                                        
                                        mainForm.zedGraphControl1.AxisChange();
                                        mainForm.zedGraphControl1.Invalidate();

                                    }
                                    catch (Exception)
                                    {
                                    }
                                    pointToDraw_temp = pointToDraw;       
                                }
                            }
                            
                            /////////////////////
                        

                            for (index = 0; index < packetSize; index++)
                            {
                                recived_bytes.Dequeue();
                            }

                            Received++;

                            mainForm.BeginInvoke((ThreadStart)delegate()
                            {
                                toolStripStatusReceived.Text = "Received :" + Received.ToString()+" packets";
                            });

                            
                        }
                        else
                        {
                            recived_bytes.Dequeue();
                            Dropped++;
                            mainForm.BeginInvoke((ThreadStart)delegate()
                            {
                                mainForm.toolStripStatusDropped.Text = "Dropped :" + Dropped.ToString()+" bytes";
                            });
                        }
                    }
                    else
                    {
                        recived_bytes.Dequeue();
                        Dropped++;
                        mainForm.BeginInvoke((ThreadStart)delegate()
                        {
                            mainForm.toolStripStatusDropped.Text = "Dropped :" + Dropped.ToString() + " bytes";
                        });
                    }
                }
                catch (Exception ex)
                {

                }
            }
            
        }
         
        private void checkChart_CheckedChanged(object sender, EventArgs e)
        {
            isChartEnabled = checkChart.Checked;
            
        }

        private static void Timer_Tick(object source, ElapsedEventArgs e)
        {
            time++;
            mainForm.BeginInvoke((ThreadStart)delegate()
                {
                    mainForm.toolStripStatusTime.Text = "Time: " + (time / 3600).ToString() + " h " + (time / 60 - 60 * (time / 3600)).ToString() + " m " + (time - 60*(time / 60 - 60 * (time / 3600)) - 3600*(time/3600)).ToString() + " s";
                });
           
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {

                OpenFileDialog openFileDialog1 = new OpenFileDialog();

                openFileDialog1.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog1.FilterIndex = 1;
                openFileDialog1.RestoreDirectory = true;

                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    if (openFileDialog1.CheckFileExists)
                    {
                        xStart = (int)x_start.Value;
                        xStop = (int)x_stop.Value; 
                        load_data_file_name = openFileDialog1.FileName;
                        loaddata = new Thread(load_data);
                        loaddata.Start();   

                    }
                }
            }
            catch (Exception ex)
            {

            }

              
           
        }

        static void load_data()
        {
            StreamReader re = File.OpenText(load_data_file_name);
            string[] parts;
            IPointListEdit list1 = null;
            IPointListEdit list2 = null;
            string input = null;
            numOfChannels = 1;

            double y1max = 0, y1min = 10000, y2max = 0, y2min = 10000, xMinOld=-1;
            try
            {
                time = 0;                
                input = re.ReadLine(); //reading first line 

                input = re.ReadLine(); //reading second line
                parts = input.Split(':');
                numOfChannels = int.Parse(parts[1]);

                input = re.ReadLine(); //reading 3-rd line
                parts = input.Split(','); //spliting line
                ////////////////////////////


                // Get a reference to the GraphPane
                GraphPane myPane = mainForm.zedGraphControl2.GraphPane;

                // Set the titles and axis labels
                myPane.Title.IsVisible = false;
                myPane.XAxis.Title.Text = parts[0];
                myPane.YAxis.Title.Text = parts[1];
                if (numOfChannels==2)
                {
                    myPane.Y2Axis.Title.Text = parts[2];
                }

                // Make up some data points based on the Sine function
                RollingPointPairList cList = new RollingPointPairList(500000);
                RollingPointPairList cList2 = new RollingPointPairList(500000);
                //RollingPointPairList fList = new RollingPointPairList(500000);

                myPane.CurveList.Clear();
                // Generate a red curve with diamond symbols, and "Velocity" in the legend
                LineItem myCurve = myPane.AddCurve(parts[1],
                   cList, Color.Red, SymbolType.None);

                if (numOfChannels == 2)
                {
                    LineItem myCurve22 = myPane.AddCurve(parts[2],
                        cList2, Color.Blue, SymbolType.None);
                    myCurve22.IsY2Axis = true;
                    myPane.Y2Axis.IsVisible = true;
                }
                else
                {
                    myPane.Y2Axis.IsVisible = false;
                }
                mainForm.zedGraphControl2.IsAntiAlias = true;



                // Show the x axis grid
                myPane.XAxis.MajorGrid.IsVisible = true;

                // Make the Y axis scale red
                myPane.YAxis.Scale.FontSpec.FontColor = Color.Red;
                myPane.YAxis.Title.FontSpec.FontColor = Color.Red;

                myPane.YAxis.Scale.FormatAuto = false;
                if (numOfChannels == 2)
                {
                    myPane.Y2Axis.Scale.FormatAuto = false;
                    myPane.Y2Axis.Scale.FontSpec.FontColor = Color.Blue;
                    myPane.Y2Axis.Title.FontSpec.FontColor = Color.Blue;
                }
                // turn off the opposite tics so the Y tics don't show up on the Y2 axis
                myPane.YAxis.MajorTic.IsOpposite = false;
                myPane.YAxis.MinorTic.IsOpposite = false;
                // Don't display the Y zero line
                myPane.YAxis.MajorGrid.IsZeroLine = false;
                // Align the Y axis labels so they are flush to the axis
                myPane.YAxis.Scale.Align = AlignP.Inside;

                myPane.Chart.Fill = new Fill(Color.White, Color.LightGoldenrodYellow, 45.0f);

                ////////////////////////////

                if (mainForm.zedGraphControl2.GraphPane.CurveList.Count <= 0)
                    return;
                LineItem curve1 = mainForm.zedGraphControl2.GraphPane.CurveList[0] as LineItem;
                if (curve1 == null)
                    return;
                list1 = curve1.Points as IPointListEdit;
                if (list1 == null)
                    return;

                if (numOfChannels == 2)
                {
                    LineItem curve2 = mainForm.zedGraphControl2.GraphPane.CurveList[1] as LineItem;
                    if (curve2 == null)
                        return;
                    list2 = curve2.Points as IPointListEdit;
                    if (list2 == null)
                    {
                        return;
                    }
                    list2.Clear();
                }
                list1.Clear();
                

            }
            catch (Exception ex){
                MessageBox.Show(ex.Message, "Error");
            }

            Double max_t = 1;
            Double x = 0, y1 = 0, y2 = 0;
            int sampleNum = 0;
            while ((input = re.ReadLine()) != null) //reading line
            {
                if (input.Trim() != "" || input.Trim() != "END")
                {
                    sampleNum++;
                    parts = input.Split(','); //spliting line
                    try
                    {                      

                        x = double.Parse(parts[0]);
                        y1 = double.Parse(parts[1]);
                        if (y1max < y1) y1max = y1;
                        if (y1min > y1) y1min = y1;
                        if (xMinOld == -1) xMinOld = x;

                        if (numOfChannels == 2)
                        {
                            y2 = double.Parse(parts[2]);
                            if (y2min > y2) y2min = y2;
                            if (y2max < y2) y2max = y2;
                        }
                        if (sampleNum >= xStart && (xStop == 0 || sampleNum <= xStop))
                        {
                            list1.Add(x, y1);
                            if (numOfChannels == 2)
                            {
                                list2.Add(x, y2);
                            }
                            max_t = x;
                            time++;
                            mainForm.BeginInvoke((ThreadStart)delegate()
                             {
                                 mainForm.load_label.Text = "Samples loaded : " + time.ToString();
                             });
                        }
                        if (xStop != 0 && sampleNum > xStop) { break; }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            try
            {

                mainForm.BeginInvoke((ThreadStart)delegate()
                {
                    mainForm.zedGraphControl2.GraphPane.YAxis.Scale.Min = y1min;
                });
                mainForm.BeginInvoke((ThreadStart)delegate()
                {
                    mainForm.oldY1min.Value = (decimal)y1min;
                });
                mainForm.BeginInvoke((ThreadStart)delegate()
                {
                    mainForm.zedGraphControl2.GraphPane.YAxis.Scale.Max = y1max;
                });
                mainForm.BeginInvoke((ThreadStart)delegate()
                {
                    mainForm.oldY1max.Value = (decimal)y1max;
                });

                if (numOfChannels == 2)
                {
                    mainForm.BeginInvoke((ThreadStart)delegate()
                    {
                        mainForm.zedGraphControl2.GraphPane.Y2Axis.Scale.Min = y2min;
                    });
                    mainForm.BeginInvoke((ThreadStart)delegate()
                    {
                        mainForm.oldY2min.Value = (decimal)y2min;
                    });
                    mainForm.BeginInvoke((ThreadStart)delegate()
                    {
                        mainForm.zedGraphControl2.GraphPane.Y2Axis.Scale.Max = y2max;
                    });
                    mainForm.BeginInvoke((ThreadStart)delegate()
                    {
                        mainForm.oldY2max.Value = (decimal)y2max;
                    });

                }
                else
                {
                    mainForm.BeginInvoke((ThreadStart)delegate()
                    {
                        mainForm.zedGraphControl2.GraphPane.Y2Axis.Scale.Min = 0;
                    });
                    mainForm.BeginInvoke((ThreadStart)delegate()
                    {
                        mainForm.oldY2min.Value = 0;
                    });
                    mainForm.BeginInvoke((ThreadStart)delegate()
                    {
                        mainForm.zedGraphControl2.GraphPane.Y2Axis.Scale.Max = 1;
                    });
                    mainForm.BeginInvoke((ThreadStart)delegate()
                    {
                        mainForm.oldY2max.Value = 1;
                    });
                }
                mainForm.BeginInvoke((ThreadStart)delegate()
                {
                    mainForm.zedGraphControl2.GraphPane.XAxis.Scale.Max = max_t;
                });
                mainForm.BeginInvoke((ThreadStart)delegate()
                {
                    mainForm.oldXmax.Value = (decimal)max_t;
                });
                mainForm.BeginInvoke((ThreadStart)delegate()
                {
                    mainForm.zedGraphControl2.GraphPane.XAxis.Scale.Min = xMinOld;
                });
                mainForm.BeginInvoke((ThreadStart)delegate()
                {
                    mainForm.oldXmin.Value = (decimal)xMinOld;
                });

                mainForm.BeginInvoke((ThreadStart)delegate()
                {
                    mainForm.zedGraphControl2.AxisChange();
                });
                mainForm.BeginInvoke((ThreadStart)delegate()
                {
                    mainForm.zedGraphControl2.Invalidate();
                });                     

                
            }
            catch (Exception)
            {
            }


            re.Close();
            

        }
                 
        private void display_window_ValueChanged(object sender, EventArgs e)
        {
            diplayWindow = (double)display_window.Value;
        }

        private void enableSecondChannel_CheckedChanged(object sender, EventArgs e)
        {
            isSecondChannel = enableSecondChannel.Checked;
        }
             
        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult result;
            result = MessageBox.Show("Are you sure you want to save configuration?", "Settings", MessageBoxButtons.YesNo);
                 

            if (result == DialogResult.Yes)
            {
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            setProtocol();
            try
            {
                configFile = new System.IO.StreamWriter("config.dat", false);
                configFile.WriteLine(x_Scaling.Text);

                configFile.WriteLine(xLabel.Text);
                configFile.WriteLine(y1_Scaling.Text);
                configFile.WriteLine(y1Label.Text);
                configFile.WriteLine(enableSecondChannel.Checked.ToString());
                configFile.WriteLine(y2_Scaling.Text);
                configFile.WriteLine(y2Label.Text);
                configFile.WriteLine(start_byte.Value);
                configFile.WriteLine(packet_size.Value);
                configFile.WriteLine(sample_type.SelectedIndex);
                configFile.WriteLine(first_sample_pos.Value);
                configFile.WriteLine(second_sample_pos.Value);
                configFile.WriteLine(exists_Sample_Counter.Checked.ToString());
                configFile.WriteLine(counter_position.Value);
                configFile.WriteLine(counter_type.SelectedIndex);
                configFile.WriteLine(point_To_Draw.Value);
                configFile.WriteLine(display_window.Value);
                configFile.WriteLine(graphStep.Value);
                configFile.WriteLine(checksum_Start.Value);
                configFile.WriteLine(checksum_End.Value);
                configFile.Close();
                MessageBox.Show("Your configuration has been successfully saved!", "Settings");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void LoadConfig()
        {
            try
            {
                StreamReader re = File.OpenText("config.dat");
          
                string input = null;
                input = re.ReadLine();
                x_Scaling.Text = input;
                input = re.ReadLine();
                xLabel.Text = input;
                input = re.ReadLine();
                y1_Scaling.Text = input;
                input = re.ReadLine();
                y1Label.Text = input;
                input = re.ReadLine();
                enableSecondChannel.Checked = bool.Parse(input);
                input = re.ReadLine();
                y2_Scaling.Text = input;
                input = re.ReadLine();
                y2Label.Text = input;
                input = re.ReadLine();
                start_byte.Value = decimal.Parse(input);
                input = re.ReadLine();
                packet_size.Value = decimal.Parse(input);
                input = re.ReadLine();
                sample_type.SelectedIndex = int.Parse(input);
                input = re.ReadLine();
                first_sample_pos.Value = decimal.Parse(input);
                input = re.ReadLine();
                second_sample_pos.Value = decimal.Parse(input);
                input = re.ReadLine();
                exists_Sample_Counter.Checked= bool.Parse(input);
                input = re.ReadLine();
                counter_position.Value = decimal.Parse(input);
                input = re.ReadLine();
                counter_type.SelectedIndex = int.Parse(input);
                input = re.ReadLine();
                point_To_Draw.Value = decimal.Parse(input);
                input = re.ReadLine();
                display_window.Value= decimal.Parse(input);
                input = re.ReadLine();
                graphStep.Value = decimal.Parse(input);
                input = re.ReadLine();
                checksum_Start.Value = decimal.Parse(input);
                input = re.ReadLine();
                checksum_End.Value = decimal.Parse(input);


                re.Close();
                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Settings Loading");
            }

        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            DialogResult result;
            result = MessageBox.Show("Are you sure you want to load configuration?", "Settings", MessageBoxButtons.YesNo);
            
            if (result == DialogResult.Yes)
            {
                LoadConfig();
            }
        }

        private void point_To_Draw_ValueChanged(object sender, EventArgs e)
        {
            pointToDraw = (int)point_To_Draw.Value;
        }
          
        private void graphStep_ValueChanged(object sender, EventArgs e)
        {
            timeStep = (double)graphStep.Value;
        }

        private void mainY1min_ValueChanged(object sender, EventArgs e)
        {
            y1Min = (double)mainY1min.Value;
        }

        private void mainY1max_ValueChanged(object sender, EventArgs e)
        {
            y1Max = (double)mainY1max.Value;
        }

        private void mainY2min_ValueChanged(object sender, EventArgs e)
        {
            y2Min = (double)mainY2min.Value;
        }

        private void mainY2max_ValueChanged(object sender, EventArgs e)
        {
            y2Max = (double)mainY2max.Value;
        }

        private void Refresh_Click(object sender, EventArgs e)
        {
            mainForm.zedGraphControl2.GraphPane.YAxis.Scale.Min = (double)oldY1min.Value;
            mainForm.zedGraphControl2.GraphPane.YAxis.Scale.Max = (double)oldY1max.Value;
            mainForm.zedGraphControl2.GraphPane.Y2Axis.Scale.Min = (double)oldY2min.Value;
            mainForm.zedGraphControl2.GraphPane.Y2Axis.Scale.Max = (double)oldY2max.Value;
            mainForm.zedGraphControl2.GraphPane.XAxis.Scale.Min = (double)oldXmin.Value;
            mainForm.zedGraphControl2.GraphPane.XAxis.Scale.Max = (double)oldXmax.Value;
            mainForm.zedGraphControl2.AxisChange();
            mainForm.zedGraphControl2.Invalidate();
            mainForm.zedGraphControl2.Refresh();
        }

        private void label1_Click(object sender, EventArgs e)
        {
            comboComPortNames.Items.Clear();
            string str = string.Empty;
            foreach (string s in SerialPort.GetPortNames())
            {
                comboComPortNames.Items.Add(s);
            }
        }

       
                    
                  
    }
}
