﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using JasperUI.Model;
using BingLibrary.hjb.file;
using BingLibrary.hjb.tools;
using OfficeOpenXml;
using System.IO;
using ViewROI;
using BingLibrary.HVision;
using HalconDotNet;
using System.Diagnostics;
using System.Data;

namespace JasperUI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 变量
        bool CameraState = false;
        bool PLCState = false;
        bool RobotState = false;
        bool EpsonStatusAuto = false;
        bool EpsonStatusWarning = false;
        bool EpsonStatusSError = false;
        bool EpsonStatusSafeGuard = false;
        bool EpsonStatusEStop = false;
        bool EpsonStatusError = false;
        bool EpsonStatusPaused = false;
        bool EpsonStatusRunning = false;
        bool EpsonStatusReady = false;
        private EpsonRC90 epsonRC90;
        private string iniParameterPath = System.Environment.CurrentDirectory + "\\Parameter.ini";
        
        Leisai ls;
        List<int[]> ExIoIn, ExIoOut;
        long SWms = 0;
        #endregion
        public MainWindow()
        {
            InitializeComponent();

            epsonRC90 = new EpsonRC90();

            GlobalVars.viewController1 = new HWndCtrl(ImageWindow1);
            GlobalVars.roiController1 = new ROIController();
            GlobalVars.viewController1.useROIController(GlobalVars.roiController1);
            GlobalVars.viewController1.setViewState(HWndCtrl.MODE_VIEW_MOVE);


            epsonRC90.ModelPrint += ModelPrintEventProcess;
            epsonRC90.EpsonStatusUpdate += EpsonStatusUpdateProcess;

        }
        #region 功能函数
        void AddMessage(string str)
        {
            this.Dispatcher.Invoke(new Action(() => {

                string[] s = MsgTextBox.Text.Split('\n');
                if (s.Length > 1000)
                {
                    MsgTextBox.Text = "";
                }
                if (MsgTextBox.Text != "")
                {
                    MsgTextBox.Text += "\r\n";
                }
                MsgTextBox.Text += DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " " + str;

            }));
        }
        void LoadedCallback()
        {
            Init();
            AddMessage("软件加载完成");
        }
        void Init()
        {
            try
            {
                ls = new Leisai();
                if (ls.Connect() < 3)
                {
                    throw new Exception("轴卡异常");
                }
                else
                {
                    string ExIoExcelPath = System.Environment.CurrentDirectory + "\\RobotExIo.xlsx";

                    if (File.Exists(ExIoExcelPath))
                    {
                        FileInfo existingFile = new FileInfo(ExIoExcelPath);
                        using (ExcelPackage package = new ExcelPackage(existingFile))
                        {
                            // get the first worksheet in the workbook
                            ExcelWorksheet worksheet = package.Workbook.Worksheets[1];
                            ExIoIn = new List<int[]>();//雷赛卡的输入
                            ExIoOut = new List<int[]>();//雷赛卡的输出
                            for (int i = 2; i <= worksheet.Dimension.End.Row; i++)
                            {
                                if (worksheet.Cells["A" + i.ToString()].Value != null && worksheet.Cells["B" + i.ToString()].Value != null && worksheet.Cells["C" + i.ToString()].Value != null)
                                {
                                    ExIoIn.Add(new int[3] { int.Parse(worksheet.Cells["A" + i.ToString()].Value.ToString()), int.Parse(worksheet.Cells["B" + i.ToString()].Value.ToString()), int.Parse(worksheet.Cells["C" + i.ToString()].Value.ToString()) });
                                }
                                if (worksheet.Cells["D" + i.ToString()].Value != null && worksheet.Cells["E" + i.ToString()].Value != null && worksheet.Cells["F" + i.ToString()].Value != null)
                                {
                                    ExIoOut.Add(new int[3] { int.Parse(worksheet.Cells["D" + i.ToString()].Value.ToString()), int.Parse(worksheet.Cells["E" + i.ToString()].Value.ToString()), int.Parse(worksheet.Cells["F" + i.ToString()].Value.ToString()) });
                                }
                            }
                        }

                        ExIoExcelPath = System.Environment.CurrentDirectory + "\\排版.xlsx";
                        if (File.Exists(ExIoExcelPath))
                        {
                            existingFile = new FileInfo(ExIoExcelPath);
                            using (ExcelPackage package = new ExcelPackage(existingFile))
                            {
                                // get the first worksheet in the workbook
                                ExcelWorksheet worksheet = package.Workbook.Worksheets[1];
                                for (int i = 0; i < 4; i++)
                                {
                                    for (int j = 0; j < 3; j++)
                                    {
                                        for (int k = 0; k < 4; k++)
                                        {
                                            epsonRC90.BordIndexA[i * 3 + j, k] = int.Parse(worksheet.Cells[13 + i * 2, 4 - k + j * 4].Value.ToString());
                                        }
                                        for (int k = 0; k < 4; k++)
                                        {
                                            epsonRC90.BordIndexA[i * 3 + j, k + 4] = int.Parse(worksheet.Cells[12 + i * 2, 4 - k + j * 4].Value.ToString());
                                        }
                                    }
                                }

                                for (int i = 0; i < 4; i++)
                                {
                                    for (int j = 0; j < 3; j++)
                                    {
                                        for (int k = 0; k < 4; k++)
                                        {
                                            epsonRC90.BordIndexB[i * 3 + j, k] = int.Parse(worksheet.Cells[22 + i * 2, 4 - k + j * 4].Value.ToString());
                                        }
                                        for (int k = 0; k < 4; k++)
                                        {
                                            epsonRC90.BordIndexB[i * 3 + j, k + 4] = int.Parse(worksheet.Cells[21 + i * 2, 4 - k + j * 4].Value.ToString());
                                        }
                                    }
                                }
                            }
                            if (GlobalVars.Camera.OpenCamera("CAM1","GigEVision"))
                            {
                                MachineID.Text = Inifile.INIGetStringValue(iniParameterPath, "System", "MachineID", "Jasper01");
                                string COM = Inifile.INIGetStringValue(iniParameterPath, "Scan", "Scan1", "COM0");
                                GlobalVars.Scan1 = new Scan();
                                GlobalVars.Scan1.ini(COM);
                                Async.RunFuncAsync(ls.Run, null);//刷IO卡
                                string ip = Inifile.INIGetStringValue(iniParameterPath, "FX5U", "Ip", "192.168.0.20");
                                int port = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "FX5U", "Port", "502"));
                                GlobalVars.Fx5u = new Fx5u(ip, port);
                                Async.RunFuncAsync(IORun, null);
                                Run();
                            }
                            else
                            {
                                throw new Exception("相机打开失败");
                            }




                        }
                        else
                        {
                            throw new Exception("排版文件不存在");
                        }



                    }
                    else
                    {
                        throw new Exception("IO文件不存在");
                    }


                }
            }
            catch (Exception ex)
            {
                AddMessage(ex.Message);
            }
        }
        void UpdateUI()
        {
            #region 机械手状态
            if (EpsonStatusAuto)
            {
                EllipseEpsonStatusAuto.Fill = Brushes.Yellow;
            }
            else
            {
                EllipseEpsonStatusAuto.Fill = Brushes.Gray;
            }
            if (EpsonStatusWarning)
            {
                EllipseEpsonStatusWarning.Fill = Brushes.Yellow;
            }
            else
            {
                EllipseEpsonStatusWarning.Fill = Brushes.Gray;
            }
            if (EpsonStatusSError)
            {
                EllipseEpsonStatusSError.Fill = Brushes.Yellow;
            }
            else
            {
                EllipseEpsonStatusSError.Fill = Brushes.Gray;
            }
            if (EpsonStatusSafeGuard)
            {
                EllipseEpsonStatusSafeGuard.Fill = Brushes.Yellow;
            }
            else
            {
                EllipseEpsonStatusSafeGuard.Fill = Brushes.Gray;
            }
            if (EpsonStatusEStop)
            {
                EllipseEpsonStatusEStop.Fill = Brushes.Yellow;
            }
            else
            {
                EllipseEpsonStatusEStop.Fill = Brushes.Gray;
            }
            if (EpsonStatusError)
            {
                EllipseEpsonStatusError.Fill = Brushes.Yellow;
            }
            else
            {
                EllipseEpsonStatusError.Fill = Brushes.Gray;
            }
            if (EpsonStatusPaused)
            {
                EllipseEpsonStatusPaused.Fill = Brushes.Yellow;
            }
            else
            {
                EllipseEpsonStatusPaused.Fill = Brushes.Gray;
            }
            if (EpsonStatusRunning)
            {
                EllipseEpsonStatusRunning.Fill = Brushes.Yellow;
            }
            else
            {
                EllipseEpsonStatusRunning.Fill = Brushes.Gray;
            }
            if (EpsonStatusReady)
            {
                EllipseEpsonStatusReady.Fill = Brushes.Yellow;
            }
            else
            {
                EllipseEpsonStatusReady.Fill = Brushes.Gray;
            }
            #endregion
            #region 设备连接状态
            if (PLCState)
            {
                EllipsePLCState.Fill = Brushes.Green;
            }
            else
            {
                EllipsePLCState.Fill = Brushes.Red;
            }
            if (RobotState)
            {
                EllipseRobotState.Fill = Brushes.Green;
            }
            else
            {
                EllipseRobotState.Fill = Brushes.Red;
            }
            if (CameraState)
            {
                EllipseCameraState.Fill = Brushes.Green;
            }
            else
            {
                EllipseCameraState.Fill = Brushes.Red;
            }
            #endregion
            #region 其他
            CycleText.Text = SWms.ToString() + " ms";
            #endregion
        }
        async void Run()
        {
            
            Stopwatch sw = new Stopwatch();
            bool m2000 = false,m2004 = false;
            bool[] M2000;
            bool first = true;
            while (true)
            {
                sw.Restart();
                await Task.Delay(100);
                #region UpdateUI
                PLCState = GlobalVars.Fx5u.Connect;
                CameraState = GlobalVars.Camera.Connected;
                RobotState = epsonRC90.CtrlStatus && epsonRC90.IOReceiveStatus && epsonRC90.TestReceiveStatus && epsonRC90.TestSendStatus;
                UpdateUI();
                #endregion
                #region work
                try
                {
                    M2000 = await Task.Run<bool[]>(() => {
                        return GlobalVars.Fx5u.ReadMultiM("M2000", 16);
                    });
                    if (M2000 != null)
                    {
                        if (first)
                        {
                            first = false;
                            m2000 = M2000[0];
                            m2004 = M2000[4];
                        }
                        if (m2000 != M2000[0])
                        {
                            m2000 = M2000[0];
                            if (m2000)
                            {
                                GlobalVars.Fx5u.SetM("M2000", false);
                                GlobalVars.Fx5u.SetM("M2500", false);
                                GlobalVars.Fx5u.SetM("M2501", false);
                                GlobalVars.Fx5u.SetM("M2510", false);
                                GlobalVars.Fx5u.SetM("M2511", false);
                                GlobalVars.Scan1.GetBarCode((string barcode) =>
                                {
                                    AddMessage("测试机1轨道扫码:" + barcode);
                                    if (barcode != "Error")
                                    {
                                        Mysql mysql = new Mysql();
                                        if (mysql.Connect())
                                        {
                                            string stm = "SELECT * FROM BODMSG WHERE SCBODBAR = '" + barcode + "' ORDER BY SIDATE DESC LIMIT 0,5";
                                            DataSet ds = mysql.Select(stm);
                                            DataTable dt = ds.Tables["table0"];
                                            if (dt.Rows.Count > 0)
                                            {
                                                if (dt.Rows[0]["STATUS"] == DBNull.Value)
                                                {
                                                    stm = "INSERT INTO BODMSG (SCBODBAR, STATUS) VALUES('" + barcode + "','ON')";
                                                    mysql.executeQuery(stm);
                                                    epsonRC90.BordBarcode = barcode;
                                                    AddMessage("板 " + barcode + " 绑定");
                                                    GlobalVars.Fx5u.SetM("M2511", true);
                                                }
                                                else
                                                {
                                                    if ((string)dt.Rows[0]["STATUS"] == "OFF")
                                                    {
                                                        stm = "INSERT INTO BODMSG (SCBODBAR, STATUS) VALUES('" + barcode + "','ON')";
                                                        mysql.executeQuery(stm);
                                                        epsonRC90.BordBarcode = barcode;
                                                        AddMessage("板 " + barcode + " 绑定");
                                                        GlobalVars.Fx5u.SetM("M2511", true);
                                                    }
                                                    else
                                                    {
                                                        AddMessage("板 " + barcode + " 已测过");
                                                        GlobalVars.Fx5u.SetM("M2510", true);
                                                    }
                                                }

                                            }
                                            else
                                            {
                                                stm = "INSERT INTO BODMSG (SCBODBAR, STATUS) VALUES('" + barcode + "','ON')";
                                                mysql.executeQuery(stm);
                                                epsonRC90.BordBarcode = barcode;
                                                AddMessage("板 " + barcode + " 绑定");
                                                GlobalVars.Fx5u.SetM("M2511", true);
                                            }
                                            GlobalVars.Fx5u.SetM("M2500", true);
                                        }
                                        else
                                        {
                                            AddMessage("Mysql数据库查询失败");
                                            GlobalVars.Fx5u.SetM("M2501", true);
                                        }
                                        mysql.DisConnect();
                                        
                                    }
                                    else
                                    {
                                        GlobalVars.Fx5u.SetM("M2501", true);
                                    }
                                });
                            }
                        }
                        if (m2004 != M2000[4])
                        {
                            m2004 = M2000[4];
                            if (m2004)
                            {
                                GlobalVars.Fx5u.SetM("M2004", false);
                                if (epsonRC90.TestSendStatus)
                                {
                                    await epsonRC90.TestSentNet.SendAsync("RobotCanGet");
                                }
                            }
                        }
                    }
                }
                catch
                { }
                #endregion
                SWms = sw.ElapsedMilliseconds;
            }
        }
        void IORun()
        {
            while (true)
            {
                System.Threading.Thread.Sleep(10);
                foreach (int[] item in ExIoIn)//雷赛卡的输入
                {
                    switch (item[1])
                    {
                        case 0:
                            epsonRC90.Rc90In[item[0]] = ls.Input[item[2]];
                            break;
                        case 1:
                            epsonRC90.Rc90In[item[0]] = ls.Input[item[2] + 32];
                            break;
                        case 2:
                            epsonRC90.Rc90In[item[0]] = ls.Input[item[2] + 64];
                            break;
                        default:
                            break;
                    }

                }
                foreach (int[] item in ExIoOut)
                {
                    switch (item[1])
                    {
                        case 0:
                            ls.Output[item[2]] = epsonRC90.Rc90Out[item[0]];
                            break;
                        case 1:
                            ls.Output[item[2] + 32] = epsonRC90.Rc90Out[item[0]];
                            break;
                        case 2:
                            ls.Output[item[2] + 64] = epsonRC90.Rc90Out[item[0]];
                            break;
                        default:
                            break;
                    }
                }
            }
        }
        #endregion
        #region 事件函数
        private void ModelPrintEventProcess(string str)
        {
            AddMessage(str);
        }
        private void EpsonStatusUpdateProcess(string str)
        {
            EpsonStatusAuto = str[2] == '1';
            EpsonStatusWarning = str[3] == '1';
            EpsonStatusSError = str[4] == '1';
            EpsonStatusSafeGuard = str[5] == '1';
            EpsonStatusEStop = str[6] == '1';
            EpsonStatusError = str[7] == '1';
            EpsonStatusPaused = str[8] == '1';
            EpsonStatusRunning = str[9] == '1';
            EpsonStatusReady = str[10] == '1';
        }
        #endregion

        private void HomePageSelect(object sender, RoutedEventArgs e)
        {
            HomePage.Visibility = Visibility.Visible;
            ParameterPage.Visibility = Visibility.Collapsed;
            CameraPage.Visibility = Visibility.Collapsed;
        }

        private void ParameterPageSelect(object sender, RoutedEventArgs e)
        {
            HomePage.Visibility = Visibility.Collapsed;
            ParameterPage.Visibility = Visibility.Visible;
            CameraPage.Visibility = Visibility.Collapsed;
        }
        private void CameraPageSelect(object sender, RoutedEventArgs e)
        {
            HomePage.Visibility = Visibility.Collapsed;
            ParameterPage.Visibility = Visibility.Collapsed;
            CameraPage.Visibility = Visibility.Visible;
        }

        private void SaveParameterButtonClick(object sender, RoutedEventArgs e)
        {

        }

        private void MsgTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            MsgTextBox.ScrollToEnd();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
        }

        private async void EpsonStartButtonClick(object sender, RoutedEventArgs e)
        {
            if (epsonRC90.CtrlStatus && EpsonStatusReady && !EpsonStatusEStop)
            {
                await epsonRC90.CtrlNet.SendAsync("$start,0");
            }
        }

        private async void EpsonPauseButtonClick(object sender, RoutedEventArgs e)
        {
            if (epsonRC90.CtrlStatus)
            {
                await epsonRC90.CtrlNet.SendAsync("$pause");
            }
        }

        private async void EpsonContinueButtonClick(object sender, RoutedEventArgs e)
        {
            if (epsonRC90.CtrlStatus)
            {
                await epsonRC90.CtrlNet.SendAsync("$continue");
            }
        }

        private async void EpsonReStartButtonClick(object sender, RoutedEventArgs e)
        {
            bool r = MessageBox.Show("确定重启机械手?", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
            if (epsonRC90.CtrlStatus && r)
            {
                await epsonRC90.CtrlNet.SendAsync("$stop");
                await Task.Delay(300);
                await epsonRC90.CtrlNet.SendAsync("$SetMotorOff,1");
                await Task.Delay(400);
                await epsonRC90.CtrlNet.SendAsync("$reset");
            }
        }

        private void FuncButtonClick(object sender, RoutedEventArgs e)
        {
            //epsonRC90.BottomScanGetBarCodeCallback("G5Y936600AZP2CQ1S");
            //try
            //{
            //    GlobalVars.GetImage();
            //    GlobalVars.GetBarcode();
            //}
            //catch (Exception ex)
            //{
            //    AddMessage(ex.Message);
            //}
            //GlobalVars.Scan1.GetBarCode((string str) => { AddMessage(str); });
            ////GlobalVars.Fx5u.WriteMultW("D1000", new short[3] { 1, 2, 3 });
            //var A = GlobalVars.Fx5u.ReadMultiW("D1000",2);
            //AddMessage(A[0].ToString() + " " + A[1].ToString());
            ////GlobalVars.Fx5u.WriteD("D1000",256);
        }

        private void GrapButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GlobalVars.GetBarcode();
            }
            catch (Exception ex)
            {
                AddMessage(ex.Message);
            }

        }

        private void DrawRec1_Click(object sender, RoutedEventArgs e)
        {
            HTuple row1, column1, row2, column2;
            GlobalVars.viewController1.viewPort.HalconWindow.SetColor("red");
            HOperatorSet.DrawRectangle1(GlobalVars.viewController1.viewPort.HalconWindow,out row1,out column1,out row2,out column2);
            HObject rectangle;
            HOperatorSet.GenRectangle1(out rectangle, row1, column1, row2, column2);
            GlobalVars.viewController1.addIconicVar(rectangle);
            GlobalVars.viewController1.repaint();
            rectangle.WriteObject(System.Environment.CurrentDirectory + "\\rectangle1.hobj");
        }

        private void DrawRec2_Click(object sender, RoutedEventArgs e)
        {
            HTuple row1, column1, row2, column2;
            GlobalVars.viewController1.viewPort.HalconWindow.SetColor("red");
            HOperatorSet.DrawRectangle1(GlobalVars.viewController1.viewPort.HalconWindow, out row1, out column1, out row2, out column2);
            HObject rectangle;
            HOperatorSet.GenRectangle1(out rectangle, row1, column1, row2, column2);
            GlobalVars.viewController1.addIconicVar(rectangle);
            GlobalVars.viewController1.repaint();
            rectangle.WriteObject(System.Environment.CurrentDirectory + "\\rectangle2.hobj");
        }

        private void GrapImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GlobalVars.GetImage();
            }
            catch (Exception ex)
            {
                AddMessage(ex.Message);
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
            await Task.Delay(1);
            CameraPage.Visibility = Visibility.Visible;
            LoadedCallback();
            CameraPage.Visibility = Visibility.Collapsed;
        }
    }
}
