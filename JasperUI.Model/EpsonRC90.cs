﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BingLibrary.hjb.net;
using BingLibrary.hjb.file;
using BingLibrary.hjb.tools;
using BingLibrary.Net.net;
using System.Data;
using ViewROI;
using BingLibrary.HVision;
using BingLibrary.hjb;
using System.IO;
using System.Diagnostics;

namespace JasperUI.Model
{
    public class EpsonRC90
    {
        #region 变量
        public TcpIpClient CtrlNet = new TcpIpClient();
        public TcpIpClient IOReceiveNet = new TcpIpClient();
        public TcpIpClient TestSentNet = new TcpIpClient();
        public TcpIpClient TestReceiveNet = new TcpIpClient();
        public UDPClient udp = new UDPClient();
        string Ip = "192.168.1.2";
        private bool isLogined = false;
        public bool[] Rc90In = new bool[100];
        public bool[] Rc90Out = new bool[100];
        public bool CtrlStatus = false, IOReceiveStatus = false, TestSendStatus = false, TestReceiveStatus = false;
        private string iniParameterPath = System.Environment.CurrentDirectory + "\\Parameter.ini";
        
        public int[,] BordIndexA = new int[12, 8];
        public int[,] BordIndexB = new int[12, 8];
        public int BordSW = 0;
        //条码 板条码 索引 产品状态 日期 时间
        public ProducInfo[] BarInfo = new ProducInfo[96];
        string[] SamBarcode = new string[8];
        public string BordBarcode = "Null";
        public string Name;
        public JasperTester jasperTester = new JasperTester();
        public List<string> SamOpenList, SamShortList;
        public static bool IsRetestMode { set; get; } = false;
        public bool AOISwitch = false;
        public int ABBSwitch = 1;
        #endregion
        #region 事件
        public delegate void PrintEventHandler(string ModelMessageStr);
        public event PrintEventHandler ModelPrint;
        public event PrintEventHandler EpsonStatusUpdate;
        #endregion
        #region 构造函数
        public EpsonRC90(string ip,string mcuip,int mcuport,string name)
        {
            Name = name;
            Ip = ip;
            string MCUIp = mcuip;
            int udpport = mcuport;
            udp.Connect(udpport, 13800, MCUIp);
            for (int i = 0; i < 96; i++)
            {
                BarInfo[i] = new ProducInfo();
                BarInfo[i].Barcode = "FAIL";
                BarInfo[i].BordBarcode = "Null";
                BarInfo[i].Status = 0;
                BarInfo[i].TDate = DateTime.Now.ToString("yyyyMMdd");
                BarInfo[i].TTime = DateTime.Now.ToString("HHmmss");
            }
            for (int i = 0; i < 8; i++)
            {
                //Inifile.INIWriteValue(iniParameterPath, "Summary", "Tester1TestCount" + (i + 1).ToString(), "0");
                try
                {
                    switch (Name)
                    {
                        case "第1台":
                            jasperTester.TestCount[i] = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "Summary", "Tester1TestCount" + (i + 1).ToString(), "0"));
                            jasperTester.PassCount[i] = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "Summary", "Tester1PassCount" + (i + 1).ToString(), "0"));
                            break;
                        case "第2台":
                            jasperTester.TestCount[i] = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "Summary", "Tester2TestCount" + (i + 1).ToString(), "0"));
                            jasperTester.PassCount[i] = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "Summary", "Tester2PassCount" + (i + 1).ToString(), "0"));
                            break;
                        default:
                            break;
                    }
                    if (jasperTester.TestCount[i] > 0)
                    {
                        jasperTester.Yield[i] = (double)jasperTester.PassCount[i] / (double)jasperTester.TestCount[i] * 100;
                    }
                    else
                    {
                        jasperTester.Yield[i] = 0;
                    }
                }
                catch
                { }

            }
            Async.RunFuncAsync(Run, null);
        }
        #endregion
        #region 功能
        void Run()
        {
            checkCtrlNet();
            GetStatus();
            checkIOReceiveNet();
            IORevAnalysis();
            checkTestSentNet();
            checkTestReceiveNet();
            TestRevAnalysis();
        }
        public async void checkCtrlNet()
        {
            while (true)
            {

                if (!CtrlNet.tcpConnected)
                {
                    await Task.Delay(1000);
                    if (!CtrlNet.tcpConnected)
                    {
                        isLogined = false;
                        bool r1 = await CtrlNet.Connect(Ip, 5000);
                        if (r1)
                        {
                            CtrlStatus = true;
                            ModelPrint(Name + "机械手CtrlNet连接");
                        }
                        else
                            CtrlStatus = false;
                    }
                }
                if (!isLogined && CtrlStatus)
                {
                    await CtrlNet.SendAsync("$login,123");
                    string s = await CtrlNet.ReceiveAsync();
                    if (s.Contains("#login,0"))
                        isLogined = true;
                    await Task.Delay(400);
                }
                else
                {
                    await Task.Delay(3000);
                }
            }
        }
        private async void GetStatus()
        {
            string status = "";
            while (true)
            {
                if (isLogined == true)
                {
                    try
                    {
                        status = await CtrlNet.SendAndReceive("$getstatus");
                        string[] statuss = status.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                        if (statuss[0] == "#getstatus")
                        {
                            if (statuss[1].Length == 11)
                            {
                                EpsonStatusUpdate(statuss[1]);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //Log.Default.Error("EpsonRC90.GetStatus", ex.Message);
                    }
                }
                await Task.Delay(1000);
            }
        }
        public async void checkIOReceiveNet()
        {
            while (true)
            {
                await Task.Delay(400);
                if (!IOReceiveNet.tcpConnected)
                {
                    await Task.Delay(1000);
                    if (!IOReceiveNet.tcpConnected)
                    {
                        bool r1 = await IOReceiveNet.Connect(Ip, 2007);
                        if (r1)
                        {
                            IOReceiveStatus = true;
                            ModelPrint(Name + "机械手IOReceiveNet连接");

                        }
                        else
                            IOReceiveStatus = false;
                    }
                }
                else
                { await Task.Delay(15000); }
            }
        }
        private async void IORevAnalysis()
        {
            while (true)
            {
                //await Task.Delay(100);
                if (IOReceiveStatus == true)
                {
                    string s = await IOReceiveNet.ReceiveAsync();

                    string[] ss = s.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    try
                    {
                        s = ss[0];

                    }
                    catch
                    {
                        s = "error";
                    }

                    if (s == "error")
                    {
                        IOReceiveNet.tcpConnected = false;
                        IOReceiveStatus = false;
                        ModelPrint(Name + "机械手IOReceiveNet断开");
                    }
                    else
                    {
                        string[] strs = s.Split(',');
                        if (strs[0] == "IOCMD" && strs[1].Length == 100)
                        {
                            for (int i = 0; i < 100; i++)
                            {
                                Rc90Out[i] = strs[1][i] == '1' ? true : false;
                            }
                            string RsedStr = "";
                            for (int i = 0; i < 100; i++)
                            {
                                RsedStr += Rc90In[i] ? "1" : "0";
                            }
                            await IOReceiveNet.SendAsync(RsedStr);
                            //ModelPrint("IOSend " + RsedStr);
                            //await Task.Delay(1);
                        }
                        //ModelPrint("IORev: " + s);
                    }
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }
        public async void checkTestSentNet()
        {
            while (true)
            {
                await Task.Delay(400);
                if (!TestSentNet.tcpConnected)
                {
                    await Task.Delay(1000);
                    if (!TestSentNet.tcpConnected)
                    {
                        bool r1 = await TestSentNet.Connect(Ip, 2000);
                        if (r1)
                        {
                            TestSendStatus = true;
                            ModelPrint(Name + "机械手TestSentNet连接");

                        }
                        else
                            TestSendStatus = false;
                    }
                }
                else
                { await Task.Delay(15000);
                    TestSentNet.IsOnline();
                    if (!TestSentNet.tcpConnected)
                        ModelPrint(Name + "机械手TestSentNet断开");
                }
            }
        }
        public async void checkTestReceiveNet()
        {
            while (true)
            {
                await Task.Delay(400);
                if (!TestReceiveNet.tcpConnected)
                {
                    await Task.Delay(1000);
                    if (!TestReceiveNet.tcpConnected)
                    {
                        bool r1 = await TestReceiveNet.Connect(Ip, 2001);
                        if (r1)
                        {
                            TestReceiveStatus = true;
                            ModelPrint(Name + "机械手TestReceiveNet连接");

                        }
                        else
                            TestReceiveStatus = false;
                    }
                }
                else
                { await Task.Delay(15000); }
            }
        }
        private async void TestRevAnalysis()
        {
            while (true)
            {
                if (TestReceiveStatus == true)
                {
                    string s = await TestReceiveNet.ReceiveAsync();

                    string[] ss = s.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    try
                    {
                        s = ss[0];
                    }
                    catch
                    {
                        s = "error";
                    }

                    if (s == "error")
                    {
                        TestReceiveNet.tcpConnected = false;
                        TestReceiveStatus = false;
                        ModelPrint(Name + "机械手TestReceiveNet断开");
                    }
                    else
                    {
                        ModelPrint(Name + "TestRev: " + s);
                        try
                        {
                            string[] strs = s.Split(';');
                            switch (strs[0])
                            {
                                case "ScanBarcode":
                                    String[] Barcodes = new string[2];
                                    switch (Name)
                                    {
                                        case "第1台":
                                            GlobalVars.GetImage();
                                            Barcodes = GlobalVars.GetBarcode();
                                            break;
                                        case "第2台":
                                            GlobalVars.GetImage2();
                                            Barcodes = GlobalVars.GetBarcode2();
                                            break;
                                        default:
                                            break;
                                    }

                                    if (strs.Length > 1)
                                    {
                                        BottomScanGetBarCodeCallback(Barcodes, strs);
                                    }
                                    else
                                    {
                                        string barcode;
                                        if (Barcodes[0] == "error")
                                        {
                                            barcode = Barcodes[1];
                                        }
                                        else
                                        {
                                            barcode = Barcodes[0];
                                        }
                                        BottomScanGetBarCodeCallback(barcode);
                                    }

                                    break;
                                case "TestStart":
                                    SendBarcode(int.Parse(strs[1]));
                                    break;
                                case "TestResult":
                                    if (strs.Length == 10)
                                    {
                                        SaveResult(strs);
                                    }
                                    break;
                                case "RobotGetFinish":
                                    switch (Name)
                                    {
                                        case "第1台":
                                            GlobalVars.Fx5u.SetM("M2508", true);
                                            break;
                                        case "第2台":
                                            GlobalVars.Fx5u.SetM("M2509", true);
                                            break;
                                        default:
                                            break;
                                    }
                                    
                                    break;
                                case "ScanBarcodeSample":
                                    Barcodes = new string[2];
                                    switch (Name)
                                    {
                                        case "第1台":
                                            GlobalVars.GetImage();
                                            Barcodes = GlobalVars.GetBarcode();
                                            break;
                                        case "第2台":
                                            GlobalVars.GetImage2();
                                            Barcodes = GlobalVars.GetBarcode2();
                                            break;
                                        default:
                                            break;
                                    }
                                    int index1 = int.Parse(strs[2]);
                                    SamBarcode[index1] = Barcodes[0] == "error" ? "FAIL" : Barcodes[0];
                                    SamBarcode[index1 + 4] = Barcodes[1] == "error" ? "FAIL" : Barcodes[1];
                                    await TestSentNet.SendAsync("BarcodeInfo2;OK");
                                    ModelPrint(Name + "BarcodeInfo2;OK");
                                    break;
                                case "TestStartSample":
                                    SendSamBarcode();
                                    break;
                                case "CheckSample":
                                    CheckSam();
                                    break;
                                case "AskSample":
                                    break;
                                case "CheckClean":
                                    break;
                                default:
                                    ModelPrint(Name + "无效指令： " + s);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            ModelPrint(Name + ex.Message);
                        }
                    }
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }
        public async void BottomScanGetBarCodeCallback(string barcode)
        {
            //barcode = "G5Y936600AZP2CQ1S";
            ModelPrint(Name + barcode);
            await Task.Run(async () =>
            {
                try
                {
                    Oracle oraDB = new Oracle("zdtbind", "sfcabar", "sfcabar*168");
                    if (oraDB.isConnect())
                    {
                        string sqlstr = "select * from sfcdata.barautbind where SCBARCODE = '" + barcode + "'";
                        DataSet ds = oraDB.executeQuery(sqlstr);
                        DataTable dt = ds.Tables[0];
                        if (dt.Rows.Count > 0)
                        {
                            int nowindex = int.Parse((string)dt.Rows[0]["PCSSER"]);

                            for (int i = 0; i < 96; i++)
                            {
                                if (BordIndexA[i / 8, i % 8] == nowindex)
                                {
                                    BordSW = 0;
                                    break;
                                }
                                if (BordIndexB[i / 8, i % 8] == nowindex)
                                {
                                    BordSW = 1;
                                    break;
                                }
                            }

                            string PNLBarcode = (string)dt.Rows[0]["SCPNLBAR"];
                            sqlstr = "select * from sfcdata.barautbind where SCPNLBAR = '" + PNLBarcode + "'";
                            ds = oraDB.executeQuery(sqlstr);
                            dt = ds.Tables[0];
                            if (dt.Rows.Count > 0)
                            {
                                for (int i = 0; i < 96; i++)
                                {
                                    int pcser;
                                    if (BordSW == 0)
                                    {
                                        pcser = BordIndexA[i / 8, i % 8];
                                    }
                                    else
                                    {
                                        pcser = BordIndexB[i / 8, i % 8];
                                    }


                                    try
                                    {
                                        DataRow[] drs = dt.Select(string.Format("PCSSER = '{0}'", pcser.ToString()));
                                        if (drs.Length > 0)
                                        {
                                            DataRow dr = drs[0];
                                            Oracle oraDB_1 = new Oracle("zdtdb", "ictdata", "ictdata*168");
                                            if (oraDB_1.isConnect())
                                            {
                                                string sqlstr_1 = "select * from TED_FCT_DATA where barcode = '" + (string)dr["SCBARCODE"] + "'";
                                                DataSet ds_1 = oraDB_1.executeQuery(sqlstr_1);
                                                DataTable dt_1 = ds_1.Tables[0];
                                                bool abbresult = true;
                                                switch (ABBSwitch)
                                                {
                                                    case 1:
                                                        abbresult = dt_1.Rows.Count <= 0;//InLine模式，要没有记录
                                                        break;
                                                    case 2:
                                                    case 3:
                                                        abbresult = dt_1.Rows.Count > 0;//复测、重工模式，要有记录
                                                        break;
                                                    default:
                                                        break;
                                                }

                                                if (abbresult)
                                                {
                                                    

                                                    bool isAoi = false;
                                                    if (AOISwitch)
                                                    {
                                                        sqlstr = "select to_char(sfcdata.GETCK_posaoi_t1('" + (string)dr["SCBARCODE"] + "', 'A')) from dual";
                                                        ds = oraDB.executeQuery(sqlstr);
                                                        DataTable dt1 = ds.Tables[0];
                                                        if ((string)dt1.Rows[0][0] == "0")
                                                        {
                                                            isAoi = true;
                                                        }
                                                        else
                                                        {
                                                            //sqlstr = "select to_char(sfcdata.GETCK_posaoi_t1('" + (string)dr["SCBARCODE"] + "', 'B')) from dual";
                                                            //ds = oraDB.executeQuery(sqlstr);
                                                            //dt1 = ds.Tables[0];
                                                            //if ((string)dt1.Rows[0][0] == "0")
                                                            //{
                                                            //    isAoi = true;
                                                            //}
                                                        }
                                                    }

                                                    //条码 板条码 产品状态 日期 时间
                                                    BarInfo[i].Barcode = isAoi ? "FAIL" : (string)dr["SCBARCODE"];
                                                    BarInfo[i].BordBarcode = BordBarcode;
                                                    BarInfo[i].Status = isAoi ? 1 : 0;
                                                    BarInfo[i].TDate = DateTime.Now.ToString("yyyyMMdd");
                                                    BarInfo[i].TTime = DateTime.Now.ToString("HHmmss");
                                                }
                                                else
                                                {
                                                    switch (ABBSwitch)
                                                    {
                                                        case 1:
                                                            ModelPrint(Name + "InLine模式，条码 " + (string)dr["SCBARCODE"] + "记录 " + dt_1.Rows.Count.ToString() + "> 0");
                                                            break;
                                                        case 2:
                                                            ModelPrint(Name + "复测模式，条码 " + (string)dr["SCBARCODE"] + "记录 " + dt_1.Rows.Count.ToString() + "= 0");
                                                            break;
                                                        case 3:
                                                            ModelPrint(Name + "重工模式，条码 " + (string)dr["SCBARCODE"] + "记录 " + dt_1.Rows.Count.ToString() + "= 0");
                                                            break;
                                                        default:
                                                            break;
                                                    }

                                                    BarInfo[i].Barcode = "FAIL";
                                                    BarInfo[i].BordBarcode = BordBarcode;
                                                    BarInfo[i].Status = 6;
                                                    BarInfo[i].TDate = DateTime.Now.ToString("yyyyMMdd");
                                                    BarInfo[i].TTime = DateTime.Now.ToString("HHmmss");
                                                }
                                            }
                                            else
                                            {
                                                BarInfo[i].Barcode = "FAIL";
                                                BarInfo[i].BordBarcode = BordBarcode;
                                                BarInfo[i].Status = 2;
                                                BarInfo[i].TDate = DateTime.Now.ToString("yyyyMMdd");
                                                BarInfo[i].TTime = DateTime.Now.ToString("HHmmss");
                                            }
                                            oraDB_1.disconnect();

                                            
                                        }
                                        else
                                        {
                                            BarInfo[i].Barcode = "FAIL";
                                            BarInfo[i].BordBarcode = BordBarcode;
                                            BarInfo[i].Status = 2;
                                            BarInfo[i].TDate = DateTime.Now.ToString("yyyyMMdd");
                                            BarInfo[i].TTime = DateTime.Now.ToString("HHmmss");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        BarInfo[i].Barcode = "FAIL";
                                        BarInfo[i].BordBarcode = BordBarcode;
                                        BarInfo[i].Status = 2;
                                        BarInfo[i].TDate = DateTime.Now.ToString("yyyyMMdd");
                                        BarInfo[i].TTime = DateTime.Now.ToString("HHmmss");
                                    }
                                }
                                string retstr = "";
                                string mid = "";
                                switch (Name)
                                {
                                    case "第1台":
                                        mid = "MachineID";
                                        break;
                                    case "第2台":
                                        mid = "MachineID2";
                                        break;
                                    default:
                                        break;
                                }
                                string machinestr = Inifile.INIGetStringValue(iniParameterPath, "System", mid, "Jasper01");
                                Mysql mysql = new Mysql();
                                if (mysql.Connect())
                                {
                                    for (int i = 0; i < 96; i++)
                                    {
                                        string stm = "INSERT INTO BARBIND (MACHINE,SCBARCODE,SCPNLBAR,SCBODBAR,SDATE,STIME,PCSSER,RESULT) VALUES ('" + machinestr + "','" + BarInfo[i].Barcode + "','"
                                        + PNLBarcode + "','" + BordBarcode + "','" + BarInfo[i].TDate + "','" + BarInfo[i].TTime + "','" + (i + 1).ToString() + "','" + BarInfo[i].Status.ToString() + "')";
                                        mysql.executeQuery(stm);
                                    }                                    
                                }
                                mysql.DisConnect();
                                for (int i = 0; i < 96; i++)
                                {
                                    retstr += BarInfo[i].Status.ToString() + ";";
                                }
                                retstr = retstr.Substring(0, retstr.Length - 1);

                                await TestSentNet.SendAsync("BarcodeInfo;OK;" + retstr);
                                ModelPrint(Name + "BarcodeInfo;OK;" + retstr);
                            }
                            else
                            {
                                ModelPrint(Name + "条码" + PNLBarcode + "查询失败");
                                if (TestSendStatus)
                                {
                                    await TestSentNet.SendAsync("BarcodeInfo;NG");
                                }
                            }
                        }
                        else
                        {
                            ModelPrint(Name + "条码" + barcode + "查询失败");
                            if (TestSendStatus)
                            {
                                await TestSentNet.SendAsync("BarcodeInfo;NG");
                            }
                        }
                    }
                    else
                    {
                        ModelPrint(Name + "数据库未连接");
                        if (TestSendStatus)
                        {
                            await TestSentNet.SendAsync("BarcodeInfo;NG");
                        }
                    }
                    oraDB.disconnect();
                }
                catch (Exception ex)
                {
                    ModelPrint(Name + ex.Message);
                    if (TestSendStatus)
                    {
                        await TestSentNet.SendAsync("BarcodeInfo;NG");
                    }
                }
            });

        }
        public async void BottomScanGetBarCodeCallback(string[] barcode,string[] mes)
        {
            ModelPrint(Name + barcode[0] + "," + barcode[1]);
            await Task.Run(async()=> {
                int index = int.Parse(mes[1]);
                int index1 = int.Parse(mes[2]);
                int[] barindex = new int[2];
                barindex[0] = index1 + index * 8;
                barindex[1] = index1 + 4 + index * 8;
                try
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    for (int i = 0; i < 2; i++)
                    {
                        if (barcode[i] != "error")
                        {
                            Oracle oraDB_1 = new Oracle("zdtdb", "ictdata", "ictdata*168");
                            if (oraDB_1.isConnect())
                            {

                                string sqlstr_1 = "select * from TED_FCT_DATA where barcode = '" + barcode[i] + "'";
                                DataSet ds_1 = oraDB_1.executeQuery(sqlstr_1);
                                DataTable dt_1 = ds_1.Tables[0];
                                bool abbresult = true;
                                switch (ABBSwitch)
                                {
                                    case 1:
                                        abbresult = dt_1.Rows.Count <= 0;//InLine模式，要没有记录
                                        break;
                                    case 2:
                                    case 3:
                                        abbresult = dt_1.Rows.Count > 0;//复测、重工模式，要有记录
                                        break;
                                    default:                                        
                                        break;
                                }

                                if (abbresult)
                                {
                                    Oracle oraDB = new Oracle("zdtbind", "sfcabar", "sfcabar*168");
                                    if (oraDB.isConnect())
                                    {
                                        string sqlstr = "select * from sfcdata.barautbind where SCBARCODE = '" + barcode[i] + "'";
                                        DataSet ds = oraDB.executeQuery(sqlstr);


                                        DataTable dt = ds.Tables[0];
                                        if (dt.Rows.Count > 0)
                                        {




                                            bool isAoi = false;
                                            if (AOISwitch)
                                            {
                                                sqlstr = "select to_char(sfcdata.GETCK_posaoi_t1('" + barcode[i] + "', 'A')) from dual";
                                                ds = oraDB.executeQuery(sqlstr);



                                                DataTable dt1 = ds.Tables[0];
                                                if ((string)dt1.Rows[0][0] == "0")
                                                {
                                                    isAoi = true;
                                                }
                                            }

                                            //条码 板条码 产品状态 日期 时间
                                            BarInfo[barindex[i]].Barcode = isAoi ? "FAIL" : barcode[i];
                                            BarInfo[barindex[i]].BordBarcode = BordBarcode;
                                            BarInfo[barindex[i]].Status = isAoi ? 1 : 0;
                                            BarInfo[barindex[i]].TDate = DateTime.Now.ToString("yyyyMMdd");
                                            BarInfo[barindex[i]].TTime = DateTime.Now.ToString("HHmmss");

                                        }
                                        else
                                        {
                                            ModelPrint(Name + "未查询到条码" + barcode[i] + "信息");
                                            BarInfo[barindex[i]].Barcode = "FAIL";
                                            BarInfo[barindex[i]].BordBarcode = BordBarcode;
                                            BarInfo[barindex[i]].Status = 2;
                                            BarInfo[barindex[i]].TDate = DateTime.Now.ToString("yyyyMMdd");
                                            BarInfo[barindex[i]].TTime = DateTime.Now.ToString("HHmmss");
                                        }

                                    }
                                    else
                                    {
                                        ModelPrint(Name + "数据库连接失败");
                                        BarInfo[barindex[i]].Barcode = "FAIL";
                                        BarInfo[barindex[i]].BordBarcode = BordBarcode;
                                        BarInfo[barindex[i]].Status = 2;
                                        BarInfo[barindex[i]].TDate = DateTime.Now.ToString("yyyyMMdd");
                                        BarInfo[barindex[i]].TTime = DateTime.Now.ToString("HHmmss");
                                    }
                                    oraDB.disconnect();
                                }
                                else
                                {
                                    switch (ABBSwitch)
                                    {
                                        case 1:
                                            ModelPrint(Name + "InLine模式，条码 " + barcode[i] + "记录 " + dt_1.Rows.Count.ToString() + "> 0");
                                            break;
                                        case 2:
                                            ModelPrint(Name + "复测模式，条码 " + barcode[i] + "记录 " + dt_1.Rows.Count.ToString() + "= 0");
                                            break;
                                        case 3:
                                            ModelPrint(Name + "重工模式，条码 " + barcode[i] + "记录 " + dt_1.Rows.Count.ToString() + "= 0");
                                            break;
                                        default:
                                            break;
                                    }
                                    
                                    BarInfo[barindex[i]].Barcode = "FAIL";
                                    BarInfo[barindex[i]].BordBarcode = BordBarcode;
                                    BarInfo[barindex[i]].Status = 6;
                                    BarInfo[barindex[i]].TDate = DateTime.Now.ToString("yyyyMMdd");
                                    BarInfo[barindex[i]].TTime = DateTime.Now.ToString("HHmmss");
                                }

                                
                            }
                            else
                            {
                                ModelPrint(Name + "数据库连接失败");
                                BarInfo[barindex[i]].Barcode = "FAIL";
                                BarInfo[barindex[i]].BordBarcode = BordBarcode;
                                BarInfo[barindex[i]].Status = 2;
                                BarInfo[barindex[i]].TDate = DateTime.Now.ToString("yyyyMMdd");
                                BarInfo[barindex[i]].TTime = DateTime.Now.ToString("HHmmss");
                            }
                            oraDB_1.disconnect();
                        }
                        else
                        {
                            BarInfo[barindex[i]].Barcode = "FAIL";
                            BarInfo[barindex[i]].BordBarcode = BordBarcode;
                            BarInfo[barindex[i]].Status = 2;
                            BarInfo[barindex[i]].TDate = DateTime.Now.ToString("yyyyMMdd");
                            BarInfo[barindex[i]].TTime = DateTime.Now.ToString("HHmmss");
                        }
                        string mid = "";
                        switch (Name)
                        {
                            case "第1台":
                                mid = "MachineID";
                                break;
                            case "第2台":
                                mid = "MachineID2";
                                break;
                            default:
                                break;
                        }
                        string machinestr = Inifile.INIGetStringValue(iniParameterPath, "System", mid, "Jasper01");

                        //Console.WriteLine("1. " + sw.ElapsedMilliseconds.ToString() + "ms");

                        Mysql mysql = new Mysql();
                        if (mysql.Connect())
                        {
                            string stm = "INSERT INTO BARBIND (MACHINE,SCBARCODE,SCBODBAR,SDATE,STIME,PCSSER,RESULT) VALUES ('" + machinestr + "','" + BarInfo[barindex[i]].Barcode + "','"
                            + BordBarcode + "','" + BarInfo[barindex[i]].TDate + "','" + BarInfo[barindex[i]].TTime + "','" + (barindex[i] + 1).ToString() + "','" + BarInfo[barindex[i]].Status.ToString() + "')";

                            //Console.WriteLine("2. " + sw.ElapsedMilliseconds.ToString() + "ms");

                            mysql.executeQuery(stm);

                            //Console.WriteLine("3. " + sw.ElapsedMilliseconds.ToString() + "ms");

                        }
                        mysql.DisConnect();
                    }
                    string retstr = mes[2] + ";" + BarInfo[barindex[0]].Status.ToString() + ";" + BarInfo[barindex[1]].Status.ToString();
                    await TestSentNet.SendAsync("BarcodeInfo1;" + retstr);
                    ModelPrint(Name + "BarcodeInfo1;" + retstr + " " + sw.ElapsedMilliseconds.ToString() + "ms");
                    Console.WriteLine(Name + "BarcodeInfo1;" + retstr + " " + sw.ElapsedMilliseconds.ToString() + "ms");
                    sw.Stop();                    
                }
                catch (Exception ex)
                {
                    ModelPrint(Name + ex.Message);

                }
                
            });
        }
        async void SendBarcode(int index)
        {
            string str = "";
            for (int i = 0; i < 8; i++)
            {
                str += BarInfo[i + index * 8].Barcode + "|";
            }
            str += IsRetestMode ? "9" : "0";
            ModelPrint(Name + str);
            bool r = await udp.SendAsync(str);
            for (int i = 0; i < 8; i++)
            {
                jasperTester.Result[i] = "N";
            }
        }
        async void SendSamBarcode()
        {
            string str = "";
            for (int i = 0; i < 8; i++)
            {
                str += SamBarcode[i] + "|";
            }
            str += "3";
            ModelPrint(Name + str);
            bool r = await udp.SendAsync(str);
        }
        async void SaveResult(string[] rststr)
        {
            int index = int.Parse(rststr[1]);
            bool rst = true;
            await Task.Run(() =>
            {               
                Mysql mysql = new Mysql();
                if (mysql.Connect())
                {
                   
                    for (int i = 0; i < 8; i++)
                    {
                        string resultstr = BarInfo[index * 8 + i].Status == 6 ? "6" : rststr[2 + i];
                        string stm = "UPDATE BARBIND SET RESULT = '" + resultstr + "' WHERE SCBARCODE = '" + BarInfo[index * 8 + i].Barcode + "' AND SCBODBAR = '" + BarInfo[index * 8 + i].BordBarcode
                        + "' AND SDATE = '" + BarInfo[index * 8 + i].TDate + "' AND STIME = '" + BarInfo[index * 8 + i].TTime + "' AND PCSSER = '" + (index * 8 + i + 1).ToString() + "'";
                        int aa = mysql.executeQuery(stm);
                        if (aa < 1)
                        {
                            rst = false;
                        }
                    }

                    mysql.DisConnect();
                }
            });
            await TestSentNet.SendAsync("TestResult;" + (rst ? "OK" : "NG"));
            if (!Directory.Exists("D:\\生产记录\\" + Name + "\\" + DateTime.Now.ToString("yyyyMMdd")))
            {
                Directory.CreateDirectory("D:\\生产记录\\" + Name + "\\" + DateTime.Now.ToString("yyyyMMdd"));
            }
            string path = "D:\\生产记录\\" + Name + "\\" + DateTime.Now.ToString("yyyyMMdd") + "\\" + DateTime.Now.ToString("yyyyMMdd") + "生产记录.csv";
            
            for (int i = 0; i < 8; i++)
            {
                switch (rststr[2 + i])
                {
                    case "3":
                        jasperTester.Result[i] = "F";
                        break;
                    case "4":
                        jasperTester.Result[i] = "P";
                        break;
                    default:
                        jasperTester.Result[i] = "N";
                        break;
                }
                //jasperTester.Result[i] = rststr[2 + i] == "4" ? "P" : "F";
                if (rststr[2 + i] == "4" || rststr[2 + i] == "3")
                {
                    jasperTester.TestCount[i]++;
                }
                if (rststr[2 + i] == "4")
                {
                    jasperTester.PassCount[i]++;
                }
                if (jasperTester.TestCount[i] > 0)
                {
                    jasperTester.Yield[i] = (double)jasperTester.PassCount[i] / (double)jasperTester.TestCount[i] * 100;
                }
                else
                {
                    jasperTester.Yield[i] = 0;
                }

                switch (Name)
                {
                    case "第1台":
                        Inifile.INIWriteValue(iniParameterPath, "Summary", "Tester1TestCount" + (i + 1).ToString(), jasperTester.TestCount[i].ToString());
                        Inifile.INIWriteValue(iniParameterPath, "Summary", "Tester1PassCount" + (i + 1).ToString(), jasperTester.PassCount[i].ToString());
                        break;
                    case "第2台":
                        Inifile.INIWriteValue(iniParameterPath, "Summary", "Tester2TestCount" + (i + 1).ToString(), jasperTester.TestCount[i].ToString());
                        Inifile.INIWriteValue(iniParameterPath, "Summary", "Tester2PassCount" + (i + 1).ToString(), jasperTester.PassCount[i].ToString());
                        break;
                    default:
                        break;
                }
                Csvfile.savetocsv(path, new string[] { DateTime.Now.ToString(), BarInfo[index * 8 + i].Barcode, rststr[2 + i] == "4" ? "P" : "F" });
            }

        }
        public async void CheckSam()
        {
            string MachineID = "1";
            switch (Name)
            {
                case "第1台":
                    MachineID = Inifile.INIGetStringValue(iniParameterPath, "System", "MachineID", "1");
                    break;
                case "第2台":
                    MachineID = Inifile.INIGetStringValue(iniParameterPath, "System", "MachineID2", "1");
                    break;
                default:
                    break;
            }           
            await Task.Run(async() => {
                try
                {
                    Oracle oraDB = new Oracle("zdtdb", "ictdata", "ictdata*168");
                    string sqlstr = "select * from BARSAMREC where CDATE = '" + DateTime.Now.ToString("yyyyMMdd") + "' and CTIME > '" + DateTime.Now.AddHours(-2).ToString("HHmmss") + "' and SR01 in ('";
                    for (int i = 0; i < 8; i++)
                    {
                        sqlstr += MachineID + "_" + (i + 1).ToString() + "'";
                        if (i < 7)
                        {
                            sqlstr += ",'";
                        }
                        if (i == 7)
                        {
                            sqlstr += ") ";
                        }
                    }
                    DataSet ds = oraDB.executeQuery(sqlstr);
                    DataTable dt1 = ds.Tables[0];
                    string Columns = "";
                    for (int i = 0; i < dt1.Columns.Count - 1; i++)
                    {
                        Columns += dt1.Columns[i].ColumnName + ",";
                    }
                    Columns += dt1.Columns[dt1.Columns.Count - 1].ColumnName;
                    if (!Directory.Exists("D:\\样本测试\\" + DateTime.Now.ToString("yyyyMMdd")))
                    {
                        Directory.CreateDirectory("D:\\样本测试\\" + DateTime.Now.ToString("yyyyMMdd"));
                    }
                    Csvfile.dt2csv(dt1, "D:\\样本测试\\" + DateTime.Now.ToString("yyyyMMdd") + "\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + "Sample.csv", "Sample", Columns);
                    try
                    {
                        Process process1 = new Process();
                        process1.StartInfo.FileName = "D:\\样本测试\\" + DateTime.Now.ToString("yyyyMMdd") + "\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + "Sample.csv";
                        process1.StartInfo.Arguments = "";
                        process1.StartInfo.WindowStyle = ProcessWindowStyle.Maximized;
                        process1.Start();
                    }
                    catch (Exception ex)
                    {
                        ModelPrint(Name + ex.Message);
                    }
                    int[][] Result = new int[3][];
                    Result[0] = new int[] { 0, 0, 0, 0, 0, 0, 0, 0 };
                    Result[1] = new int[] { 0, 0, 0, 0, 0, 0, 0, 0 };
                    Result[2] = new int[] { 0, 0, 0, 0, 0, 0, 0, 0 };
                    int SamLimitCount = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "System", "SamLimitCount", "999"));
                    for (int j = 0; j < 8; j++)
                    {
                        DataRow[] dtr = dt1.Select(string.Format("TRES = '{0}' AND SR01 = '{1}'", "OK", MachineID + "_" + (j + 1).ToString()));
                        Result[0][j] = dtr.Length;
                        if (dtr.Length == 0)
                        {
                            ModelPrint(MachineID + "_" + (j + 1).ToString() + " " + "OK" + " 样本未测到");
                        }
                        else
                        {
                            for (int i = 0; i < dtr.Length; i++)
                            {
                                string barcode = (string)dtr[i]["BARCODE"];
                                string _sqlstr = "select * from BARSAMREC where BARCODE = '" + barcode + "'";
                                DataSet _ds = oraDB.executeQuery(sqlstr);
                                DataTable _dt1 = ds.Tables[0];
                                if (_dt1.Rows.Count > SamLimitCount)
                                {
                                    ModelPrint(MachineID + "_" + (j + 1).ToString() + " " + "OK" + " 样本 " + barcode + " 超过管控次数 " + _dt1.Rows.Count.ToString() + " > " + SamLimitCount.ToString());
                                    Result[0][j] = 0;
                                    break;
                                }
                            }
                        }
                    }
                    string NgItems = "";
                    foreach (string item in SamOpenList)
                    {
                        NgItems += "'" + item + "',";
                    }
                    NgItems = NgItems.Substring(0, NgItems.Length - 1);
                    for (int j = 0; j < 8; j++)
                    {
                        DataRow[] dtr = dt1.Select(string.Format("TRES IN ({0}) AND SR01 = '{1}'", NgItems, MachineID + "_" + (j + 1).ToString()));
                        Result[1][j] = dtr.Length;
                        if (dtr.Length == 0)
                        {
                            ModelPrint(MachineID + "_" + (j + 1).ToString() + " " + "OPEN" + " 样本未测到");
                        }
                        else
                        {
                            for (int i = 0; i < dtr.Length; i++)
                            {
                                string barcode = (string)dtr[i]["BARCODE"];
                                string _sqlstr = "select * from BARSAMREC where BARCODE = '" + barcode + "'";
                                DataSet _ds = oraDB.executeQuery(sqlstr);
                                DataTable _dt1 = ds.Tables[0];
                                if (_dt1.Rows.Count > SamLimitCount)
                                {
                                    ModelPrint(MachineID + "_" + (j + 1).ToString() + " " + "OPEN" + " 样本 " + barcode + " 超过管控次数 " + _dt1.Rows.Count.ToString() + " > " + SamLimitCount.ToString());
                                    Result[1][j] = 0;
                                    break;
                                }
                            }
                        }
                    }
                    NgItems = "";
                    foreach (string item in SamShortList)
                    {
                        NgItems += "'" + item + "',";
                    }
                    NgItems = NgItems.Substring(0, NgItems.Length - 1);
                    for (int j = 0; j < 8; j++)
                    {
                        DataRow[] dtr = dt1.Select(string.Format("TRES IN ({0}) AND SR01 = '{1}'", NgItems, MachineID + "_" + (j + 1).ToString()));
                        Result[2][j] = dtr.Length;
                        if (dtr.Length == 0)
                        {
                            ModelPrint(MachineID + "_" + (j + 1).ToString() + " " + "SHORT" + " 样本未测到");
                        }
                        else
                        {
                            for (int i = 0; i < dtr.Length; i++)
                            {
                                string barcode = (string)dtr[i]["BARCODE"];
                                string _sqlstr = "select * from BARSAMREC where BARCODE = '" + barcode + "'";
                                DataSet _ds = oraDB.executeQuery(sqlstr);
                                DataTable _dt1 = ds.Tables[0];
                                if (_dt1.Rows.Count > SamLimitCount)
                                {
                                    ModelPrint(MachineID + "_" + (j + 1).ToString() + " " + "SHORT" + " 样本 " + barcode + " 超过管控次数 " + _dt1.Rows.Count.ToString() + " > " + SamLimitCount.ToString());
                                    Result[2][j] = 0;
                                    break;
                                }
                            }
                        }
                    }
                    oraDB.disconnect();
                    string rst = "";
                    bool success = true;
                    for (int i = 0; i < 3; i++)
                    {
                        //rst += i.ToString() + ";";
                        for (int j = 0; j < 8; j++)
                        {
                            if (Result[i][j] == 0)
                            {
                                success = false;
                                rst += "0;";
                            }
                            else
                            {
                                rst += "1;";
                            }
                        }
                    }
                    string ResultStr;
                    if (success)
                    {
                        ResultStr = "EndSample";
                        await udp.SendAsync("testModel:0");
                    }
                    else
                    {
                        ResultStr = "RestartSample;" + rst;
                    }
                    await TestSentNet.SendAsync(ResultStr);
                    ModelPrint(Name + ResultStr);
                }
                catch (Exception ex)
                {
                    ModelPrint(Name + ex.Message);
                }
                
            });
        }

        #endregion

    }
    public class ProducInfo
    {
        //条码 板条码 产品状态 日期 时间
        public string Barcode { set; get; }
        public string BordBarcode { set; get; }
        public int Status { set; get; }
        public string TDate { set; get; }
        public string TTime { set; get; }
    }
    public class JasperTester
    {
        public string[] Result { set; get; }
        public int[] PassCount { set; get; }
        public int[] TestCount { set; get; }
        public Double[] Yield { set; get; }
        public JasperTester()
        {
            Result = new string[8] { "N", "N", "N", "N", "N", "N", "N", "N", };
            PassCount = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            TestCount = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            Yield = new double[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
        }
    }

}
