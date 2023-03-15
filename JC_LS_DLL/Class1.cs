using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace JC_LS_DLL
{
    public enum CmdID  //枚举变量表示
    {
        LaserState = 0,//出光或关光（使能开启或关闭）
        FireMode = 1,//出光模式（信号触发模式）
        Power = 2,//激光功率（电流‰比例值）
        Freq = 3,//重复频率（KHz）
        PulseWidth = 4,//脉冲宽度（us）
        GetLaserState = 5,//查询激光器状态     
        PulseN = 6,//脉冲个数
        FreqMode = 7,//频率触发模式

    }

    //系统状态返回值列表
    //0：待机
    //1：系统故障
    //2：预热中


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

    public class LS
    {
        private readonly SerialPort SerialPort1 = new SerialPort();
        //变量申明
        byte[] data;//设置参数时用来存储发送给串口的完整字节数组
        byte[] data03;//设置脉冲数时实际发给串口的数据
        byte[] rawdata;//设置参数时用来存储不含CRC校验值的字节数组
        byte[] CRCvalue;//存储CRC校验值
        byte[] CRCvalue3;//设置脉冲个数时用来存储CRC校验值
        readonly int sleeptime = 50;//发送完request之后到查询激光器返回值之前需要一个间隔时间(ms)
        int firetime;//“脉冲数/频率”算出的理论出光时间值(s)

        //三个变量用于保存激光清扫参数（未启用）
        //double Power = -1;//用于查询激光清扫参数时来判断参数是否设置成功，如果这个值还是-1，则激光清扫参数没有设置成功过。
        //double Frequency = -1;//KHz
        //int PulseCount = -1;//范围1-65535

        //CRC-16-MODBUS,https://www.lddgo.net/encrypt/crc
        public static byte[] ToModbus(byte[] byteData)
        {
            byte[] CRC = new byte[2];

            UInt16 wCrc = 0xFFFF;
            for (int i = 0; i < byteData.Length; i++)
            {
                wCrc ^= Convert.ToUInt16(byteData[i]);
                for (int j = 0; j < 8; j++)
                {
                    if ((wCrc & 0x0001) == 1)
                    {
                        wCrc >>= 1;
                        wCrc ^= 0xA001;//异或多项式
                    }
                    else
                    {
                        wCrc >>= 1;
                    }
                }
            }

            CRC[1] = (byte)((wCrc & 0xFF00) >> 8);//高位在后
            CRC[0] = (byte)(wCrc & 0x00FF);       //低位在前
            return CRC;
        }

        //设置激光器参数
        public int LS_4T_SetLaserParaI(int cmdID, double value)
        {
            int ivalue;
            float fvalue;
            CmdID ID = (CmdID)cmdID;
            switch (ID)
            {
                case CmdID.LaserState:  //设置自动出光或关光(使能开启或关闭)
                    if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                    {
                        if (value == 1)//使能开启
                        {
                            rawdata = new byte[7] { 0x7F, 0x05, 0x21, 0x01, 0x00, 0x00, 0x00 };
                            CRCvalue = ToModbus(rawdata);
                            data = new byte[9] { 0x7F, 0x05, 0x21, 0x01, 0x00, 0x00, 0x00, CRCvalue[0], CRCvalue[1] };//包含CRC校验的数据，待发送
                            try
                            {
                                SerialPort1.Write(data, 0, 9);
                            }
                            catch (Exception)
                            {    // 捕捉异常
                                return -4;//发送数据失败
                            }
                        }
                        else //使能关闭
                        {
                            rawdata = new byte[7] { 0x7F, 0x05, 0x21, 0x00, 0x00, 0x00, 0x00 };
                            CRCvalue = ToModbus(rawdata);
                            data = new byte[9] { 0x7F, 0x05, 0x21, 0x00, 0x00, 0x00, 0x00, CRCvalue[0], CRCvalue[1] };//包含CRC校验的数据，待发送
                            try
                            {
                                SerialPort1.Write(data, 0, 9);
                            }
                            catch (Exception)
                            {    // 捕捉异常
                                return -4;//发送数据失败
                            }
                        }
                        Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                        if (SerialPort1.BytesToRead > 0)
                        {
                            byte[] recData = new byte[SerialPort1.BytesToRead];
                            SerialPort1.Read(recData, 0, recData.Length);
                            if (recData.Count() == 9 && recData[7] == CRCvalue[0] && recData[8] == CRCvalue[1])
                                return 0;
                            else
                                return -1;//接收到的数据不完整
                        }
                        else
                        {
                            return -2;//未接收到数据
                        }
                    }
                    else
                    {
                        return -3;//激光器未连接

                    }

                case CmdID.FireMode:  //设置出光模式（常出光Cw模式或Fire模式）
                    if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                    {
                        if (value == 1)//Cw模式
                        {
                            rawdata = new byte[7] { 0x7F, 0x05, 0x0D, 0x01, 0x00, 0x00, 0x00 };
                            CRCvalue = ToModbus(rawdata);
                            data = new byte[9] { 0x7F, 0x05, 0x0D, 0x01, 0x00, 0x00, 0x00, CRCvalue[0], CRCvalue[1] };
                            try
                            {
                                SerialPort1.Write(data, 0, 9);
                            }
                            catch (Exception)
                            {    // 捕捉异常
                                return -4;//发送数据失败
                            }
                        }
                        else//Fire模式
                        {
                            rawdata = new byte[7] { 0x7F, 0x05, 0x0D, 0x00, 0x00, 0x00, 0x00 };
                            CRCvalue = ToModbus(rawdata);
                            data = new byte[9] { 0x7F, 0x05, 0x0D, 0x00, 0x00, 0x00, 0x00, CRCvalue[0], CRCvalue[1] };
                            try
                            {
                                SerialPort1.Write(data, 0, 9);
                            }
                            catch (Exception)
                            {    // 捕捉异常
                                return -4;//发送数据失败
                            }
                        }
                        Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                        if (SerialPort1.BytesToRead > 0)
                        {
                            byte[] recData = new byte[SerialPort1.BytesToRead];
                            SerialPort1.Read(recData, 0, recData.Length);

                            if (recData.Count() == 9 && recData[7] == CRCvalue[0] && recData[8] == CRCvalue[1])
                                return 0;
                            else
                                return -1;//接收到的数据不完整
                        }
                        else
                        {
                            return -2;//未接收到数据
                        }
                    }
                    else
                    {
                        return -3;//激光器未连接

                    }

                case CmdID.Power:  //设置功率，此处为设置电流大小
                    if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                    {
                        ivalue = Convert.ToInt32(value);
                        if ((ivalue < 0) || (ivalue > 1000))
                        {
                            return -5;//电流值‰超出范围
                        }
                        else
                        {
                            var b = BitConverter.GetBytes(ivalue);
                            string temp = BitConverter.ToString(b.ToArray()).Replace("-", "");
                            var data1 = temp.Substring(0, 2);
                            var data2 = temp.Substring(2, 2);
                            var data3 = temp.Substring(4, 2);
                            var data4 = temp.Substring(6, 2);
                            rawdata = new byte[7];
                            rawdata[0] = Convert.ToByte("7F", 16);
                            rawdata[1] = Convert.ToByte("05", 16);
                            rawdata[2] = Convert.ToByte("33", 16);
                            rawdata[3] = Convert.ToByte(data1, 16);
                            rawdata[4] = Convert.ToByte(data2, 16);
                            rawdata[5] = Convert.ToByte(data3, 16);
                            rawdata[6] = Convert.ToByte(data4, 16);
                            CRCvalue = ToModbus(rawdata);
                            data = new byte[9] { rawdata[0], rawdata[1], rawdata[2], rawdata[3], rawdata[4], rawdata[5], rawdata[6], CRCvalue[0], CRCvalue[1] };
                            foreach (var item in data) { Console.WriteLine(item); }
                            try
                            {
                                SerialPort1.Write(data, 0, 9);
                            }
                            catch (Exception)
                            {    // 捕捉异常
                                return -4;//发送数据失败
                            }
                        }
                        Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                        if (SerialPort1.BytesToRead > 0)
                        {
                            byte[] recData = new byte[SerialPort1.BytesToRead];
                            SerialPort1.Read(recData, 0, recData.Length);

                            if (recData.Count() == 9 && recData[7] == CRCvalue[0] && recData[8] == CRCvalue[1])
                            {
                                //Power = value;
                                return 0;
                            }
                            else
                                return -1;//接收到的数据不完整
                        }
                        else
                        {
                            return -2;//未接收到数据
                        }
                    }
                    else
                    {
                        return -3;//激光器未连接
                    }

                case CmdID.Freq:  //设置频率
                    if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                    {
                        if ((value < 1) || (value > 10))
                        {
                            return -6;//频率值超出范围
                        }
                        else
                        {
                            string value01 = Convert.ToString(value);
                            fvalue = float.Parse(value01);
                            byte[] b = BitConverter.GetBytes(fvalue);
                            string temp = BitConverter.ToString(b.ToArray()).Replace("-", "");
                            var data1 = temp.Substring(0, 2);
                            var data2 = temp.Substring(2, 2);
                            var data3 = temp.Substring(4, 2);
                            var data4 = temp.Substring(6, 2);
                            rawdata = new byte[7];
                            rawdata[0] = Convert.ToByte("7F", 16);
                            rawdata[1] = Convert.ToByte("05", 16);
                            rawdata[2] = Convert.ToByte("02", 16);
                            rawdata[3] = Convert.ToByte(data1, 16);
                            rawdata[4] = Convert.ToByte(data2, 16);
                            rawdata[5] = Convert.ToByte(data3, 16);
                            rawdata[6] = Convert.ToByte(data4, 16);
                            CRCvalue = ToModbus(rawdata);
                            data = new byte[9] { rawdata[0], rawdata[1], rawdata[2], rawdata[3], rawdata[4], rawdata[5], rawdata[6], CRCvalue[0], CRCvalue[1] };
                            try
                            {
                                SerialPort1.Write(data, 0, 9);
                            }
                            catch (Exception)
                            {    // 捕捉异常
                                return -4;//发送数据失败
                            }
                        }
                        Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                        if (SerialPort1.BytesToRead > 0)
                        {
                            byte[] recData = new byte[SerialPort1.BytesToRead];
                            SerialPort1.Read(recData, 0, recData.Length);

                            if (recData.Count() == 9 && recData[7] == CRCvalue[0] && recData[8] == CRCvalue[1])
                            {
                                //Frequency = value;
                                return 0;
                            }
                            else
                                return -1;//接收到的数据不完整
                        }
                        else
                        {
                            return -2;//未接收到数据
                        }
                    }
                    else
                    {
                        return -3;//激光器未连接
                    }

                case CmdID.PulseWidth:  //设置脉冲宽度
                    if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                    {
                        if ((value < 1) || (value > 9))
                        {
                            return -7;//脉冲宽度值超出范围
                        }
                        else
                        {                           
                            ivalue = Convert.ToInt32(value);
                            byte[] b = BitConverter.GetBytes(ivalue);
                            string temp = BitConverter.ToString(b.ToArray()).Replace("-", "");
                            var data1 = temp.Substring(0, 2);
                            var data2 = temp.Substring(2, 2);
                            var data3 = temp.Substring(4, 2);
                            var data4 = temp.Substring(6, 2);
                            rawdata = new byte[7];
                            rawdata[0] = Convert.ToByte("7F", 16);
                            rawdata[1] = Convert.ToByte("05", 16);
                            rawdata[2] = Convert.ToByte("02", 16);
                            rawdata[3] = Convert.ToByte(data1, 16);
                            rawdata[4] = Convert.ToByte(data2, 16);
                            rawdata[5] = Convert.ToByte(data3, 16);
                            rawdata[6] = Convert.ToByte(data4, 16);
                            CRCvalue = ToModbus(rawdata);
                            data = new byte[9] { rawdata[0], rawdata[1], rawdata[2], rawdata[3], rawdata[4], rawdata[5], rawdata[6], CRCvalue[0], CRCvalue[1] };
                            try
                            {
                                SerialPort1.Write(data, 0, 9);
                            }
                            catch (Exception)
                            {    // 捕捉异常
                                return -4;//发送数据失败
                            }
                        }
                        Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                        if (SerialPort1.BytesToRead > 0)
                        {
                            byte[] recData = new byte[SerialPort1.BytesToRead];
                            SerialPort1.Read(recData, 0, recData.Length);

                            if (recData.Count() == 9 && recData[7] == CRCvalue[0] && recData[8] == CRCvalue[1])
                                return 0;
                            else
                                return -1;//接收到的数据不完整
                        }

                        else
                        {
                            return -2;//未接收到数据
                        }

                    }
                    else
                    {
                        return -3;//激光器未连接
                    }

                case CmdID.PulseN:  //设置脉冲数，因为这个激光器设置脉冲数完成就会Fire，所以此处只算出需要发送的值，不发送给串口。
                    if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                    {
                        ivalue = Convert.ToInt32(value);
                        if ((value < 1) || (value > 65535))
                        {
                            return -8;//脉冲数超出范围
                        }
                        else
                        {
                            byte[] b = BitConverter.GetBytes(ivalue);
                            string temp = BitConverter.ToString(b.ToArray()).Replace("-", "");
                            var data1 = temp.Substring(0, 2);
                            var data2 = temp.Substring(2, 2);
                            var data3 = temp.Substring(4, 2);
                            var data4 = temp.Substring(6, 2);
                            rawdata = new byte[7];
                            rawdata[0] = Convert.ToByte("7F", 16);
                            rawdata[1] = Convert.ToByte("05", 16);
                            rawdata[2] = Convert.ToByte("3B", 16);
                            rawdata[3] = Convert.ToByte(data1, 16);
                            rawdata[4] = Convert.ToByte(data2, 16);
                            rawdata[5] = Convert.ToByte(data3, 16);
                            rawdata[6] = Convert.ToByte(data4, 16);
                            CRCvalue = ToModbus(rawdata);
                            data = new byte[9] { rawdata[0], rawdata[1], rawdata[2], rawdata[3], rawdata[4], rawdata[5], rawdata[6], CRCvalue[0], CRCvalue[1] };
                            //PulseCount = ivalue;
                            return 0;
                        }
                    }
                    else
                    {
                        return -3;//激光器未连接
                    }

                case CmdID.FreqMode:
                    if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                    {
                        if (value == 0)//内触发
                        {
                            rawdata = new byte[7] { 0x7F, 0x05, 0x01, 0x00, 0x00, 0x00, 0x00 };//不含CRC校验的数据
                            CRCvalue = ToModbus(rawdata);
                            //foreach (var item in CRCvalue) { Console.WriteLine(item); }
                            //Console.WriteLine("Add CRCvalue:");
                            data = new byte[9] { 0x7F, 0x05, 0x01, 0x00, 0x00, 0x00, 0x00, CRCvalue[0], CRCvalue[1] };//包含CRC校验的数据，待发送

                            try
                            {
                                SerialPort1.Write(data, 0, 9); //向串口发送request
                            }
                            catch (Exception)
                            {    // 捕捉异常
                                return -4;//发送数据失败
                            }
                        }
                        else //外触发
                        {
                            rawdata = new byte[7] { 0x7F, 0x05, 0x01, 0x01, 0x00, 0x00, 0x00 };
                            CRCvalue = ToModbus(rawdata);
                            data = new byte[9] { 0x7F, 0x05, 0x01, 0x01, 0x00, 0x00, 0x00, CRCvalue[0], CRCvalue[1] };
                            foreach (var item in data) { Console.WriteLine(item); }

                            try
                            {
                                SerialPort1.Write(data, 0, 9);
                            }
                            catch (Exception)
                            {    // 捕捉异常
                                return -4;//发送数据失败
                            }
                        }
                        Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                        if (SerialPort1.BytesToRead > 0)
                        {
                            byte[] recData = new byte[SerialPort1.BytesToRead];
                            SerialPort1.Read(recData, 0, recData.Length);

                            if (recData.Count() == 9 && recData[7] == CRCvalue[0] && recData[8] == CRCvalue[1])
                                return 0;
                            else
                                return -1;//接收到的数据不完整
                        }
                        else
                        {
                            return -2;//未接收到数据
                        }
                    }
                    else
                    {
                        return -3;//激光器未连接

                    }
                default: return -1;
            }
        }

        //查询激光器参数
        public unsafe int LS_4T_GetLaserParaI(int cmdID, double* value)
        {
            CmdID ID = (CmdID)cmdID;
            switch (ID)
            {
                case CmdID.LaserState:  //查询使能状态
                    if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                    {
                        rawdata = new byte[3] { 0x5D, 0x01, 0x04 };
                        CRCvalue = ToModbus(rawdata);
                        data = new byte[5] { 0x5D, 0x01, 0x04, CRCvalue[0], CRCvalue[1] };//包含CRC校验的数据，待发送
                        try
                        {
                            SerialPort1.Write(data, 0, 5);
                        }
                        catch (Exception)
                        {    // 捕捉异常
                            return -4;//发送数据失败
                        }
                        Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                        if (SerialPort1.BytesToRead > 0)
                        {
                            byte[] recData = new byte[SerialPort1.BytesToRead];
                            SerialPort1.Read(recData, 0, recData.Length);

                            Console.WriteLine("======获取到的字节数======：" + recData.Count());
                            if (recData[0] == 0x5D && recData[1] == 0x36 && recData[2] == 0x04 && recData.Count() == 58)//log recData的长度和内容
                            {
                                double laserstate = recData[3];
                                Console.WriteLine("系统使能状态：" + laserstate);
                                *value = laserstate;
                                return 0;
                            }
                            else
                            {
                                Console.WriteLine("接收到的数据不完整");
                                return -1;//接收到的数据不完整
                            }
                        }
                        else
                        {
                            return -2;//未接收到数据
                        }
                    }
                    else
                    {
                        return -3;//激光器未连接
                    }

                case CmdID.FireMode:  //查询出光模式（常出光Cw模式或Fire模式，value=0是Fire模式，value=1是Cw模式）
                    if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                    {
                        rawdata = new byte[3] { 0x5D, 0x01, 0x04 };
                        CRCvalue = ToModbus(rawdata);
                        data = new byte[5] { 0x5D, 0x01, 0x04, CRCvalue[0], CRCvalue[1] };//包含CRC校验的数据，待发送                    
                        try
                        {
                            SerialPort1.Write(data, 0, 5);
                        }
                        catch (Exception)
                        {    // 捕捉异常
                            return -4;//发送数据失败
                        }
                        Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                        if (SerialPort1.BytesToRead > 0)
                        {
                            byte[] recData = new byte[SerialPort1.BytesToRead];
                            SerialPort1.Read(recData, 0, recData.Length);

                            Console.WriteLine("======获取到的字节数======：" + recData.Count());
                            if (recData[0] == 0x5D && recData[1] == 0x36 && recData[2] == 0x04 && recData.Count() == 58)//log recData的长度和内容
                            {
                                int firemode = recData[51];//信号触发模式
                                Console.WriteLine("信号触发模式:" + firemode);
                                *value = firemode;
                                return 0;

                            }
                            else
                            {
                                Console.WriteLine("接收到的数据不完整");
                                return -1;//接收到的数据不完整
                            }
                        }
                        else
                        {
                            return -2;//未接收到数据
                        }
                    }
                    else
                    {
                        return -3;//激光器未连接
                    }


                case CmdID.Power:  //查询激光功率(电流‰)，目前激光器的返回值没有这个值，待供应商增加该返回值之后再做补充
                    return 0;

                case CmdID.Freq:  //查询重复频率
                    if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                    {
                        rawdata = new byte[3] { 0x5D, 0x01, 0x04 };
                        CRCvalue = ToModbus(rawdata);
                        data = new byte[5] { 0x5D, 0x01, 0x04, CRCvalue[0], CRCvalue[1] };//包含CRC校验的数据，待发送
                        try
                        {
                            SerialPort1.Write(data, 0, 5);
                        }
                        catch (Exception)
                        {    // 捕捉异常
                            return -4;//发送数据失败
                        }
                        Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                        if (SerialPort1.BytesToRead > 0)
                        {
                            byte[] recData = new byte[SerialPort1.BytesToRead];
                            SerialPort1.Read(recData, 0, recData.Length);
                            Console.WriteLine("======获取到的字节数======：" + recData.Count());
                            if (recData[0] == 0x5D && recData[1] == 0x36 && recData[2] == 0x04 && recData.Count() == 58)//log recData的长度和内容
                            {
                                byte[] _freq_0 = new byte[4] { recData[10], recData[11], recData[12], recData[13] };//内触发频率
                                double freq = BitConverter.ToSingle(_freq_0, 0);
                                Console.WriteLine("频率：" + freq + "KHz");
                                *value = freq;
                                return 0;
                            }
                            else
                            {
                                Console.WriteLine("接收到的数据不完整");
                                return -1;//接收到的数据不完整
                            }
                        }
                        else
                        {
                            return -2;//未接收到数据
                        }
                    }
                    else
                    {
                        return -3;//激光器未连接
                    }

                case CmdID.PulseWidth:  //查询脉冲宽度
                    if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                    {
                        rawdata = new byte[3] { 0x5D, 0x01, 0x04 };
                        CRCvalue = ToModbus(rawdata);
                        data = new byte[5] { 0x5D, 0x01, 0x04, CRCvalue[0], CRCvalue[1] };//包含CRC校验的数据，待发送
                        try
                        {
                            SerialPort1.Write(data, 0, 5);
                        }
                        catch (Exception)
                        {    // 捕捉异常
                            return -4;//发送数据失败
                        }
                        Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                        if (SerialPort1.BytesToRead > 0)
                        {
                            byte[] recData = new byte[SerialPort1.BytesToRead];
                            SerialPort1.Read(recData, 0, recData.Length);
                            Console.WriteLine("======获取到的字节数======：" + recData.Count());
                            if (recData[0] == 0x5D && recData[1] == 0x36 && recData[2] == 0x04 && recData.Count() == 58)//log recData的长度和内容
                            {
                                int pulse_width = recData[14];//内触发脉宽
                                Console.WriteLine("内触发脉宽：" + pulse_width + "us");
                                *value = pulse_width;
                                return 0;
                            }
                            else
                            {
                                Console.WriteLine("接收到的数据不完整");
                                return -1;//接收到的数据不完整
                            }
                        }
                        else
                        {
                            return -2;//未接收到数据
                        }
                    }
                    else
                    {
                        return -3;//激光器未连接
                    }

                case CmdID.PulseN://查询脉冲个数，目前激光器的返回值没有这个值，待供应商增加之后再做补充
                    return 0;

                default: return -1;

            }

        }
        //查询激光器通信是否正常
        public int LS_4T_GetLaserCommSts()
        {
            if ((SerialPort1 != null) && (SerialPort1.IsOpen))
            {
                rawdata = new byte[3] { 0x5D, 0x01, 0x04 };
                CRCvalue = ToModbus(rawdata);
                data = new byte[5] { 0x5D, 0x01, 0x04, CRCvalue[0], CRCvalue[1] };//包含CRC校验的数据，待发送
                try
                {
                    SerialPort1.Write(data, 0, 5);
                }
                catch (Exception)
                {    // 捕捉异常
                    return -4;
                }
                Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                if (SerialPort1.BytesToRead > 0)
                {
                    byte[] recData = new byte[SerialPort1.BytesToRead];
                    SerialPort1.Read(recData, 0, recData.Length);
                    Console.WriteLine("======获取到的字节数======：" + recData.Count());
                    if (recData[0] == 0x5D && recData[1] == 0x36 && recData[2] == 0x04 && recData.Count() == 58)//log recData的长度和内容
                    {
                        return 0;

                    }
                    else
                    {
                        Console.WriteLine("接收到的数据不完整");
                        return -1;//接收到的数据不完整
                    }
                }
                else
                {
                    return -2;//未接收到数据
                }

            }
            else
            {
                return -3;//激光器未连接
            }
        }

        //设置激光清扫参数（由于这个激光器当脉冲数被设置成功时就会触发Fire动作，所以这里设置脉冲数时只做转换，并没有将值发送至串口。在后面触发清扫的动作中去发送）
        public int LS_4A_SetLaserSweeperPara(double LaserPwr, double LaserRepF, int LaserPulseN)
        {
            byte[] data01;
            byte[] data02;
            byte[] rawdata1;
            byte[] rawdata2;
            byte[] rawdata3;
            byte[] CRCvalue1;
            byte[] CRCvalue2;
            float fRepF;
            int ipower;
            int ret = -100;



            if ((SerialPort1 != null) && (SerialPort1.IsOpen))
            {
                ipower = Convert.ToInt32(LaserPwr);
                //fvalue =
                string value02 = Convert.ToString(LaserRepF);
                fRepF = float.Parse(value02);
                firetime = LaserPulseN / ((int)(LaserRepF * 1000));//出光时间firetime = pulseNum/freq (单位需要由KHz ->Hz)

                if (((ipower < 0) || (ipower > 1000))|| ((fRepF < 1) || (fRepF > 10))|| ((LaserPulseN < 1) || (LaserPulseN > 65535)))
                {
                    return -10;//清扫参数超出范围
                }
                else
                {
                    /////////Power
                    var a = BitConverter.GetBytes(ipower);
                    string temp = BitConverter.ToString(a.ToArray()).Replace("-", "");
                    var data1 = temp.Substring(0, 2);
                    var data2 = temp.Substring(2, 2);
                    var data3 = temp.Substring(4, 2);
                    var data4 = temp.Substring(6, 2);
                    rawdata1 = new byte[7];
                    rawdata1[0] = Convert.ToByte("7F", 16);
                    rawdata1[1] = Convert.ToByte("05", 16);
                    rawdata1[2] = Convert.ToByte("33", 16);
                    rawdata1[3] = Convert.ToByte(data1, 16);
                    rawdata1[4] = Convert.ToByte(data2, 16);
                    rawdata1[5] = Convert.ToByte(data3, 16);
                    rawdata1[6] = Convert.ToByte(data4, 16);
                    CRCvalue1 = ToModbus(rawdata1);
                    data01 = new byte[9] { rawdata1[0], rawdata1[1], rawdata1[2], rawdata1[3], rawdata1[4], rawdata1[5], rawdata1[6], CRCvalue1[0], CRCvalue1[1] };

                    try
                    {
                        SerialPort1.Write(data01, 0, 9);
                    }
                    catch (Exception)
                    {    // 捕捉异常
                        return -4;//发送数据失败
                    }
                    Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                    if (SerialPort1.BytesToRead > 0)
                    {
                        byte[] recData1 = new byte[SerialPort1.BytesToRead];
                        SerialPort1.Read(recData1, 0, recData1.Length);

                        if (recData1.Count() == 9 && recData1[7] == CRCvalue1[0] && recData1[8] == CRCvalue1[1])
                        {
                            ret = 0;
                        }

                        else
                            return -1;//接收到的数据不完整
                    }

                    else
                    {
                        return -2;//未接收到数据
                    }
                    /////////Freq
                    string value01 = Convert.ToString(LaserRepF);
                    float fvalue = float.Parse(value01);
                    byte[] b = BitConverter.GetBytes(fvalue);
                    string temp2 = BitConverter.ToString(b.ToArray()).Replace("-", "");
                    var data5 = temp2.Substring(0, 2);
                    var data6 = temp2.Substring(2, 2);
                    var data7 = temp2.Substring(4, 2);
                    var data8 = temp2.Substring(6, 2);
                    rawdata2 = new byte[7];
                    rawdata2[0] = Convert.ToByte("7F", 16);
                    rawdata2[1] = Convert.ToByte("05", 16);
                    rawdata2[2] = Convert.ToByte("02", 16);
                    rawdata2[3] = Convert.ToByte(data5, 16);
                    rawdata2[4] = Convert.ToByte(data6, 16);
                    rawdata2[5] = Convert.ToByte(data7, 16);
                    rawdata2[6] = Convert.ToByte(data8, 16);
                    CRCvalue2 = ToModbus(rawdata2);
                    data02 = new byte[9] { rawdata2[0], rawdata2[1], rawdata2[2], rawdata2[3], rawdata2[4], rawdata2[5], rawdata2[6], CRCvalue2[0], CRCvalue2[1] };

                    try
                    {
                        SerialPort1.Write(data02, 0, 9);
                    }
                    catch (Exception)
                    {    // 捕捉异常
                        return -4;//发送数据失败
                    }
                    Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                    if (SerialPort1.BytesToRead > 0)
                    {
                        byte[] recData2 = new byte[SerialPort1.BytesToRead];
                        SerialPort1.Read(recData2, 0, recData2.Length);

                        if (recData2.Count() == 9 && recData2[7] == CRCvalue2[0] && recData2[8] == CRCvalue2[1])
                        {
                            ret = 0;
                        }

                        else
                            return -1;//接收到的数据不完整
                    }

                    else
                    {
                        return -2;//未接收到数据
                    }
                    /////////PulseN
                    var c = BitConverter.GetBytes(LaserPulseN);
                    string temp3 = BitConverter.ToString(c.ToArray()).Replace("-", "");
                    var data9 = temp3.Substring(0, 2);
                    var data10 = temp3.Substring(2, 2);
                    var data11 = temp3.Substring(4, 2);
                    var data12 = temp3.Substring(6, 2);
                    rawdata3 = new byte[7];
                    rawdata3[0] = Convert.ToByte("7F", 16);
                    rawdata3[1] = Convert.ToByte("05", 16);
                    rawdata3[2] = Convert.ToByte("3B", 16);
                    rawdata3[3] = Convert.ToByte(data9, 16);
                    rawdata3[4] = Convert.ToByte(data10, 16);
                    rawdata3[5] = Convert.ToByte(data11, 16);
                    rawdata3[6] = Convert.ToByte(data12, 16);
                    CRCvalue3 = ToModbus(rawdata3);
                    data03 = new byte[9] { rawdata3[0], rawdata3[1], rawdata3[2], rawdata3[3], rawdata3[4], rawdata3[5], rawdata3[6], CRCvalue3[0], CRCvalue3[1] };
                    ret = 0;
                }


            }

            else
            {
                return -3;//激光器未连接
            }
            if (ret == 0)
            {
                return 0;
            }
            else
                return -1;
        }

        //查询当前激光清扫参数
        //public unsafe int LS_4A_GetLaserSweeperPara(double* LaserPwr, double* LaserRepF, int* LaserPulseN)
        //{
        //    if (Power.Equals(-1))
        //    {
        //        return -11;//查询失败
        //    }
        //    else
        //    {
        //        *LaserPwr = Power;
        //        *LaserRepF = Frequency;
        //        *LaserPulseN = PulseCount;
        //        return 0;//查询成功
        //    }
        //}

        //触发Fire模式清扫，将设置脉冲数那一步获得的值发送给设备
        public int LS_4T_Fire()
        {
            if ((SerialPort1 != null) && (SerialPort1.IsOpen))
            {
                try
                {
                    SerialPort1.Write(data03, 0, 9);
                }
                catch (Exception)
                {    // 捕捉异常
                    return -4;
                }
                Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                if (SerialPort1.BytesToRead > 0)
                {
                    byte[] recData = new byte[SerialPort1.BytesToRead];
                    SerialPort1.Read(recData, 0, recData.Length);

                    if (recData.Count() == 9 && recData[7] == CRCvalue3[0] && recData[8] == CRCvalue3[1])
                        return 0;
                    else
                        return -1;//接收到的数据不完整
                }

                else
                {
                    return -2;//未接收到数据
                }
            }
            else
            {
                return -3;
            }
        }

        //查询Fire模式清扫是否完成
        public int LS_4T_GetFireFinish()
        {
            int i = 0;
            int j = 0;
            int firetag = 0;
            int firetag2 = 0;
            while ( i < 10)//循环10次（最长800ms）去检查firestate是否变为1（正在出光状态）
            {
                if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                {
                    rawdata = new byte[3] { 0x5D, 0x01, 0x04 };
                    CRCvalue = ToModbus(rawdata);
                    data = new byte[5] { 0x5D, 0x01, 0x04, CRCvalue[0], CRCvalue[1] };//包含CRC校验的数据，待发送
                    try
                    {
                        SerialPort1.Write(data, 0, 5);
                    }
                    catch (Exception)
                    {    // 捕捉异常
                        return -4;//发送数据失败
                    }
                    Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                    if (SerialPort1.BytesToRead > 0)
                    {
                        byte[] recData = new byte[SerialPort1.BytesToRead];
                        SerialPort1.Read(recData, 0, recData.Length);

                        //Console.WriteLine("======获取到的字节数======：" + recData.Count());
                        if (recData[0] == 0x5D && recData[1] == 0x36 && recData[2] == 0x04 && recData.Count() == 58)//log recData的长度和内容
                        {
                            int firestate = recData[8];//Fire状态反馈
                            if(firestate == 1) 
                            {
                                firetag = 1;
                                break;
                            }
                        }
                        else
                        {
                            return -1;//接收到的数据不完整
                        }
                    }
                    else
                    {
                        return -2;//未接收到数据
                    }
                }
                else
                {
                    return -3;//激光器未连接
                }
                Thread.Sleep(50);
                i++;
            }
            Console.WriteLine("变量i的值为：" + i);
            Console.WriteLine("变量firetag的值为：" + firetag);
            Console.WriteLine("出光时间： " + firetime);
            if (i == 10) //说明轮询结束还是查到Fire状态开启
            {
                return -9;//Fire模式清扫异常
            }
            else
            {
                //DateTime dt = DateTime.Now;
                //int ms = dt.Millisecond;

                //Console.WriteLine("The current time is: " + dt.ToString());

                //Console.WriteLine("Milliseconds of current time: " + ms.ToString());

                Thread.Sleep(firetime*1000);//等待firetime后再去检查firestate是否变为0（待机状态）

                //Console.WriteLine("The current time is: " + dt.ToString());

                //Console.WriteLine("Milliseconds of current time: " + ms.ToString());

                while (j < 200)
                {
                    if ((SerialPort1 != null) && (SerialPort1.IsOpen))
                    {
                        rawdata = new byte[3] { 0x5D, 0x01, 0x04 };
                        CRCvalue = ToModbus(rawdata);
                        data = new byte[5] { 0x5D, 0x01, 0x04, CRCvalue[0], CRCvalue[1] };//包含CRC校验的数据，待发送
                        try
                        {
                            SerialPort1.Write(data, 0, 5);
                        }
                        catch (Exception)
                        {    // 捕捉异常
                            return -4;//发送数据失败
                        }
                        Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                        if (SerialPort1.BytesToRead > 0)
                        {
                            byte[] recData = new byte[SerialPort1.BytesToRead];
                            SerialPort1.Read(recData, 0, recData.Length);

                            Console.WriteLine("======获取到的字节数======：" + recData.Count());
                            if (recData[0] == 0x5D && recData[1] == 0x36 && recData[2] == 0x04 && recData.Count() == 58)//log recData的长度和内容
                            {
                                int firestate = recData[8];//Fire状态反馈
                                if (firestate == 0)
                                {
                                    firetag2 = 1;
                                    break;
                                }
                            }
                            else
                            {
                                return -1;//接收到的数据不完整
                            }
                        }
                        else
                        {
                            return -2;//未接收到数据
                        }
                    }
                    else
                    {
                        return -3;//激光器未连接
                    }
                    Thread.Sleep(80);
                    j++;
                }
                Console.WriteLine("变量j的值为：" + j);
                Console.WriteLine("变量firetag2的值为：" + firetag2);
                if ((firetag == 1) && (firetag2 == 1))
                {
                    return 0;//Fire模式清扫完成

                }
                else
                    return -9;//Fire模式清扫异常
            }
        }

        //查询激光器内部工作/监测的单个整型参数
        //public unsafe int LS_4T_GetLaserOpPara(int cmdID, double* value)
        //{
        //    return 0;
        //}

        //查询激光器状态
        public unsafe int LS_4T_GetLaserState(int* laserState)

        {
            if ((SerialPort1 != null) && (SerialPort1.IsOpen))
            {
                rawdata = new byte[3] { 0x5D, 0x01, 0x04 };
                CRCvalue = ToModbus(rawdata);
                data = new byte[5] { 0x5D, 0x01, 0x04, CRCvalue[0], CRCvalue[1] };//包含CRC校验的数据，待发送

                try
                {
                    SerialPort1.Write(data, 0, 5);
                }
                catch (Exception)
                {    // 捕捉异常
                    return -4;//发送数据失败
                }

                Thread.Sleep(sleeptime); //30ms内数据接收完毕，可根据实际情况调整
                if (SerialPort1.BytesToRead > 0)
                {
                    byte[] recData = new byte[SerialPort1.BytesToRead];
                    SerialPort1.Read(recData, 0, recData.Length);

                    Console.WriteLine("======获取到的字节数======：" + recData.Count());
                    if (recData[0] == 0x5D && recData[1] == 0x36 && recData[2] == 0x04 && recData.Count() == 58)//log recData的长度和内容
                    {
                        int ErrorState = recData[4];//系统故障状态
                        Console.WriteLine("系统故障状态：" + ErrorState);
                        int preheat = recData[5];//系统预热状态

                        if (ErrorState != 0)
                        {
                            *laserState = 1;
                            return 1;
                        }
                        else if (preheat == 0)
                        {
                            *laserState = 2;
                            return 2;
                        }
                        else
                        {
                            *laserState = 0;
                            return 0;
                        }                      
                    }
                    else
                    {
                        Console.WriteLine("接收到的数据不完整");
                        return -1;//接收到的数据不完整
                    }
                }
                else
                {
                    return -2;//未接收到数据
                }

            }
            else
            {
                return -3;//激光器未连接
            }
        }

        //连接激光器
        public int LS_4T_ConnectLaser(string COMID)
        {
            if (SerialPort1.IsOpen == true)
            {
                return 0;
            }
            else
            {
                if (COMID.Equals(""))
                {
                    return -12;//串口号未提供
                }
                else
                {
                    try
                    {
                        SerialPort1.PortName = COMID;
                        // 波特率 115200
                        SerialPort1.BaudRate = 115200;
                        // 数据位为 8 位
                        SerialPort1.DataBits = 8;
                        // 停止位为 1 位
                        SerialPort1.StopBits = StopBits.One;
                        // 无奇偶校验位
                        SerialPort1.Parity = Parity.None;
                        // 读取串口超时时间为1000ms
                        SerialPort1.ReadTimeout = 1000;
                        SerialPort1.Open();//打开串口
                        return 0;
                    }
                    catch (Exception)
                    {
                        return -13;//激光器连接失败

                    }
                }
            }
            
        }

        //断开激光器连接
        public int LS_4T_DisConnectLaser()
        {
            if (SerialPort1.IsOpen == false)
            {
                return 0;
            }
            else
            {
                try
                {
                    SerialPort1.Close();//关闭串口
                    return 0;

                }
                catch (Exception)
                {
                    return -14;//断开连接失败
                }
            }
        }

        //初始化激光器
        public int LS_4T_InitLaser(string COMID)
        {
            //连接激光器，并验证成功
            int iReturn = LS_4T_ConnectLaser(COMID);
            if (iReturn != 0)
            { return -13;}
            //设置自动出光（使能开），并验证成功
            int iReturn2 = LS_4T_SetLaserParaI(0, 1);
            if (iReturn2 == -4)
            { return -4; }
            else if (iReturn2 == -1) 
            { return -1; }
            else if (iReturn2 == -2)
            { return -2; }
            else if (iReturn2 == -3)
            { return -3; }
            //设置激光出光模式（默认Fire清扫模式），并验证成功
            int iReturn3 = LS_4T_SetLaserParaI(1, 0);
            if (iReturn3 == -4)
            { return -4; }
            else if (iReturn3 == -1)
            { return -1; }
            else if (iReturn3 == -2)
            { return -2; }
            else if (iReturn3 == -3)
            { return -3; }
            else
            return 0;
        }
    }
}