using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using JC_LS_DLL;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolBar;

namespace DLL_demo
{
    public partial class Form1 : Form
    {
        JC_LS_DLL.LS LaserSweeper = new JC_LS_DLL.LS();
        int iReturn;

      //LaserState = 0,//出光或关光（使能开启或关闭）
      //FireMode = 1,//出光模式（信号触发模式）
      //Power = 2,//激光功率（电流‰比例值）
      //Freq = 3,//重复频率（KHz）
      //PulseWidth = 4,//脉冲宽度（us）
      //GetLaserState = 5,//查询激光器状态     
      //PulseN = 6,//脉冲个数
      //FreqMode = 7,//频率触发模式

        //=========================================

		//异常情况返回值列表
		//-1;//接收到的数据不完整
		//-2;//未接收到数据
		//-3;//激光器未连接
		//-4;//发送数据失败
		//-5;//电流值‰超出范围(0-1000)
		//-6;//频率值超出范围(1.0-10.0)KHz
		//-7;//脉冲宽度值超出范围(1-9)us
		//-8;//脉冲数超出范围(1-65535)
		//-9;//Fire模式清扫异常
		//-10;//清扫参数超出范围
		//-11://查询清扫参数失败
		//-12;//串口号未提供
		//-13;//激光器连接失败
		//-14;//断开连接失败

        //=========================================

        //系统状态返回值
        //0：待机
        //1：系统故障
        //2：预热中

        public Form1()
        {
            InitializeComponent();
            string[] Ports =
                new[] { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9" };
            Ports.ToList().ForEach(port => comboBox1.Items.Add(port));

            string[] Tri_mode =
                new[] { "内触发", "外触发" };
            Tri_mode.ToList().ForEach(mode => comboBox2.Items.Add(mode));

            string[] Sig_tri_mode =
                new[] { "Fire模式", "Cw模式" };
            Sig_tri_mode.ToList().ForEach(mode => comboBox3.Items.Add(mode));
        }


        private void button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "连接串口")
            {//如果按钮显示的是打开
                string COMID = comboBox1.Text;
                iReturn = LaserSweeper.LS_4T_ConnectLaser(COMID);
                if (iReturn == 0)
                {
                    MessageBox.Show("串口" + COMID + "连接成功！");
                    button1.BackColor = Color.Red;
                    button1.Text = "关闭串口";

                }
                else
                {
                    MessageBox.Show("Error Code: " + iReturn);
                }

            }
            else
            {//要关闭串口
                iReturn = LaserSweeper.LS_4T_DisConnectLaser();
                if (iReturn == 0)
                {
                    MessageBox.Show("串口连接已断开！");
                    button1.BackColor = Color.LightGreen; 
                    button1.Text = "连接串口";
                }
                else
                {
                    MessageBox.Show("Error Code: " + iReturn);
                }

            }
        

        }

        //使能开启或关闭
        private void button2_Click(object sender, EventArgs e)
        {
            if (button2.Text == "使能开")
            {
                iReturn = LaserSweeper.LS_4T_SetLaserParaI((int)CmdID.LaserState, 1);
                if (iReturn == 0)
                {
                    button2.BackColor = Color.Red;
                    button2.Text = "使能关";
                }
                else
                {
                    MessageBox.Show("Error Code: " + iReturn);
                }
            }
            else
            {
                iReturn = LaserSweeper.LS_4T_SetLaserParaI(0, 0);
                if (iReturn == 0)
                {
                    button2.BackColor = Color.LightGreen;
                    button2.Text = "使能开";
                }
                else
                {
                    MessageBox.Show("Error Code: " + iReturn);
                }

            }
        }

