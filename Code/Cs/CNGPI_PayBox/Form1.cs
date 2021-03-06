﻿using CNGPI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace CNGPI_PayBox
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] ports= System.IO.Ports.SerialPort.GetPortNames();
            foreach(string pn in ports)
            {
                base_comname.Items.Add(pn);
            }
            if (base_comname.Items.Count > 0)
            {
                base_comname.SelectedIndex = 0;
            }
            Devinfo = new CNGPI.DeviceInfo(0x00010001);
            Devinfo.DeviceType = 0x0103;
            Devinfo.ID = CNGPI.Utility.ByteToHex(Guid.NewGuid().ToByteArray());
            sync_cb_state.SelectedIndex = 0;

            //订单号生成方法
            byte[] ordernum = Guid.NewGuid().ToByteArray();
        }

        CNGPI.CNGPIPayBox PayBox = null;
        CNGPI.DeviceInfo Devinfo = null;

        private void base_bt_fresh_Click(object sender, EventArgs e)
        {
            if (PayBox != null)
            {
                PayBox.Dispose();
                PayBox = null;
            }
            for (int i=0;i< base_comname.Items.Count; i++)
            {
                try
                {
                    CNGPI.CNGPIPayBox box = new CNGPI.CNGPIPayBox(Devinfo, base_comname.Items[i].ToString());
                    box.OnIODebug += Box_OnIODebug;
                    box.OnReviceMsg += Box_OnReviceMsg;
                    if (box.ConnectAndShakeHands())
                    {
                        PayBox = box;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    event_txt.AppendText(ex.ToString());
                }
            }
            PayBoxConnect();
        }

        private CNGPI.Message Box_OnReviceMsg(CNGPI.Message msg)
        {
            switch (msg.PID)
            {
                case 0x010C:
                    return new Msg_GameAlert_Back()
                    {
                        TransID = (msg as CNGPI.ITransMsg).TransID,
                        ADR = PayBox.RemoteDev.CurrPortIndex
                    };
                case 0x0105:
                    return new Msg_GameStart_Back()
                    {
                        TransID = (msg as CNGPI.ITransMsg).TransID,
                        ADR = PayBox.RemoteDev.CurrPortIndex
                    };
                case 0x0106:
                    return new Msg_GameFinish_Back()
                    {
                        TransID = (msg as CNGPI.ITransMsg).TransID,
                        ADR = PayBox.RemoteDev.CurrPortIndex,
                        ErrCode=0
                    };
                case 0x0401:
                    var msg41 = msg as Msg_CreateOrder_Event;
                    ven_box.Text = msg41.BoxNum.ToString();
                    ven_price.Text = msg41.Price.ToString();
                    return new Msg_CreateOrder_Back()
                    {
                        ADR = PayBox.RemoteDev.CurrPortIndex,
                        ErrCode = 0,
                        QrCode = "https://github.com/xsharkx/CNGPI/raw/master/logo/CNGPIV1.png"
                    };
                case 0x0402:
                    return new Msg_CancelOrder_Back()
                    {
                        ADR = PayBox.RemoteDev.CurrPortIndex,
                        ErrCode = 0,
                    };
                case 0x0409:
                    return new Msg_GetQrCode_Back()
                    {
                        ADR = PayBox.RemoteDev.CurrPortIndex,
                        ErrCode = 0,
                        QrCode = "https://github.com/xsharkx/CNGPI/raw/master/logo/CNGPIV1.png"
                    };
            }
            return null;
        }

        private void DebugInfo(string msg)
        {
            this.BeginInvoke((EventHandler)delegate {
                event_txt.AppendText(System.DateTime.Now.ToString("HH:mm:ss(fff)") + "\r\n" + msg + "\r\n\r\n\r\n");
            });
        }

        private void Box_OnIODebug(string msg, byte[] data)
        {
            if (msg.IndexOf("状态同步") != -1 && !chk_showsync.Checked)
            {
                return;
            }
            DebugInfo($"{msg}:\r\n{CNGPI.Utility.ByteToHex2(data)}");
        }

        private void PayBoxConnect()
        {
            grp_nomal.Enabled=grp_sync.Enabled = (PayBox != null);
            if (PayBox != null)
            {
                grp_gift.Enabled = PayBox.RemoteDev.DeviceType == 0x0003 || PayBox.RemoteDev.DeviceType == 0x0005;
                grp_tigket.Enabled = PayBox.RemoteDev.DeviceType == 0x0002;
                grp_sell.Enabled = PayBox.RemoteDev.DeviceType == 0x0004 || PayBox.RemoteDev.DeviceType == 0x0005;
            }
            else
            {
                grp_gift.Enabled = false;
                grp_tigket.Enabled = false;
                grp_sell.Enabled = false;
            }
        }

        private void base_bt_conn_Click(object sender, EventArgs e)
        {
            if (PayBox == null)
            {
                try
                {
                    CNGPI.CNGPIPayBox box = new CNGPI.CNGPIPayBox(Devinfo, base_comname.Text.ToString());
                    box.OnIODebug += Box_OnIODebug;
                    box.OnReviceMsg += Box_OnReviceMsg;
                    if (box.ConnectAndShakeHands())
                    {
                        PayBox = box;
                    }
                }
                catch (Exception ex)
                {
                    event_txt.AppendText(ex.ToString());
                }
                if (PayBox != null)
                {
                    base_bt_conn.Text = "断开连接";
                }
            }
            else
            {
                PayBox.Dispose();
                PayBox = null;
                base_bt_conn.Text = "连接并握手";
            }
            PayBoxConnect();
        }

        private void all_bt_readconfig_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendAndBackMsg<CNGPI.Msg_GetConfig_Back>(new CNGPI.Msg_GetConfig_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex
                }, 2000);
                if (back.ErrCode == 0)
                {
                    all_num_coin.Value = back.CoinsPerTimes;
                    DebugInfo("成功");
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }

            }
            catch(Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private string GetErrorCode(int code)
        {
            return code.ToString("X2").PadLeft(4, '0');
        }

        private void all_bt_writeconfig_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendAndBackMsg<CNGPI.Msg_Config_Back>(new CNGPI.Msg_Config_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex,
                    CoinsPerTimes = (int)all_num_coin.Value
                }, 2000);
                if (back.ErrCode == 0)
                {
                    DebugInfo("成功");
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void all_bt_readexconfig_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendAndBackMsg<CNGPI.Msg_MenuGet_Back>(new CNGPI.Msg_MenuGet_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex,
                    ItemID=Int32.Parse(txt_menuitem.Text),
                }, 2000);
                if (back.ErrCode == 0)
                {
                    DebugInfo("成功");
                    txt_menuvalue.Text = back.ItemValue.ToString();
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void all_bt_writeexconfig_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendAndBackMsg<CNGPI.Msg_MenuSet_Back>(new CNGPI.Msg_MenuSet_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex,
                    ItemID = Int32.Parse(txt_menuitem.Text),
                    ItemValue= (uint)Int32.Parse(txt_menuvalue.Text),
                }, 2000);
                if (back.ErrCode == 0)
                {
                    DebugInfo("成功");
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void all_bt_reset_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendAndBackMsg<CNGPI.Msg_Reset_Back>(new CNGPI.Msg_Reset_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex
                }, 2000);
                if (back.ErrCode == 0)
                {
                    DebugInfo("成功");
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void all_bt_getcount_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendAndBackMsg<CNGPI.Msg_GetCounter_Back>(new CNGPI.Msg_GetCounter_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex
                }, 2000);
                if (back.ErrCode == 0)
                {
                    DebugInfo($"成功");
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void all_bt_stop_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendAndBackMsg<CNGPI.Msg_Pause_Back>(new CNGPI.Msg_Pause_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex,
                    Action=0
                }, 2000);
                if (back != null)
                {
                    DebugInfo("成功");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void all_bt_restart_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendAndBackMsg<CNGPI.Msg_Pause_Back>(new CNGPI.Msg_Pause_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex,
                    Action = 1
                }, 2000);
                if (back != null)
                {
                    DebugInfo("成功");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void all_bt_pay_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendNoRepeatAndBackMsg<CNGPI.Msg_Pay_Back>(new CNGPI.Msg_Pay_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex,
                    Coins= (int)all_num_coin.Value,
                    CoinType=0
                },500, 10);
                if (back.ErrCode == 0)
                {
                    DebugInfo($"成功");
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void gift_bt_readconfig_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendAndBackMsg<CNGPI.Msg_GetGiftConfig_Back>(new CNGPI.Msg_GetGiftConfig_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex,
                }, 500);
                if (back.ErrCode == 0)
                {
                    DebugInfo($"成功");
                    gift_num_coin.Value = back.CoinsPerTimes;
                    gift_num_gametime.Value = back.GameSec;
                    gift_num_highpower.Value = back.HightPower;
                    gift_num_lowpower.Value = back.LowPower;
                    gift_tb_paycoin.Text = back.WinCoins.ToString();
                    gift_tb_paycoin_out.Text = back.WinGifts.ToString();
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void gift_bt_writeconfig_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendAndBackMsg<CNGPI.Msg_GiftConfig_Back>(new CNGPI.Msg_GiftConfig_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex,
                    CoinsPerTimes = (int)gift_num_coin.Value,
                    GameSec = (int)gift_num_gametime.Value,
                    HightPower = (int)gift_num_highpower.Value,
                    LowPower = (int)gift_num_lowpower.Value,
                    WinCoins = Int32.Parse(gift_tb_paycoin.Text),
                    WinGifts= Int32.Parse(gift_tb_paycoin_out.Text),
                }, 500);
                if (back.ErrCode == 0)
                {
                    DebugInfo($"成功");
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void gift_bt_paywithwinlist_Click(object sender, EventArgs e)
        {
            try
            {
                string[] items = gift_tb_winlist.Text.Split(',');
                List<int> list = new List<int>();
                foreach(string item in items)
                {
                    if (item.Trim().Length == 0) continue;
                    list.Add(Int32.Parse(item));
                }
                var back = PayBox.SendNoRepeatAndBackMsg<CNGPI.Msg_PayWithWinList_Back>(new CNGPI.Msg_PayWithWinList_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex,
                    WinList = list
                }, 500, 10);
                if (back.ErrCode == 0)
                {
                    DebugInfo($"成功");
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void all_bt_real_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendNoRepeatAndBackMsg<CNGPI.Msg_Pay_Back>(new CNGPI.Msg_Pay_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex,
                    Coins = (int)all_num_coin.Value,
                    CoinType =1
                }, 500, 10);
                if (back.ErrCode == 0)
                {
                    DebugInfo($"成功");
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://github.com/xsharkx/CNGPI");
            }
            catch
            {

            }
        }

        private void sync_bt_sync_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendAndBackMsg<CNGPI.Msg_Sync_Back>(new CNGPI.Msg_Sync_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex,
                    State= Int32.Parse(sync_cb_state.Text.Split('=')[0].Replace("0x",""))
                }, 500);
                if (back.ErrCode ==0)
                {
                    sync_lab_state.Text = back.State.ToString("X2");
                    //DebugInfo($"成功");
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }
        }

        private void sell_bt_payok_Click(object sender, EventArgs e)
        {
            try
            {
                var back = PayBox.SendNoRepeatAndBackMsg<CNGPI.Msg_PayOrder_Back>(new CNGPI.Msg_PayOrder_Event()
                {
                    ADR = PayBox.RemoteDev.CurrPortIndex,
                    OrderNum = Utility.ByteToHex(Guid.NewGuid().ToByteArray()),
                    BoxNum = Int32.Parse(ven_box.Text),
                    Price=(uint) Int32.Parse(ven_price.Text),
                }, 500, 10);
                if (back.ErrCode == 0)
                {
                    //DebugInfo($"成功");
                }
                else
                {
                    DebugInfo($"错误{GetErrorCode(back.ErrCode)}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo(ex.Message);
            }           
        }

        public long GetTimeLikeJS()
        {
            long lLeft = 621355968000000000;
            DateTime dt = DateTime.Now;
            long Sticks = (dt.Ticks - lLeft) / 10000;
            return Sticks;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            event_txt.Clear();
        }
    }
}