        //频率触发模式
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox2.Text == "内触发")
            {
                iReturn = LaserSweeper.LS_4T_SetLaserParaI((int)CmdID.FreqMode, 0);
                if (iReturn == 0)
                {
                    MessageBox.Show("设置成功！");
                }
                else
                {
                    MessageBox.Show("Error Code: " + iReturn);
                }
            }
            else //外触发
            {
                iReturn = LaserSweeper.LS_4T_SetLaserParaI((int)CmdID.FreqMode, 1);
                if (iReturn == 0)
                {
                    MessageBox.Show("设置成功！");
                }
                else
                {
                    MessageBox.Show("Error Code: " + iReturn);
                }
            }
            
        }

        //设置激光清扫参数
        private void button3_Click(object sender, EventArgs e)
        {
            if (!double.TryParse(textBox1.Text.Trim(), out double dLaserPwrValue) || !double.TryParse(textBox2.Text.Trim(), out double dLaserRepF)
                || !int.TryParse(textBox3.Text.Trim(), out int iLaserPulseN))
            {
                MessageBox.Show("参数输入不正确，三个参数都请输入数字！");
            }
            else
            {
                iReturn = LaserSweeper.LS_4A_SetLaserSweeperPara(dLaserPwrValue, dLaserRepF, iLaserPulseN);
                if (iReturn == 0)
                    MessageBox.Show("返回值：" + iReturn);
                else
                    MessageBox.Show("Error Code: " + iReturn);
            }
        }

        //出光模式（信号触发模式）
        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox3.Text == "Fire模式")
            {
                iReturn = LaserSweeper.LS_4T_SetLaserParaI((int)CmdID.FireMode, 0);
                if (iReturn == 0)
                {
                    MessageBox.Show("设置成功！");
                }
                else
                {
                    MessageBox.Show("Error Code: " + iReturn);
                }
            }
            else //Cw模式
            {
                iReturn = LaserSweeper.LS_4T_SetLaserParaI((int)CmdID.FireMode, 1);
                if (iReturn == 0)
                {
                    MessageBox.Show("设置成功！");
                }
                else
                {
                    MessageBox.Show("Error Code: " + iReturn);
                }
            }
        }

        //设置功率（电流）
        private void button4_Click(object sender, EventArgs e)
        {
            if (!double.TryParse(textBox4.Text.Trim(), out double dPowerValue))
            {
                MessageBox.Show("输入不正确，请输入数字");
            }

            else
            {
                iReturn = LaserSweeper.LS_4T_SetLaserParaI((int)CmdID.Power, dPowerValue);
                if (iReturn == 0)
                    MessageBox.Show(" 设置成功！");
                else
                    MessageBox.Show("Error Code: " + iReturn);
            }
        }

        //设置频率
        private void button5_Click(object sender, EventArgs e)
        {
            if (!double.TryParse(textBox5.Text.Trim(), out double dPowerValue))
            {
                MessageBox.Show("输入不正确，请输入数字");
            }

            else
            {
                iReturn = LaserSweeper.LS_4T_SetLaserParaI((int)CmdID.Freq, dPowerValue);
                if (iReturn == 0)
                    MessageBox.Show(" 设置成功！");
                else
                    MessageBox.Show("Error Code: " + iReturn);
            }
        }

        //设置脉宽
        private void button6_Click(object sender, EventArgs e)
        {
            if (!double.TryParse(textBox6.Text.Trim(), out double dPulseWidth))
            {
                MessageBox.Show("输入不正确，请输入数字");
            }

            else
            {
                iReturn = LaserSweeper.LS_4T_SetLaserParaI((int)CmdID.PulseWidth, dPulseWidth);
                if (iReturn == 0)
                    MessageBox.Show(" 设置成功！");
                else
                    MessageBox.Show("Error Code: " + iReturn);
            }
        }

        //设置脉冲个数
        private void button7_Click(object sender, EventArgs e)
        {
            if (!double.TryParse(textBox7.Text.Trim(), out double dPulseN))
            {
                MessageBox.Show("输入不正确，请输入数字");
            }

            else
            {
                iReturn = LaserSweeper.LS_4T_SetLaserParaI((int)CmdID.PulseN, dPulseN);
                if (iReturn == 0)
                    MessageBox.Show(" 设置成功！");
                else
                    MessageBox.Show("Error Code: " + iReturn);
            }
        }
        //初始化激光器
        private void button18_Click(object sender, EventArgs e)
        {
            string COMID = comboBox1.Text;
            iReturn = LaserSweeper.LS_4T_InitLaser(COMID);
            if (iReturn == 0)
            {
                button1.Text = "关闭串口";
                button1.BackColor = Color.Red;
                
                button2.Text = "使能关";
                button2.BackColor = Color.Red;
                MessageBox.Show("设置成功！");
            }
            else
            {
                MessageBox.Show("Error Code: " + iReturn);
            }
        }

        //触发清扫
        private void button14_Click(object sender, EventArgs e)
        {
            iReturn = LaserSweeper.LS_4T_Fire();
            if (iReturn == 0)
            {
                //MessageBox.Show("设置成功！");
                Console.WriteLine("Set OK!");
            }
            else
            {
                MessageBox.Show("Error Code: " + iReturn);
            }

        }

        //查询清扫是否完成
        private void button15_Click(object sender, EventArgs e)
        {
            iReturn = LaserSweeper.LS_4T_GetFireFinish();
            if (iReturn == 0)
            {
                MessageBox.Show("清扫已完成！");
            }
            else
            {
                MessageBox.Show("Error Code: " + iReturn);
            }
        }

        //查询使能状态
        private void button8_Click(object sender, EventArgs e)
        {
            unsafe
            {
                double* value;
                double a = 0;
                value = &a;
                iReturn = LaserSweeper.LS_4T_GetLaserParaI((int)CmdID.LaserState, value);
                if (iReturn == 0)
                {
                    if (*value == 0)
                    {
                        textBox8.Text = "关闭";
                        MessageBox.Show("返回值：" + iReturn + " " + "使能状态值：" + *value);
                        
                    }
                    else
                    {
                        textBox8.Text = "开启";
                        MessageBox.Show("返回值：" + iReturn + " " + "使能状态值：" + *value);
                        
                    }
                }

                else
                    MessageBox.Show("Error Code: " + iReturn);
            }

        }

        //查询出光（信号触发）模式
        private void button9_Click(object sender, EventArgs e)
        {
            unsafe
            {
                double* value;
                double a = 0;
                value = &a;
                iReturn = LaserSweeper.LS_4T_GetLaserParaI((int)CmdID.FireMode, value);
                if (iReturn == 0)
                {
                    if (*value == 0)
                    {
                        textBox9.Text = "Fire模式";
                        MessageBox.Show("返回值：" + iReturn + " " + "出光模式状态值：" + *value);
                        
                    }
                    else
                    {
                        textBox9.Text = "Cw模式";
                        MessageBox.Show("返回值：" + iReturn + " " + "出光模式状态值：" + *value);

                    }
                }

                else
                    MessageBox.Show("Error Code: " + iReturn);
            }
        }
        //查询功率（电流）
        private void button10_Click(object sender, EventArgs e)
        {
            //textBox10.Text = "后续会添加";
            MessageBox.Show("目前激光器无法查询此值，后续会添加");
        }
        //查询频率
        private void button11_Click(object sender, EventArgs e)
        {
            unsafe
            {
                double* value;
                double a = 0;
                value = &a;
                iReturn = LaserSweeper.LS_4T_GetLaserParaI((int)CmdID.Freq, value);
                if (iReturn == 0)
                {
                    textBox11.Text = (*value).ToString();
                    MessageBox.Show("返回值：" + iReturn + " " + "频率值：" + *value);

                }

                else
                    MessageBox.Show("Error Code: " + iReturn);
            }
        }
        //查询脉宽
        private void button12_Click(object sender, EventArgs e)
        {
            unsafe
            {
                double* value;
                double a = 0;
                value = &a;
                iReturn = LaserSweeper.LS_4T_GetLaserParaI((int)CmdID.PulseWidth, value);
                if (iReturn == 0)
                {
                    textBox12.Text = (*value).ToString();
                    MessageBox.Show("返回值：" + iReturn + " " + "脉宽值：" + *value);

                }

                else
                    MessageBox.Show("Error Code: " + iReturn);
            }
        }
        //查询脉冲数
        private void button13_Click(object sender, EventArgs e)
        {
            //textBox13.Text = "后续会添加";
            MessageBox.Show("目前激光器无法查询此值，后续会添加");
        }
        //查询通信状态
        private void button16_Click(object sender, EventArgs e)
        {
            iReturn = LaserSweeper.LS_4T_GetLaserCommSts();
            if (iReturn == 0)
            {
                textBox14.Text = "正常";
                MessageBox.Show("正常");

            }
            else
            {
                textBox14.Text = "异常";
                MessageBox.Show("Error Code: " + iReturn);

            }
        }
        //查询激光器状态
        private void button17_Click(object sender, EventArgs e)
        {
            unsafe
            {
                int* laserState;
                int a = -1;
                laserState = &a;
                iReturn = LaserSweeper.LS_4T_GetLaserState(laserState);
                if (iReturn == 0)
                {
                    if (*laserState == 0)
                    {
                        textBox15.Text = "空闲";
                        MessageBox.Show("状态值：" + *laserState + ", " + "激光器状态为空闲");

                    }
                    else if (*laserState == 1)
                    {
                        textBox15.Text = "系统故障";
                        MessageBox.Show("状态值：" + *laserState + ", " + "激光器状态为系统故障");

                    }
                    else if (*laserState == 2)
                    {
                        textBox15.Text = "预热中";
                        MessageBox.Show("状态值：" + *laserState + ", " + "激光器状态为预热中");

                    }

                }
                else
                {
                    textBox15.Text = "异常";
                    MessageBox.Show("Error Code: " + iReturn);

                }
            }
        }
    }
}

