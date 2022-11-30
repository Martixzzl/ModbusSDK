using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using ModbusSDK.Exceptions;

namespace ModbusSDK
{
    public class ModbusClient : IDisposable
    {
        public int NumberOfRetries { get; set; } = 3;
                
        public event ModbusClient.ReceiveDataChangedHandler ReceiveDataChanged;
              
        public event ModbusClient.SendDataChangedHandler SendDataChanged;
               
        public event ModbusClient.ConnectedChangedHandler ConnectedChanged;
                
        public ModbusClient(string ipAddress, int port)
        {
            bool flag = this.debug;
            if (flag)
            {
                StoreLogData.Instance.Store("ModbusSDK library initialized for Modbus-TCP, IPAddress: " + ipAddress + ", Port: " + port.ToString(), DateTime.Now);
            }
            Console.WriteLine("ModbusSDK Client Library Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Console.WriteLine("Copyright (c) Stefan Rossmann Engineering Solutions");
            Console.WriteLine();
            this.ipAddress = ipAddress;
            this.port = port;
        }
        public ModbusClient(string serialPort)
        {
            bool flag = this.debug;
            if (flag)
            {
                StoreLogData.Instance.Store("ModbusSDK library initialized for Modbus-RTU, COM-Port: " + serialPort, DateTime.Now);
            }
            Console.WriteLine("ModbusSDK Client Library Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Console.WriteLine("Copyright (c) Stefan Rossmann Engineering Solutions");
            Console.WriteLine();
            this.serialport = new SerialPort();
            this.serialport.PortName = serialPort;
            this.serialport.BaudRate = this.baudRate;
            this.serialport.Parity = this.parity;
            this.serialport.StopBits = this.stopBits;
            this.serialport.WriteTimeout = 10000;
            this.serialport.ReadTimeout = this.connectTimeout;
            this.serialport.DataReceived += new SerialDataReceivedEventHandler(this.DataReceivedHandler);
        }

        public ModbusClient()
        {
            bool flag = this.debug;
            if (flag)
            {
                StoreLogData.Instance.Store("ModbusSDK library initialized for Modbus-TCP", DateTime.Now);
            }
            Console.WriteLine("ModbusSDK Client Library Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Console.WriteLine("Copyright (c) Stefan Rossmann Engineering Solutions");
            Console.WriteLine();
        }

        public void Connect()
        {
            bool flag = this.serialport != null;
            if (flag)
            {
                bool flag2 = !this.serialport.IsOpen;
                if (flag2)
                {
                    bool flag3 = this.debug;
                    if (flag3)
                    {
                        StoreLogData.Instance.Store("Open Serial port " + this.serialport.PortName, DateTime.Now);
                    }
                    this.serialport.BaudRate = this.baudRate;
                    this.serialport.Parity = this.parity;
                    this.serialport.StopBits = this.stopBits;
                    this.serialport.WriteTimeout = 10000;
                    this.serialport.ReadTimeout = this.connectTimeout;
                    this.serialport.Open();
                    this.connected = true;
                }
                bool flag4 = this.ConnectedChanged != null;
                if (flag4)
                {
                    try
                    {
                        this.ConnectedChanged(this);
                    }
                    catch
                    {
                    }
                }
            }
            else
            {
                bool flag5 = !this.udpFlag;
                if (flag5)
                {
                    bool flag6 = this.debug;
                    if (flag6)
                    {
                        StoreLogData.Instance.Store("Open TCP-Socket, IP-Address: " + this.ipAddress + ", Port: " + this.port.ToString(), DateTime.Now);
                    }
                    this.tcpClient = new TcpClient();
                    IAsyncResult asyncResult = this.tcpClient.BeginConnect(this.ipAddress, this.port, null, null);
                    bool flag7 = asyncResult.AsyncWaitHandle.WaitOne(this.connectTimeout);
                    bool flag8 = !flag7;
                    if (flag8)
                    {
                        throw new ConnectionException("connection timed out");
                    }
                    this.tcpClient.EndConnect(asyncResult);
                    this.stream = this.tcpClient.GetStream();
                    this.stream.ReadTimeout = this.connectTimeout;
                    this.connected = true;
                }
                else
                {
                    this.tcpClient = new TcpClient();
                    this.connected = true;
                }
                bool flag9 = this.ConnectedChanged != null;
                if (flag9)
                {
                    try
                    {
                        this.ConnectedChanged(this);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public void Connect(string ipAddress, int port)
        {
            bool flag = !this.udpFlag;
            if (flag)
            {
                bool flag2 = this.debug;
                if (flag2)
                {
                    StoreLogData.Instance.Store("Open TCP-Socket, IP-Address: " + ipAddress + ", Port: " + port.ToString(), DateTime.Now);
                }
                this.tcpClient = new TcpClient();
                IAsyncResult asyncResult = this.tcpClient.BeginConnect(ipAddress, port, null, null);
                bool flag3 = asyncResult.AsyncWaitHandle.WaitOne(this.connectTimeout);
                bool flag4 = !flag3;
                if (flag4)
                {
                    throw new ConnectionException("connection timed out");
                }
                this.tcpClient.EndConnect(asyncResult);
                this.stream = this.tcpClient.GetStream();
                this.stream.ReadTimeout = this.connectTimeout;
                this.connected = true;
            }
            else
            {
                this.tcpClient = new TcpClient();
                this.connected = true;
            }
            bool flag5 = this.ConnectedChanged != null;
            if (flag5)
            {
                this.ConnectedChanged(this);
            }
        }

        public static float ConvertRegistersToFloat(int[] registers)
        {
            bool flag = registers.Length != 2;
            if (flag)
            {
                throw new ArgumentException("Input Array length invalid - Array langth must be '2'");
            }
            int num = registers[1];
            int num2 = registers[0];
            byte[] bytes = BitConverter.GetBytes(num);
            byte[] bytes2 = BitConverter.GetBytes(num2);
            byte[] array = new byte[]
            {
                bytes2[0],
                bytes2[1],
                bytes[0],
                bytes[1]
            };
            return BitConverter.ToSingle(array, 0);
        }

        public static float ConvertRegistersToFloat(int[] registers, ModbusClient.RegisterOrder registerOrder)
        {
            int[] registers2 = new int[]
            {
                registers[0],
                registers[1]
            };
            bool flag = registerOrder == ModbusClient.RegisterOrder.HighLow;
            if (flag)
            {
                registers2 = new int[]
                {
                    registers[1],
                    registers[0]
                };
            }
            return ModbusClient.ConvertRegistersToFloat(registers2);
        }

        public static int ConvertRegistersToInt(int[] registers)
        {
            bool flag = registers.Length != 2;
            if (flag)
            {
                throw new ArgumentException("Input Array length invalid - Array langth must be '2'");
            }
            int num = registers[1];
            int num2 = registers[0];
            byte[] bytes = BitConverter.GetBytes(num);
            byte[] bytes2 = BitConverter.GetBytes(num2);
            byte[] array = new byte[]
            {
                bytes2[0],
                bytes2[1],
                bytes[0],
                bytes[1]
            };
            return BitConverter.ToInt32(array, 0);
        }

        public static int ConvertRegistersToInt(int[] registers, ModbusClient.RegisterOrder registerOrder)
        {
            int[] registers2 = new int[]
            {
                registers[0],
                registers[1]
            };
            bool flag = registerOrder == ModbusClient.RegisterOrder.HighLow;
            if (flag)
            {
                registers2 = new int[]
                {
                    registers[1],
                    registers[0]
                };
            }
            return ModbusClient.ConvertRegistersToInt(registers2);
        }

        public static long ConvertRegistersToLong(int[] registers)
        {
            bool flag = registers.Length != 4;
            if (flag)
            {
                throw new ArgumentException("Input Array length invalid - Array langth must be '4'");
            }
            int num = registers[3];
            int num2 = registers[2];
            int num3 = registers[1];
            int num4 = registers[0];
            byte[] bytes = BitConverter.GetBytes(num);
            byte[] bytes2 = BitConverter.GetBytes(num2);
            byte[] bytes3 = BitConverter.GetBytes(num3);
            byte[] bytes4 = BitConverter.GetBytes(num4);
            byte[] array = new byte[]
            {
                bytes4[0],
                bytes4[1],
                bytes3[0],
                bytes3[1],
                bytes2[0],
                bytes2[1],
                bytes[0],
                bytes[1]
            };
            return BitConverter.ToInt64(array, 0);
        }

        public static long ConvertRegistersToLong(int[] registers, ModbusClient.RegisterOrder registerOrder)
        {
            bool flag = registers.Length != 4;
            if (flag)
            {
                throw new ArgumentException("Input Array length invalid - Array langth must be '4'");
            }
            int[] registers2 = new int[]
            {
                registers[0],
                registers[1],
                registers[2],
                registers[3]
            };
            bool flag2 = registerOrder == ModbusClient.RegisterOrder.HighLow;
            if (flag2)
            {
                registers2 = new int[]
                {
                    registers[3],
                    registers[2],
                    registers[1],
                    registers[0]
                };
            }
            return ModbusClient.ConvertRegistersToLong(registers2);
        }

        public static double ConvertRegistersToDouble(int[] registers)
        {
            bool flag = registers.Length != 4;
            if (flag)
            {
                throw new ArgumentException("Input Array length invalid - Array langth must be '4'");
            }
            int num = registers[3];
            int num2 = registers[2];
            int num3 = registers[1];
            int num4 = registers[0];
            byte[] bytes = BitConverter.GetBytes(num);
            byte[] bytes2 = BitConverter.GetBytes(num2);
            byte[] bytes3 = BitConverter.GetBytes(num3);
            byte[] bytes4 = BitConverter.GetBytes(num4);
            byte[] array = new byte[]
            {
                bytes4[0],
                bytes4[1],
                bytes3[0],
                bytes3[1],
                bytes2[0],
                bytes2[1],
                bytes[0],
                bytes[1]
            };
            return BitConverter.ToDouble(array, 0);
        }

        public static double ConvertRegistersToDouble(int[] registers, ModbusClient.RegisterOrder registerOrder)
        {
            bool flag = registers.Length != 4;
            if (flag)
            {
                throw new ArgumentException("Input Array length invalid - Array langth must be '4'");
            }
            int[] registers2 = new int[]
            {
                registers[0],
                registers[1],
                registers[2],
                registers[3]
            };
            bool flag2 = registerOrder == ModbusClient.RegisterOrder.HighLow;
            if (flag2)
            {
                registers2 = new int[]
                {
                    registers[3],
                    registers[2],
                    registers[1],
                    registers[0]
                };
            }
            return ModbusClient.ConvertRegistersToDouble(registers2);
        }

        public static int[] ConvertFloatToRegisters(float floatValue)
        {
            byte[] bytes = BitConverter.GetBytes(floatValue);
            byte[] array = new byte[4];
            array[0] = bytes[2];
            array[1] = bytes[3];
            byte[] array2 = array;
            byte[] array3 = new byte[4];
            array3[0] = bytes[0];
            array3[1] = bytes[1];
            byte[] array4 = array3;
            return new int[]
            {
                BitConverter.ToInt32(array4, 0),
                BitConverter.ToInt32(array2, 0)
            };
        }

        public static int[] ConvertFloatToRegisters(float floatValue, ModbusClient.RegisterOrder registerOrder)
        {
            int[] array = ModbusClient.ConvertFloatToRegisters(floatValue);
            int[] result = array;
            bool flag = registerOrder == ModbusClient.RegisterOrder.HighLow;
            if (flag)
            {
                result = new int[]
                {
                    array[1],
                    array[0]
                };
            }
            return result;
        }

        public static int[] ConvertIntToRegisters(int intValue)
        {
            byte[] bytes = BitConverter.GetBytes(intValue);
            byte[] array = new byte[4];
            array[0] = bytes[2];
            array[1] = bytes[3];
            byte[] array2 = array;
            byte[] array3 = new byte[4];
            array3[0] = bytes[0];
            array3[1] = bytes[1];
            byte[] array4 = array3;
            return new int[]
            {
                BitConverter.ToInt32(array4, 0),
                BitConverter.ToInt32(array2, 0)
            };
        }

        public static int[] ConvertIntToRegisters(int intValue, ModbusClient.RegisterOrder registerOrder)
        {
            int[] array = ModbusClient.ConvertIntToRegisters(intValue);
            int[] result = array;
            bool flag = registerOrder == ModbusClient.RegisterOrder.HighLow;
            if (flag)
            {
                result = new int[]
                {
                    array[1],
                    array[0]
                };
            }
            return result;
        }

        public static int[] ConvertLongToRegisters(long longValue)
        {
            byte[] bytes = BitConverter.GetBytes(longValue);
            byte[] array = new byte[4];
            array[0] = bytes[6];
            array[1] = bytes[7];
            byte[] array2 = array;
            byte[] array3 = new byte[4];
            array3[0] = bytes[4];
            array3[1] = bytes[5];
            byte[] array4 = array3;
            byte[] array5 = new byte[4];
            array5[0] = bytes[2];
            array5[1] = bytes[3];
            byte[] array6 = array5;
            byte[] array7 = new byte[4];
            array7[0] = bytes[0];
            array7[1] = bytes[1];
            byte[] array8 = array7;
            return new int[]
            {
                BitConverter.ToInt32(array8, 0),
                BitConverter.ToInt32(array6, 0),
                BitConverter.ToInt32(array4, 0),
                BitConverter.ToInt32(array2, 0)
            };
        }

        public static int[] ConvertLongToRegisters(long longValue, ModbusClient.RegisterOrder registerOrder)
        {
            int[] array = ModbusClient.ConvertLongToRegisters(longValue);
            int[] result = array;
            bool flag = registerOrder == ModbusClient.RegisterOrder.HighLow;
            if (flag)
            {
                result = new int[]
                {
                    array[3],
                    array[2],
                    array[1],
                    array[0]
                };
            }
            return result;
        }

        public static int[] ConvertDoubleToRegisters(double doubleValue)
        {
            byte[] bytes = BitConverter.GetBytes(doubleValue);
            byte[] array = new byte[4];
            array[0] = bytes[6];
            array[1] = bytes[7];
            byte[] array2 = array;
            byte[] array3 = new byte[4];
            array3[0] = bytes[4];
            array3[1] = bytes[5];
            byte[] array4 = array3;
            byte[] array5 = new byte[4];
            array5[0] = bytes[2];
            array5[1] = bytes[3];
            byte[] array6 = array5;
            byte[] array7 = new byte[4];
            array7[0] = bytes[0];
            array7[1] = bytes[1];
            byte[] array8 = array7;
            return new int[]
            {
                BitConverter.ToInt32(array8, 0),
                BitConverter.ToInt32(array6, 0),
                BitConverter.ToInt32(array4, 0),
                BitConverter.ToInt32(array2, 0)
            };
        }

        public static int[] ConvertDoubleToRegisters(double doubleValue, ModbusClient.RegisterOrder registerOrder)
        {
            int[] array = ModbusClient.ConvertDoubleToRegisters(doubleValue);
            int[] result = array;
            bool flag = registerOrder == ModbusClient.RegisterOrder.HighLow;
            if (flag)
            {
                result = new int[]
                {
                    array[3],
                    array[2],
                    array[1],
                    array[0]
                };
            }
            return result;
        }

        public static string ConvertRegistersToString(int[] registers, int offset, int stringLength)
        {
            byte[] array = new byte[stringLength];
            byte[] array2 = new byte[2];
            checked
            {
                for (int i = 0; i < stringLength / 2; i++)
                {
                    array2 = BitConverter.GetBytes(registers[offset + i]);
                    array[i * 2] = array2[0];
                    array[i * 2 + 1] = array2[1];
                }
                return Encoding.Default.GetString(array);
            }
        }

        public static int[] ConvertStringToRegisters(string stringToConvert)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(stringToConvert);
            checked
            {
                int[] array = new int[stringToConvert.Length / 2 + stringToConvert.Length % 2];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = (int)bytes[i * 2];
                    bool flag = i * 2 + 1 < bytes.Length;
                    if (flag)
                    {
                        array[i] |= (int)bytes[i * 2 + 1] << 8;
                    }
                }
                return array;
            }
        }

        public static ushort calculateCRC(byte[] data, ushort numberOfBytes, int startByte)
        {
            byte[] array = new byte[]
            {
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64,
                1,
                192,
                128,
                65,
                1,
                192,
                128,
                65,
                0,
                193,
                129,
                64
            };
            byte[] array2 = new byte[]
            {
                0,
                192,
                193,
                1,
                195,
                3,
                2,
                194,
                198,
                6,
                7,
                199,
                5,
                197,
                196,
                4,
                204,
                12,
                13,
                205,
                15,
                207,
                206,
                14,
                10,
                202,
                203,
                11,
                201,
                9,
                8,
                200,
                216,
                24,
                25,
                217,
                27,
                219,
                218,
                26,
                30,
                222,
                223,
                31,
                221,
                29,
                28,
                220,
                20,
                212,
                213,
                21,
                215,
                23,
                22,
                214,
                210,
                18,
                19,
                211,
                17,
                209,
                208,
                16,
                240,
                48,
                49,
                241,
                51,
                243,
                242,
                50,
                54,
                246,
                247,
                55,
                245,
                53,
                52,
                244,
                60,
                252,
                253,
                61,
                byte.MaxValue,
                63,
                62,
                254,
                250,
                58,
                59,
                251,
                57,
                249,
                248,
                56,
                40,
                232,
                233,
                41,
                235,
                43,
                42,
                234,
                238,
                46,
                47,
                239,
                45,
                237,
                236,
                44,
                228,
                36,
                37,
                229,
                39,
                231,
                230,
                38,
                34,
                226,
                227,
                35,
                225,
                33,
                32,
                224,
                160,
                96,
                97,
                161,
                99,
                163,
                162,
                98,
                102,
                166,
                167,
                103,
                165,
                101,
                100,
                164,
                108,
                172,
                173,
                109,
                175,
                111,
                110,
                174,
                170,
                106,
                107,
                171,
                105,
                169,
                168,
                104,
                120,
                184,
                185,
                121,
                187,
                123,
                122,
                186,
                190,
                126,
                127,
                191,
                125,
                189,
                188,
                124,
                180,
                116,
                117,
                181,
                119,
                183,
                182,
                118,
                114,
                178,
                179,
                115,
                177,
                113,
                112,
                176,
                80,
                144,
                145,
                81,
                147,
                83,
                82,
                146,
                150,
                86,
                87,
                151,
                85,
                149,
                148,
                84,
                156,
                92,
                93,
                157,
                95,
                159,
                158,
                94,
                90,
                154,
                155,
                91,
                153,
                89,
                88,
                152,
                136,
                72,
                73,
                137,
                75,
                139,
                138,
                74,
                78,
                142,
                143,
                79,
                141,
                77,
                76,
                140,
                68,
                132,
                133,
                69,
                135,
                71,
                70,
                134,
                130,
                66,
                67,
                131,
                65,
                129,
                128,
                64
            };
            ushort num = numberOfBytes;
            byte b = byte.MaxValue;
            byte b2 = byte.MaxValue;
            int num2 = 0;
            checked
            {
                while (num > 0)
                {
                    num -= 1;
                    bool flag = num2 + startByte < data.Length;
                    if (flag)
                    {
                        int num3 = (int)(b2 ^ data[num2 + startByte]);
                        b2 = ((byte)(b ^ array[num3]));
                        b = array2[num3];
                    }
                    num2++;
                }
                return (ushort)((int)b << 8 | (int)b2);
            }
        }

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            this.serialport.DataReceived -= new SerialDataReceivedEventHandler(this.DataReceivedHandler);
            this.receiveActive = true;
            SerialPort serialPort = (SerialPort)sender;
            bool flag = this.bytesToRead == 0;
            checked
            {
                if (flag)
                {
                    serialPort.DiscardInBuffer();
                    this.receiveActive = false;
                    this.serialport.DataReceived += new SerialDataReceivedEventHandler(this.DataReceivedHandler);
                }
                else
                {
                    this.readBuffer = new byte[256];
                    int num = 0;
                    DateTime now = DateTime.Now;
                    do
                    {
                        try
                        {
                            now = DateTime.Now;
                            while (serialPort.BytesToRead == 0)
                            {
                                Thread.Sleep(10);
                                bool flag2 = DateTime.Now.Ticks - now.Ticks > 20000000L;
                                if (flag2)
                                {
                                    break;
                                }
                            }
                            int num2 = serialPort.BytesToRead;
                            byte[] array = new byte[num2];
                            serialPort.Read(array, 0, num2);
                            Array.Copy(array, 0, this.readBuffer, num, (num + array.Length <= this.bytesToRead) ? array.Length : (this.bytesToRead - num));
                            num += array.Length;
                        }
                        catch (Exception)
                        {
                        }
                        bool flag3 = this.bytesToRead <= num;
                        if (flag3)
                        {
                            break;
                        }
                        bool flag4 = ModbusClient.DetectValidModbusFrame(this.readBuffer, (num < this.readBuffer.Length) ? num : this.readBuffer.Length) | this.bytesToRead <= num;
                        if (flag4)
                        {
                            break;
                        }
                    }
                    while (DateTime.Now.Ticks - now.Ticks < 20000000L);
                    this.receiveData = new byte[num];
                    Array.Copy(this.readBuffer, 0, this.receiveData, 0, (num < this.readBuffer.Length) ? num : this.readBuffer.Length);
                    bool flag5 = this.debug;
                    if (flag5)
                    {
                        StoreLogData.Instance.Store("Received Serial-Data: " + BitConverter.ToString(this.readBuffer), DateTime.Now);
                    }
                    this.bytesToRead = 0;
                    this.dataReceived = true;
                    this.receiveActive = false;
                    this.serialport.DataReceived += new SerialDataReceivedEventHandler(this.DataReceivedHandler);
                    bool flag6 = this.ReceiveDataChanged != null;
                    if (flag6)
                    {
                        this.ReceiveDataChanged(this);
                    }
                }
            }
        }

        public static bool DetectValidModbusFrame(byte[] readBuffer, int length)
        {
            bool flag = length < 6;
            checked
            {
                bool result;
                if (flag)
                {
                    result = false;
                }
                else
                {
                    bool flag2 = readBuffer[0] < 1 | readBuffer[0] > 247;
                    if (flag2)
                    {
                        result = false;
                    }
                    else
                    {
                        byte[] array = new byte[2];
                        array = BitConverter.GetBytes(ModbusClient.calculateCRC(readBuffer, (ushort)(length - 2), 0));
                        bool flag3 = array[0] != readBuffer[length - 2] | array[1] != readBuffer[length - 1];
                        result = !flag3;
                    }
                }
                return result;
            }
        }

        public bool[] ReadDiscreteInputs(int startingAddress, int quantity)
        {
            bool flag = this.debug;
            if (flag)
            {
                StoreLogData.Instance.Store("FC2 (Read Discrete Inputs from Master device), StartingAddress: " + startingAddress.ToString() + ", Quantity: " + quantity.ToString(), DateTime.Now);
            }
            checked
            {
                this.transactionIdentifierInternal += 1U;
                bool flag2 = this.serialport != null;
                if (flag2)
                {
                    bool flag3 = !this.serialport.IsOpen;
                    if (flag3)
                    {
                        bool flag4 = this.debug;
                        if (flag4)
                        {
                            StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", DateTime.Now);
                        }
                        throw new SerialPortNotOpenedException("serial port not opened");
                    }
                }
                bool flag5 = this.tcpClient == null & !this.udpFlag & this.serialport == null;
                if (flag5)
                {
                    bool flag6 = this.debug;
                    if (flag6)
                    {
                        StoreLogData.Instance.Store("ConnectionException Throwed", DateTime.Now);
                    }
                    throw new ConnectionException("connection error");
                }
                bool flag7 = startingAddress > 65535 | quantity > 2000;
                if (flag7)
                {
                    bool flag8 = this.debug;
                    if (flag8)
                    {
                        StoreLogData.Instance.Store("ArgumentException Throwed", DateTime.Now);
                    }
                    throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 2000");
                }
                this.transactionIdentifier = BitConverter.GetBytes(this.transactionIdentifierInternal);
                this.protocolIdentifier = BitConverter.GetBytes(0);
                this.length = BitConverter.GetBytes(6);
                this.functionCode = 2;
                this.startingAddress = BitConverter.GetBytes(startingAddress);
                this.quantity = BitConverter.GetBytes(quantity);
                byte[] array = new byte[]
                {
                    this.transactionIdentifier[1],
                    this.transactionIdentifier[0],
                    this.protocolIdentifier[1],
                    this.protocolIdentifier[0],
                    this.length[1],
                    this.length[0],
                    this.unitIdentifier,
                    this.functionCode,
                    this.startingAddress[1],
                    this.startingAddress[0],
                    this.quantity[1],
                    this.quantity[0],
                    this.crc[0],
                    this.crc[1]
                };
                this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array, 6, 6));
                array[12] = this.crc[0];
                array[13] = this.crc[1];
                bool flag9 = this.serialport != null;
                if (flag9)
                {
                    this.dataReceived = false;
                    bool flag10 = quantity % 8 == 0;
                    if (flag10)
                    {
                        this.bytesToRead = 5 + quantity / 8;
                    }
                    else
                    {
                        this.bytesToRead = 6 + quantity / 8;
                    }
                    this.serialport.Write(array, 6, 8);
                    bool flag11 = this.debug;
                    if (flag11)
                    {
                        byte[] array2 = new byte[8];
                        Array.Copy(array, 6, array2, 0, 8);
                        bool flag12 = this.debug;
                        if (flag12)
                        {
                            StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array2), DateTime.Now);
                        }
                    }
                    bool flag13 = this.SendDataChanged != null;
                    if (flag13)
                    {
                        this.sendData = new byte[8];
                        Array.Copy(array, 6, this.sendData, 0, 8);
                        this.SendDataChanged(this);
                    }
                    array = new byte[2100];
                    this.readBuffer = new byte[256];
                    DateTime now = DateTime.Now;
                    byte b = byte.MaxValue;
                    while (b != this.unitIdentifier & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                    {
                        while (!this.dataReceived & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                        {
                            Thread.Sleep(1);
                        }
                        array = new byte[2100];
                        Array.Copy(this.readBuffer, 0, array, 6, this.readBuffer.Length);
                        b = array[6];
                    }
                    bool flag14 = b != this.unitIdentifier;
                    if (flag14)
                    {
                        array = new byte[2100];
                    }
                    else
                    {
                        this.countRetries = 0;
                    }
                }
                else
                {
                    bool flag15 = this.tcpClient.Client.Connected | this.udpFlag;
                    if (flag15)
                    {
                        bool flag16 = this.udpFlag;
                        if (flag16)
                        {
                            UdpClient udpClient = new UdpClient();
                            IPEndPoint ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.port);
                            udpClient.Send(array, array.Length - 2, ipendPoint);
                            this.portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                            udpClient.Client.ReceiveTimeout = 5000;
                            ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.portOut);
                            array = udpClient.Receive(ref ipendPoint);
                        }
                        else
                        {
                            this.stream.Write(array, 0, array.Length - 2);
                            bool flag17 = this.debug;
                            if (flag17)
                            {
                                byte[] array3 = new byte[array.Length - 2];
                                Array.Copy(array, 0, array3, 0, array.Length - 2);
                                bool flag18 = this.debug;
                                if (flag18)
                                {
                                    StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(array3), DateTime.Now);
                                }
                            }
                            bool flag19 = this.SendDataChanged != null;
                            if (flag19)
                            {
                                this.sendData = new byte[array.Length - 2];
                                Array.Copy(array, 0, this.sendData, 0, array.Length - 2);
                                this.SendDataChanged(this);
                            }
                            array = new byte[2100];
                            int num = this.stream.Read(array, 0, array.Length);
                            bool flag20 = this.ReceiveDataChanged != null;
                            if (flag20)
                            {
                                this.receiveData = new byte[num];
                                Array.Copy(array, 0, this.receiveData, 0, num);
                                bool flag21 = this.debug;
                                if (flag21)
                                {
                                    StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(this.receiveData), DateTime.Now);
                                }
                                this.ReceiveDataChanged(this);
                            }
                        }
                    }
                }
                bool flag22 = array[7] == 130 & array[8] == 1;
                if (flag22)
                {
                    bool flag23 = this.debug;
                    if (flag23)
                    {
                        StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", DateTime.Now);
                    }
                    throw new FunctionCodeNotSupportedException("Function code not supported by master");
                }
                bool flag24 = array[7] == 130 & array[8] == 2;
                if (flag24)
                {
                    bool flag25 = this.debug;
                    if (flag25)
                    {
                        StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", DateTime.Now);
                    }
                    throw new StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
                }
                bool flag26 = array[7] == 130 & array[8] == 3;
                if (flag26)
                {
                    bool flag27 = this.debug;
                    if (flag27)
                    {
                        StoreLogData.Instance.Store("QuantityInvalidException Throwed", DateTime.Now);
                    }
                    throw new QuantityInvalidException("quantity invalid");
                }
                bool flag28 = array[7] == 130 & array[8] == 4;
                if (flag28)
                {
                    bool flag29 = this.debug;
                    if (flag29)
                    {
                        StoreLogData.Instance.Store("ModbusException Throwed", DateTime.Now);
                    }
                    throw new ModbusException("error reading");
                }
                bool flag30 = this.serialport != null;
                if (flag30)
                {
                    this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array, (ushort)(array[8] + 3), 6));
                    bool flag31 = (this.crc[0] != array[(int)(array[8] + 9)] | this.crc[1] != array[(int)(array[8] + 10)]) & this.dataReceived;
                    if (flag31)
                    {
                        bool flag32 = this.debug;
                        if (flag32)
                        {
                            StoreLogData.Instance.Store("CRCCheckFailedException Throwed", DateTime.Now);
                        }
                        bool flag33 = this.NumberOfRetries <= this.countRetries;
                        if (flag33)
                        {
                            this.countRetries = 0;
                            throw new CRCCheckFailedException("Response CRC check failed");
                        }
                        this.countRetries++;
                        return this.ReadDiscreteInputs(startingAddress, quantity);
                    }
                    else
                    {
                        bool flag34 = !this.dataReceived;
                        if (flag34)
                        {
                            bool flag35 = this.debug;
                            if (flag35)
                            {
                                StoreLogData.Instance.Store("TimeoutException Throwed", DateTime.Now);
                            }
                            bool flag36 = this.NumberOfRetries <= this.countRetries;
                            if (flag36)
                            {
                                this.countRetries = 0;
                                throw new TimeoutException("No Response from Modbus Slave");
                            }
                            this.countRetries++;
                            return this.ReadDiscreteInputs(startingAddress, quantity);
                        }
                    }
                }
                bool[] array4 = new bool[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    int num2 = (int)array[9 + i / 8];
                    int num3 = Convert.ToInt32(Math.Pow(2.0, (double)(i % 8)));
                    array4[i] = Convert.ToBoolean((num2 & num3) / num3);
                }
                return array4;
            }
        }

        public bool[] ReadCoils(int startingAddress, int quantity)
        {
            bool flag = this.debug;
            if (flag)
            {
                StoreLogData.Instance.Store("FC1 (Read Coils from Master device), StartingAddress: " + startingAddress.ToString() + ", Quantity: " + quantity.ToString(), DateTime.Now);
            }
            checked
            {
                this.transactionIdentifierInternal += 1U;
                bool flag2 = this.serialport != null;
                if (flag2)
                {
                    bool flag3 = !this.serialport.IsOpen;
                    if (flag3)
                    {
                        bool flag4 = this.debug;
                        if (flag4)
                        {
                            StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", DateTime.Now);
                        }
                        throw new SerialPortNotOpenedException("serial port not opened");
                    }
                }
                bool flag5 = this.tcpClient == null & !this.udpFlag & this.serialport == null;
                if (flag5)
                {
                    bool flag6 = this.debug;
                    if (flag6)
                    {
                        StoreLogData.Instance.Store("ConnectionException Throwed", DateTime.Now);
                    }
                    throw new ConnectionException("connection error");
                }
                bool flag7 = startingAddress > 65535 | quantity > 2000;
                if (flag7)
                {
                    bool flag8 = this.debug;
                    if (flag8)
                    {
                        StoreLogData.Instance.Store("ArgumentException Throwed", DateTime.Now);
                    }
                    throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 2000");
                }
                this.transactionIdentifier = BitConverter.GetBytes(this.transactionIdentifierInternal);
                this.protocolIdentifier = BitConverter.GetBytes(0);
                this.length = BitConverter.GetBytes(6);
                this.functionCode = 1;
                this.startingAddress = BitConverter.GetBytes(startingAddress);
                this.quantity = BitConverter.GetBytes(quantity);
                byte[] array = new byte[]
                {
                    this.transactionIdentifier[1],
                    this.transactionIdentifier[0],
                    this.protocolIdentifier[1],
                    this.protocolIdentifier[0],
                    this.length[1],
                    this.length[0],
                    this.unitIdentifier,
                    this.functionCode,
                    this.startingAddress[1],
                    this.startingAddress[0],
                    this.quantity[1],
                    this.quantity[0],
                    this.crc[0],
                    this.crc[1]
                };
                this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array, 6, 6));
                array[12] = this.crc[0];
                array[13] = this.crc[1];
                bool flag9 = this.serialport != null;
                if (flag9)
                {
                    this.dataReceived = false;
                    bool flag10 = quantity % 8 == 0;
                    if (flag10)
                    {
                        this.bytesToRead = 5 + quantity / 8;
                    }
                    else
                    {
                        this.bytesToRead = 6 + quantity / 8;
                    }
                    this.serialport.Write(array, 6, 8);
                    bool flag11 = this.debug;
                    if (flag11)
                    {
                        byte[] array2 = new byte[8];
                        Array.Copy(array, 6, array2, 0, 8);
                        bool flag12 = this.debug;
                        if (flag12)
                        {
                            StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array2), DateTime.Now);
                        }
                    }
                    bool flag13 = this.SendDataChanged != null;
                    if (flag13)
                    {
                        this.sendData = new byte[8];
                        Array.Copy(array, 6, this.sendData, 0, 8);
                        this.SendDataChanged(this);
                    }
                    array = new byte[2100];
                    this.readBuffer = new byte[256];
                    DateTime now = DateTime.Now;
                    byte b = byte.MaxValue;
                    while (b != this.unitIdentifier & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                    {
                        while (!this.dataReceived & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                        {
                            Thread.Sleep(1);
                        }
                        array = new byte[2100];
                        Array.Copy(this.readBuffer, 0, array, 6, this.readBuffer.Length);
                        b = array[6];
                    }
                    bool flag14 = b != this.unitIdentifier;
                    if (flag14)
                    {
                        array = new byte[2100];
                    }
                    else
                    {
                        this.countRetries = 0;
                    }
                }
                else
                {
                    bool flag15 = this.tcpClient.Client.Connected | this.udpFlag;
                    if (flag15)
                    {
                        bool flag16 = this.udpFlag;
                        if (flag16)
                        {
                            UdpClient udpClient = new UdpClient();
                            IPEndPoint ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.port);
                            udpClient.Send(array, array.Length - 2, ipendPoint);
                            this.portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                            udpClient.Client.ReceiveTimeout = 5000;
                            ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.portOut);
                            array = udpClient.Receive(ref ipendPoint);
                        }
                        else
                        {
                            this.stream.Write(array, 0, array.Length - 2);
                            bool flag17 = this.debug;
                            if (flag17)
                            {
                                byte[] array3 = new byte[array.Length - 2];
                                Array.Copy(array, 0, array3, 0, array.Length - 2);
                                bool flag18 = this.debug;
                                if (flag18)
                                {
                                    StoreLogData.Instance.Store("Send MocbusTCP-Data: " + BitConverter.ToString(array3), DateTime.Now);
                                }
                            }
                            bool flag19 = this.SendDataChanged != null;
                            if (flag19)
                            {
                                this.sendData = new byte[array.Length - 2];
                                Array.Copy(array, 0, this.sendData, 0, array.Length - 2);
                                this.SendDataChanged(this);
                            }
                            array = new byte[2100];
                            int num = this.stream.Read(array, 0, array.Length);
                            bool flag20 = this.ReceiveDataChanged != null;
                            if (flag20)
                            {
                                this.receiveData = new byte[num];
                                Array.Copy(array, 0, this.receiveData, 0, num);
                                bool flag21 = this.debug;
                                if (flag21)
                                {
                                    StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(this.receiveData), DateTime.Now);
                                }
                                this.ReceiveDataChanged(this);
                            }
                        }
                    }
                }
                bool flag22 = array[7] == 129 & array[8] == 1;
                if (flag22)
                {
                    bool flag23 = this.debug;
                    if (flag23)
                    {
                        StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", DateTime.Now);
                    }
                    throw new FunctionCodeNotSupportedException("Function code not supported by master");
                }
                bool flag24 = array[7] == 129 & array[8] == 2;
                if (flag24)
                {
                    bool flag25 = this.debug;
                    if (flag25)
                    {
                        StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", DateTime.Now);
                    }
                    throw new StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
                }
                bool flag26 = array[7] == 129 & array[8] == 3;
                if (flag26)
                {
                    bool flag27 = this.debug;
                    if (flag27)
                    {
                        StoreLogData.Instance.Store("QuantityInvalidException Throwed", DateTime.Now);
                    }
                    throw new QuantityInvalidException("quantity invalid");
                }
                bool flag28 = array[7] == 129 & array[8] == 4;
                if (flag28)
                {
                    bool flag29 = this.debug;
                    if (flag29)
                    {
                        StoreLogData.Instance.Store("ModbusException Throwed", DateTime.Now);
                    }
                    throw new ModbusException("error reading");
                }
                bool flag30 = this.serialport != null;
                if (flag30)
                {
                    this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array, (ushort)(array[8] + 3), 6));
                    bool flag31 = (this.crc[0] != array[(int)(array[8] + 9)] | this.crc[1] != array[(int)(array[8] + 10)]) & this.dataReceived;
                    if (flag31)
                    {
                        bool flag32 = this.debug;
                        if (flag32)
                        {
                            StoreLogData.Instance.Store("CRCCheckFailedException Throwed", DateTime.Now);
                        }
                        bool flag33 = this.NumberOfRetries <= this.countRetries;
                        if (flag33)
                        {
                            this.countRetries = 0;
                            throw new CRCCheckFailedException("Response CRC check failed");
                        }
                        this.countRetries++;
                        return this.ReadCoils(startingAddress, quantity);
                    }
                    else
                    {
                        bool flag34 = !this.dataReceived;
                        if (flag34)
                        {
                            bool flag35 = this.debug;
                            if (flag35)
                            {
                                StoreLogData.Instance.Store("TimeoutException Throwed", DateTime.Now);
                            }
                            bool flag36 = this.NumberOfRetries <= this.countRetries;
                            if (flag36)
                            {
                                this.countRetries = 0;
                                throw new TimeoutException("No Response from Modbus Slave");
                            }
                            this.countRetries++;
                            return this.ReadCoils(startingAddress, quantity);
                        }
                    }
                }
                bool[] array4 = new bool[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    int num2 = (int)array[9 + i / 8];
                    int num3 = Convert.ToInt32(Math.Pow(2.0, (double)(i % 8)));
                    array4[i] = Convert.ToBoolean((num2 & num3) / num3);
                }
                return array4;
            }
        }

        public int[] ReadHoldingRegisters(int startingAddress, int quantity)
        {
            bool flag = this.debug;
            if (flag)
            {
                StoreLogData.Instance.Store("FC3 (Read Holding Registers from Master device), StartingAddress: " + startingAddress.ToString() + ", Quantity: " + quantity.ToString(), DateTime.Now);
            }
            checked
            {
                this.transactionIdentifierInternal += 1U;
                bool flag2 = this.serialport != null;
                if (flag2)
                {
                    bool flag3 = !this.serialport.IsOpen;
                    if (flag3)
                    {
                        bool flag4 = this.debug;
                        if (flag4)
                        {
                            StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", DateTime.Now);
                        }
                        throw new SerialPortNotOpenedException("serial port not opened");
                    }
                }
                bool flag5 = this.tcpClient == null & !this.udpFlag & this.serialport == null;
                if (flag5)
                {
                    bool flag6 = this.debug;
                    if (flag6)
                    {
                        StoreLogData.Instance.Store("ConnectionException Throwed", DateTime.Now);
                    }
                    throw new ConnectionException("connection error");
                }
                bool flag7 = startingAddress > 65535 | quantity > 125;
                if (flag7)
                {
                    bool flag8 = this.debug;
                    if (flag8)
                    {
                        StoreLogData.Instance.Store("ArgumentException Throwed", DateTime.Now);
                    }
                    throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 125");
                }
                this.transactionIdentifier = BitConverter.GetBytes(this.transactionIdentifierInternal);
                this.protocolIdentifier = BitConverter.GetBytes(0);
                this.length = BitConverter.GetBytes(6);
                this.functionCode = 3;
                this.startingAddress = BitConverter.GetBytes(startingAddress);
                this.quantity = BitConverter.GetBytes(quantity);
                byte[] array = new byte[]
                {
                    this.transactionIdentifier[1],
                    this.transactionIdentifier[0],
                    this.protocolIdentifier[1],
                    this.protocolIdentifier[0],
                    this.length[1],
                    this.length[0],
                    this.unitIdentifier,
                    this.functionCode,
                    this.startingAddress[1],
                    this.startingAddress[0],
                    this.quantity[1],
                    this.quantity[0],
                    this.crc[0],
                    this.crc[1]
                };
                this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array, 6, 6));
                array[12] = this.crc[0];
                array[13] = this.crc[1];
                bool flag9 = this.serialport != null;
                if (flag9)
                {
                    this.dataReceived = false;
                    this.bytesToRead = 5 + 2 * quantity;
                    this.serialport.Write(array, 6, 8);
                    bool flag10 = this.debug;
                    if (flag10)
                    {
                        byte[] array2 = new byte[8];
                        Array.Copy(array, 6, array2, 0, 8);
                        bool flag11 = this.debug;
                        if (flag11)
                        {
                            StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array2), DateTime.Now);
                        }
                    }
                    bool flag12 = this.SendDataChanged != null;
                    if (flag12)
                    {
                        this.sendData = new byte[8];
                        Array.Copy(array, 6, this.sendData, 0, 8);
                        this.SendDataChanged(this);
                    }
                    array = new byte[2100];
                    this.readBuffer = new byte[256];
                    DateTime now = DateTime.Now;
                    byte b = byte.MaxValue;
                    while (b != this.unitIdentifier & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                    {
                        while (!this.dataReceived & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                        {
                            Thread.Sleep(1);
                        }
                        array = new byte[2100];
                        Array.Copy(this.readBuffer, 0, array, 6, this.readBuffer.Length);
                        b = array[6];
                    }
                    bool flag13 = b != this.unitIdentifier;
                    if (flag13)
                    {
                        array = new byte[2100];
                    }
                    else
                    {
                        this.countRetries = 0;
                    }
                }
                else
                {
                    bool flag14 = this.tcpClient.Client.Connected | this.udpFlag;
                    if (flag14)
                    {
                        bool flag15 = this.udpFlag;
                        if (flag15)
                        {
                            UdpClient udpClient = new UdpClient();
                            IPEndPoint ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.port);
                            udpClient.Send(array, array.Length - 2, ipendPoint);
                            this.portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                            udpClient.Client.ReceiveTimeout = 5000;
                            ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.portOut);
                            array = udpClient.Receive(ref ipendPoint);
                        }
                        else
                        {
                            this.stream.Write(array, 0, array.Length - 2);
                            bool flag16 = this.debug;
                            if (flag16)
                            {
                                byte[] array3 = new byte[array.Length - 2];
                                Array.Copy(array, 0, array3, 0, array.Length - 2);
                                bool flag17 = this.debug;
                                if (flag17)
                                {
                                    StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(array3), DateTime.Now);
                                }
                            }
                            bool flag18 = this.SendDataChanged != null;
                            if (flag18)
                            {
                                this.sendData = new byte[array.Length - 2];
                                Array.Copy(array, 0, this.sendData, 0, array.Length - 2);
                                this.SendDataChanged(this);
                            }
                            array = new byte[256];
                            int num = this.stream.Read(array, 0, array.Length);
                            bool flag19 = this.ReceiveDataChanged != null;
                            if (flag19)
                            {
                                this.receiveData = new byte[num];
                                Array.Copy(array, 0, this.receiveData, 0, num);
                                bool flag20 = this.debug;
                                if (flag20)
                                {
                                    StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(this.receiveData), DateTime.Now);
                                }
                                this.ReceiveDataChanged(this);
                            }
                        }
                    }
                }
                bool flag21 = array[7] == 131 & array[8] == 1;
                if (flag21)
                {
                    bool flag22 = this.debug;
                    if (flag22)
                    {
                        StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", DateTime.Now);
                    }
                    throw new FunctionCodeNotSupportedException("Function code not supported by master");
                }
                bool flag23 = array[7] == 131 & array[8] == 2;
                if (flag23)
                {
                    bool flag24 = this.debug;
                    if (flag24)
                    {
                        StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", DateTime.Now);
                    }
                    throw new StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
                }
                bool flag25 = array[7] == 131 & array[8] == 3;
                if (flag25)
                {
                    bool flag26 = this.debug;
                    if (flag26)
                    {
                        StoreLogData.Instance.Store("QuantityInvalidException Throwed", DateTime.Now);
                    }
                    throw new QuantityInvalidException("quantity invalid");
                }
                bool flag27 = array[7] == 131 & array[8] == 4;
                if (flag27)
                {
                    bool flag28 = this.debug;
                    if (flag28)
                    {
                        StoreLogData.Instance.Store("ModbusException Throwed", DateTime.Now);
                    }
                    throw new ModbusException("error reading");
                }
                bool flag29 = this.serialport != null;
                if (flag29)
                {
                    this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array, (ushort)(array[8] + 3), 6));
                    bool flag30 = (this.crc[0] != array[(int)(array[8] + 9)] | this.crc[1] != array[(int)(array[8] + 10)]) & this.dataReceived;
                    if (flag30)
                    {
                        bool flag31 = this.debug;
                        if (flag31)
                        {
                            StoreLogData.Instance.Store("CRCCheckFailedException Throwed", DateTime.Now);
                        }
                        bool flag32 = this.NumberOfRetries <= this.countRetries;
                        if (flag32)
                        {
                            this.countRetries = 0;
                            throw new CRCCheckFailedException("Response CRC check failed");
                        }
                        this.countRetries++;
                        return this.ReadHoldingRegisters(startingAddress, quantity);
                    }
                    else
                    {
                        bool flag33 = !this.dataReceived;
                        if (flag33)
                        {
                            bool flag34 = this.debug;
                            if (flag34)
                            {
                                StoreLogData.Instance.Store("TimeoutException Throwed", DateTime.Now);
                            }
                            bool flag35 = this.NumberOfRetries <= this.countRetries;
                            if (flag35)
                            {
                                this.countRetries = 0;
                                throw new TimeoutException("No Response from Modbus Slave");
                            }
                            this.countRetries++;
                            return this.ReadHoldingRegisters(startingAddress, quantity);
                        }
                    }
                }
                int[] array4 = new int[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    byte b2 = array[9 + i * 2];
                    byte b3 = array[9 + i * 2 + 1];
                    array[9 + i * 2] = b3;
                    array[9 + i * 2 + 1] = b2;
                    array4[i] = (int)BitConverter.ToInt16(array, 9 + i * 2);
                }
                return array4;
            }
        }

        public int[] ReadInputRegisters(int startingAddress, int quantity)
        {
            bool flag = this.debug;
            if (flag)
            {
                StoreLogData.Instance.Store("FC4 (Read Input Registers from Master device), StartingAddress: " + startingAddress.ToString() + ", Quantity: " + quantity.ToString(), DateTime.Now);
            }
            checked
            {
                this.transactionIdentifierInternal += 1U;
                bool flag2 = this.serialport != null;
                if (flag2)
                {
                    bool flag3 = !this.serialport.IsOpen;
                    if (flag3)
                    {
                        bool flag4 = this.debug;
                        if (flag4)
                        {
                            StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", DateTime.Now);
                        }
                        throw new SerialPortNotOpenedException("serial port not opened");
                    }
                }
                bool flag5 = this.tcpClient == null & !this.udpFlag & this.serialport == null;
                if (flag5)
                {
                    bool flag6 = this.debug;
                    if (flag6)
                    {
                        StoreLogData.Instance.Store("ConnectionException Throwed", DateTime.Now);
                    }
                    throw new ConnectionException("connection error");
                }
                bool flag7 = startingAddress > 65535 | quantity > 125;
                if (flag7)
                {
                    bool flag8 = this.debug;
                    if (flag8)
                    {
                        StoreLogData.Instance.Store("ArgumentException Throwed", DateTime.Now);
                    }
                    throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 125");
                }
                this.transactionIdentifier = BitConverter.GetBytes(this.transactionIdentifierInternal);
                this.protocolIdentifier = BitConverter.GetBytes(0);
                this.length = BitConverter.GetBytes(6);
                this.functionCode = 4;
                this.startingAddress = BitConverter.GetBytes(startingAddress);
                this.quantity = BitConverter.GetBytes(quantity);
                byte[] array = new byte[]
                {
                    this.transactionIdentifier[1],
                    this.transactionIdentifier[0],
                    this.protocolIdentifier[1],
                    this.protocolIdentifier[0],
                    this.length[1],
                    this.length[0],
                    this.unitIdentifier,
                    this.functionCode,
                    this.startingAddress[1],
                    this.startingAddress[0],
                    this.quantity[1],
                    this.quantity[0],
                    this.crc[0],
                    this.crc[1]
                };
                this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array, 6, 6));
                array[12] = this.crc[0];
                array[13] = this.crc[1];
                bool flag9 = this.serialport != null;
                if (flag9)
                {
                    this.dataReceived = false;
                    this.bytesToRead = 5 + 2 * quantity;
                    this.serialport.Write(array, 6, 8);
                    bool flag10 = this.debug;
                    if (flag10)
                    {
                        byte[] array2 = new byte[8];
                        Array.Copy(array, 6, array2, 0, 8);
                        bool flag11 = this.debug;
                        if (flag11)
                        {
                            StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array2), DateTime.Now);
                        }
                    }
                    bool flag12 = this.SendDataChanged != null;
                    if (flag12)
                    {
                        this.sendData = new byte[8];
                        Array.Copy(array, 6, this.sendData, 0, 8);
                        this.SendDataChanged(this);
                    }
                    array = new byte[2100];
                    this.readBuffer = new byte[256];
                    DateTime now = DateTime.Now;
                    byte b = byte.MaxValue;
                    while (b != this.unitIdentifier & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                    {
                        while (!this.dataReceived & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                        {
                            Thread.Sleep(1);
                        }
                        array = new byte[2100];
                        Array.Copy(this.readBuffer, 0, array, 6, this.readBuffer.Length);
                        b = array[6];
                    }
                    bool flag13 = b != this.unitIdentifier;
                    if (flag13)
                    {
                        array = new byte[2100];
                    }
                    else
                    {
                        this.countRetries = 0;
                    }
                }
                else
                {
                    bool flag14 = this.tcpClient.Client.Connected | this.udpFlag;
                    if (flag14)
                    {
                        bool flag15 = this.udpFlag;
                        if (flag15)
                        {
                            UdpClient udpClient = new UdpClient();
                            IPEndPoint ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.port);
                            udpClient.Send(array, array.Length - 2, ipendPoint);
                            this.portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                            udpClient.Client.ReceiveTimeout = 5000;
                            ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.portOut);
                            array = udpClient.Receive(ref ipendPoint);
                        }
                        else
                        {
                            this.stream.Write(array, 0, array.Length - 2);
                            bool flag16 = this.debug;
                            if (flag16)
                            {
                                byte[] array3 = new byte[array.Length - 2];
                                Array.Copy(array, 0, array3, 0, array.Length - 2);
                                bool flag17 = this.debug;
                                if (flag17)
                                {
                                    StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(array3), DateTime.Now);
                                }
                            }
                            bool flag18 = this.SendDataChanged != null;
                            if (flag18)
                            {
                                this.sendData = new byte[array.Length - 2];
                                Array.Copy(array, 0, this.sendData, 0, array.Length - 2);
                                this.SendDataChanged(this);
                            }
                            array = new byte[2100];
                            int num = this.stream.Read(array, 0, array.Length);
                            bool flag19 = this.ReceiveDataChanged != null;
                            if (flag19)
                            {
                                this.receiveData = new byte[num];
                                Array.Copy(array, 0, this.receiveData, 0, num);
                                bool flag20 = this.debug;
                                if (flag20)
                                {
                                    StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(this.receiveData), DateTime.Now);
                                }
                                this.ReceiveDataChanged(this);
                            }
                        }
                    }
                }
                bool flag21 = array[7] == 132 & array[8] == 1;
                if (flag21)
                {
                    bool flag22 = this.debug;
                    if (flag22)
                    {
                        StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", DateTime.Now);
                    }
                    throw new FunctionCodeNotSupportedException("Function code not supported by master");
                }
                bool flag23 = array[7] == 132 & array[8] == 2;
                if (flag23)
                {
                    bool flag24 = this.debug;
                    if (flag24)
                    {
                        StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", DateTime.Now);
                    }
                    throw new StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
                }
                bool flag25 = array[7] == 132 & array[8] == 3;
                if (flag25)
                {
                    bool flag26 = this.debug;
                    if (flag26)
                    {
                        StoreLogData.Instance.Store("QuantityInvalidException Throwed", DateTime.Now);
                    }
                    throw new QuantityInvalidException("quantity invalid");
                }
                bool flag27 = array[7] == 132 & array[8] == 4;
                if (flag27)
                {
                    bool flag28 = this.debug;
                    if (flag28)
                    {
                        StoreLogData.Instance.Store("ModbusException Throwed", DateTime.Now);
                    }
                    throw new ModbusException("error reading");
                }
                bool flag29 = this.serialport != null;
                if (flag29)
                {
                    this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array, (ushort)(array[8] + 3), 6));
                    bool flag30 = (this.crc[0] != array[(int)(array[8] + 9)] | this.crc[1] != array[(int)(array[8] + 10)]) & this.dataReceived;
                    if (flag30)
                    {
                        bool flag31 = this.debug;
                        if (flag31)
                        {
                            StoreLogData.Instance.Store("CRCCheckFailedException Throwed", DateTime.Now);
                        }
                        bool flag32 = this.NumberOfRetries <= this.countRetries;
                        if (flag32)
                        {
                            this.countRetries = 0;
                            throw new CRCCheckFailedException("Response CRC check failed");
                        }
                        this.countRetries++;
                        return this.ReadInputRegisters(startingAddress, quantity);
                    }
                    else
                    {
                        bool flag33 = !this.dataReceived;
                        if (flag33)
                        {
                            bool flag34 = this.debug;
                            if (flag34)
                            {
                                StoreLogData.Instance.Store("TimeoutException Throwed", DateTime.Now);
                            }
                            bool flag35 = this.NumberOfRetries <= this.countRetries;
                            if (flag35)
                            {
                                this.countRetries = 0;
                                throw new TimeoutException("No Response from Modbus Slave");
                            }
                            this.countRetries++;
                            return this.ReadInputRegisters(startingAddress, quantity);
                        }
                    }
                }
                int[] array4 = new int[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    byte b2 = array[9 + i * 2];
                    byte b3 = array[9 + i * 2 + 1];
                    array[9 + i * 2] = b3;
                    array[9 + i * 2 + 1] = b2;
                    array4[i] = (int)BitConverter.ToInt16(array, 9 + i * 2);
                }
                return array4;
            }
        }

        public void WriteSingleCoil(int startingAddress, bool value)
        {
            bool flag = this.debug;
            if (flag)
            {
                StoreLogData.Instance.Store("FC5 (Write single coil to Master device), StartingAddress: " + startingAddress.ToString() + ", Value: " + value.ToString(), DateTime.Now);
            }
            checked
            {
                this.transactionIdentifierInternal += 1U;
                bool flag2 = this.serialport != null;
                if (flag2)
                {
                    bool flag3 = !this.serialport.IsOpen;
                    if (flag3)
                    {
                        bool flag4 = this.debug;
                        if (flag4)
                        {
                            StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", DateTime.Now);
                        }
                        throw new SerialPortNotOpenedException("serial port not opened");
                    }
                }
                bool flag5 = this.tcpClient == null & !this.udpFlag & this.serialport == null;
                if (flag5)
                {
                    bool flag6 = this.debug;
                    if (flag6)
                    {
                        StoreLogData.Instance.Store("ConnectionException Throwed", DateTime.Now);
                    }
                    throw new ConnectionException("connection error");
                }
                byte[] array = new byte[2];
                this.transactionIdentifier = BitConverter.GetBytes(this.transactionIdentifierInternal);
                this.protocolIdentifier = BitConverter.GetBytes(0);
                this.length = BitConverter.GetBytes(6);
                this.functionCode = 5;
                this.startingAddress = BitConverter.GetBytes(startingAddress);
                bool flag7 = value;
                if (flag7)
                {
                    array = BitConverter.GetBytes(65280);
                }
                else
                {
                    array = BitConverter.GetBytes(0);
                }
                byte[] array2 = new byte[]
                {
                    this.transactionIdentifier[1],
                    this.transactionIdentifier[0],
                    this.protocolIdentifier[1],
                    this.protocolIdentifier[0],
                    this.length[1],
                    this.length[0],
                    this.unitIdentifier,
                    this.functionCode,
                    this.startingAddress[1],
                    this.startingAddress[0],
                    array[1],
                    array[0],
                    this.crc[0],
                    this.crc[1]
                };
                this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array2, 6, 6));
                array2[12] = this.crc[0];
                array2[13] = this.crc[1];
                bool flag8 = this.serialport != null;
                if (flag8)
                {
                    this.dataReceived = false;
                    this.bytesToRead = 8;
                    this.serialport.Write(array2, 6, 8);
                    bool flag9 = this.debug;
                    if (flag9)
                    {
                        byte[] array3 = new byte[8];
                        Array.Copy(array2, 6, array3, 0, 8);
                        bool flag10 = this.debug;
                        if (flag10)
                        {
                            StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array3), DateTime.Now);
                        }
                    }
                    bool flag11 = this.SendDataChanged != null;
                    if (flag11)
                    {
                        this.sendData = new byte[8];
                        Array.Copy(array2, 6, this.sendData, 0, 8);
                        this.SendDataChanged(this);
                    }
                    array2 = new byte[2100];
                    this.readBuffer = new byte[256];
                    DateTime now = DateTime.Now;
                    byte b = byte.MaxValue;
                    while (b != this.unitIdentifier & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                    {
                        while (!this.dataReceived & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                        {
                            Thread.Sleep(1);
                        }
                        array2 = new byte[2100];
                        Array.Copy(this.readBuffer, 0, array2, 6, this.readBuffer.Length);
                        b = array2[6];
                    }
                    bool flag12 = b != this.unitIdentifier;
                    if (flag12)
                    {
                        array2 = new byte[2100];
                    }
                    else
                    {
                        this.countRetries = 0;
                    }
                }
                else
                {
                    bool flag13 = this.tcpClient.Client.Connected | this.udpFlag;
                    if (flag13)
                    {
                        bool flag14 = this.udpFlag;
                        if (flag14)
                        {
                            UdpClient udpClient = new UdpClient();
                            IPEndPoint ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.port);
                            udpClient.Send(array2, array2.Length - 2, ipendPoint);
                            this.portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                            udpClient.Client.ReceiveTimeout = 5000;
                            ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.portOut);
                            array2 = udpClient.Receive(ref ipendPoint);
                        }
                        else
                        {
                            this.stream.Write(array2, 0, array2.Length - 2);
                            bool flag15 = this.debug;
                            if (flag15)
                            {
                                byte[] array4 = new byte[array2.Length - 2];
                                Array.Copy(array2, 0, array4, 0, array2.Length - 2);
                                bool flag16 = this.debug;
                                if (flag16)
                                {
                                    StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(array4), DateTime.Now);
                                }
                            }
                            bool flag17 = this.SendDataChanged != null;
                            if (flag17)
                            {
                                this.sendData = new byte[array2.Length - 2];
                                Array.Copy(array2, 0, this.sendData, 0, array2.Length - 2);
                                this.SendDataChanged(this);
                            }
                            array2 = new byte[2100];
                            int num = this.stream.Read(array2, 0, array2.Length);
                            bool flag18 = this.ReceiveDataChanged != null;
                            if (flag18)
                            {
                                this.receiveData = new byte[num];
                                Array.Copy(array2, 0, this.receiveData, 0, num);
                                bool flag19 = this.debug;
                                if (flag19)
                                {
                                    StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(this.receiveData), DateTime.Now);
                                }
                                this.ReceiveDataChanged(this);
                            }
                        }
                    }
                }
                bool flag20 = array2[7] == 133 & array2[8] == 1;
                if (flag20)
                {
                    bool flag21 = this.debug;
                    if (flag21)
                    {
                        StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", DateTime.Now);
                    }
                    throw new FunctionCodeNotSupportedException("Function code not supported by master");
                }
                bool flag22 = array2[7] == 133 & array2[8] == 2;
                if (flag22)
                {
                    bool flag23 = this.debug;
                    if (flag23)
                    {
                        StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", DateTime.Now);
                    }
                    throw new StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
                }
                bool flag24 = array2[7] == 133 & array2[8] == 3;
                if (flag24)
                {
                    bool flag25 = this.debug;
                    if (flag25)
                    {
                        StoreLogData.Instance.Store("QuantityInvalidException Throwed", DateTime.Now);
                    }
                    throw new QuantityInvalidException("quantity invalid");
                }
                bool flag26 = array2[7] == 133 & array2[8] == 4;
                if (flag26)
                {
                    bool flag27 = this.debug;
                    if (flag27)
                    {
                        StoreLogData.Instance.Store("ModbusException Throwed", DateTime.Now);
                    }
                    throw new ModbusException("error reading");
                }
                bool flag28 = this.serialport != null;
                if (flag28)
                {
                    this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array2, 6, 6));
                    bool flag29 = (this.crc[0] != array2[12] | this.crc[1] != array2[13]) & this.dataReceived;
                    if (flag29)
                    {
                        bool flag30 = this.debug;
                        if (flag30)
                        {
                            StoreLogData.Instance.Store("CRCCheckFailedException Throwed", DateTime.Now);
                        }
                        bool flag31 = this.NumberOfRetries <= this.countRetries;
                        if (flag31)
                        {
                            this.countRetries = 0;
                            throw new CRCCheckFailedException("Response CRC check failed");
                        }
                        this.countRetries++;
                        this.WriteSingleCoil(startingAddress, value);
                    }
                    else
                    {
                        bool flag32 = !this.dataReceived;
                        if (flag32)
                        {
                            bool flag33 = this.debug;
                            if (flag33)
                            {
                                StoreLogData.Instance.Store("TimeoutException Throwed", DateTime.Now);
                            }
                            bool flag34 = this.NumberOfRetries <= this.countRetries;
                            if (flag34)
                            {
                                this.countRetries = 0;
                                throw new TimeoutException("No Response from Modbus Slave");
                            }
                            this.countRetries++;
                            this.WriteSingleCoil(startingAddress, value);
                        }
                    }
                }
            }
        }

        public void WriteSingleRegister(int startingAddress, int value)
        {
            bool flag = this.debug;
            if (flag)
            {
                StoreLogData.Instance.Store("FC6 (Write single register to Master device), StartingAddress: " + startingAddress.ToString() + ", Value: " + value.ToString(), DateTime.Now);
            }
            checked
            {
                this.transactionIdentifierInternal += 1U;
                bool flag2 = this.serialport != null;
                if (flag2)
                {
                    bool flag3 = !this.serialport.IsOpen;
                    if (flag3)
                    {
                        bool flag4 = this.debug;
                        if (flag4)
                        {
                            StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", DateTime.Now);
                        }
                        throw new SerialPortNotOpenedException("serial port not opened");
                    }
                }
                bool flag5 = this.tcpClient == null & !this.udpFlag & this.serialport == null;
                if (flag5)
                {
                    bool flag6 = this.debug;
                    if (flag6)
                    {
                        StoreLogData.Instance.Store("ConnectionException Throwed", DateTime.Now);
                    }
                    throw new ConnectionException("connection error");
                }
                byte[] array = new byte[2];
                this.transactionIdentifier = BitConverter.GetBytes(this.transactionIdentifierInternal);
                this.protocolIdentifier = BitConverter.GetBytes(0);
                this.length = BitConverter.GetBytes(6);
                this.functionCode = 6;
                this.startingAddress = BitConverter.GetBytes(startingAddress);
                array = BitConverter.GetBytes(value);
                byte[] array2 = new byte[]
                {
                    this.transactionIdentifier[1],
                    this.transactionIdentifier[0],
                    this.protocolIdentifier[1],
                    this.protocolIdentifier[0],
                    this.length[1],
                    this.length[0],
                    this.unitIdentifier,
                    this.functionCode,
                    this.startingAddress[1],
                    this.startingAddress[0],
                    array[1],
                    array[0],
                    this.crc[0],
                    this.crc[1]
                };
                this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array2, 6, 6));
                array2[12] = this.crc[0];
                array2[13] = this.crc[1];
                bool flag7 = this.serialport != null;
                if (flag7)
                {
                    this.dataReceived = false;
                    this.bytesToRead = 8;
                    this.serialport.Write(array2, 6, 8);
                    bool flag8 = this.debug;
                    if (flag8)
                    {
                        byte[] array3 = new byte[8];
                        Array.Copy(array2, 6, array3, 0, 8);
                        bool flag9 = this.debug;
                        if (flag9)
                        {
                            StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array3), DateTime.Now);
                        }
                    }
                    bool flag10 = this.SendDataChanged != null;
                    if (flag10)
                    {
                        this.sendData = new byte[8];
                        Array.Copy(array2, 6, this.sendData, 0, 8);
                        this.SendDataChanged(this);
                    }
                    array2 = new byte[2100];
                    this.readBuffer = new byte[256];
                    DateTime now = DateTime.Now;
                    byte b = byte.MaxValue;
                    while (b != this.unitIdentifier & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                    {
                        while (!this.dataReceived & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                        {
                            Thread.Sleep(1);
                        }
                        array2 = new byte[2100];
                        Array.Copy(this.readBuffer, 0, array2, 6, this.readBuffer.Length);
                        b = array2[6];
                    }
                    bool flag11 = b != this.unitIdentifier;
                    if (flag11)
                    {
                        array2 = new byte[2100];
                    }
                    else
                    {
                        this.countRetries = 0;
                    }
                }
                else
                {
                    bool flag12 = this.tcpClient.Client.Connected | this.udpFlag;
                    if (flag12)
                    {
                        bool flag13 = this.udpFlag;
                        if (flag13)
                        {
                            UdpClient udpClient = new UdpClient();
                            IPEndPoint ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.port);
                            udpClient.Send(array2, array2.Length - 2, ipendPoint);
                            this.portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                            udpClient.Client.ReceiveTimeout = 5000;
                            ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.portOut);
                            array2 = udpClient.Receive(ref ipendPoint);
                        }
                        else
                        {
                            this.stream.Write(array2, 0, array2.Length - 2);
                            bool flag14 = this.debug;
                            if (flag14)
                            {
                                byte[] array4 = new byte[array2.Length - 2];
                                Array.Copy(array2, 0, array4, 0, array2.Length - 2);
                                bool flag15 = this.debug;
                                if (flag15)
                                {
                                    StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(array4), DateTime.Now);
                                }
                            }
                            bool flag16 = this.SendDataChanged != null;
                            if (flag16)
                            {
                                this.sendData = new byte[array2.Length - 2];
                                Array.Copy(array2, 0, this.sendData, 0, array2.Length - 2);
                                this.SendDataChanged(this);
                            }
                            array2 = new byte[2100];
                            int num = this.stream.Read(array2, 0, array2.Length);
                            bool flag17 = this.ReceiveDataChanged != null;
                            if (flag17)
                            {
                                this.receiveData = new byte[num];
                                Array.Copy(array2, 0, this.receiveData, 0, num);
                                bool flag18 = this.debug;
                                if (flag18)
                                {
                                    StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(this.receiveData), DateTime.Now);
                                }
                                this.ReceiveDataChanged(this);
                            }
                        }
                    }
                }
                bool flag19 = array2[7] == 134 & array2[8] == 1;
                if (flag19)
                {
                    bool flag20 = this.debug;
                    if (flag20)
                    {
                        StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", DateTime.Now);
                    }
                    throw new FunctionCodeNotSupportedException("Function code not supported by master");
                }
                bool flag21 = array2[7] == 134 & array2[8] == 2;
                if (flag21)
                {
                    bool flag22 = this.debug;
                    if (flag22)
                    {
                        StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", DateTime.Now);
                    }
                    throw new StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
                }
                bool flag23 = array2[7] == 134 & array2[8] == 3;
                if (flag23)
                {
                    bool flag24 = this.debug;
                    if (flag24)
                    {
                        StoreLogData.Instance.Store("QuantityInvalidException Throwed", DateTime.Now);
                    }
                    throw new QuantityInvalidException("quantity invalid");
                }
                bool flag25 = array2[7] == 134 & array2[8] == 4;
                if (flag25)
                {
                    bool flag26 = this.debug;
                    if (flag26)
                    {
                        StoreLogData.Instance.Store("ModbusException Throwed", DateTime.Now);
                    }
                    throw new ModbusException("error reading");
                }
                bool flag27 = this.serialport != null;
                if (flag27)
                {
                    this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array2, 6, 6));
                    bool flag28 = (this.crc[0] != array2[12] | this.crc[1] != array2[13]) & this.dataReceived;
                    if (flag28)
                    {
                        bool flag29 = this.debug;
                        if (flag29)
                        {
                            StoreLogData.Instance.Store("CRCCheckFailedException Throwed", DateTime.Now);
                        }
                        bool flag30 = this.NumberOfRetries <= this.countRetries;
                        if (flag30)
                        {
                            this.countRetries = 0;
                            throw new CRCCheckFailedException("Response CRC check failed");
                        }
                        this.countRetries++;
                        this.WriteSingleRegister(startingAddress, value);
                    }
                    else
                    {
                        bool flag31 = !this.dataReceived;
                        if (flag31)
                        {
                            bool flag32 = this.debug;
                            if (flag32)
                            {
                                StoreLogData.Instance.Store("TimeoutException Throwed", DateTime.Now);
                            }
                            bool flag33 = this.NumberOfRetries <= this.countRetries;
                            if (flag33)
                            {
                                this.countRetries = 0;
                                throw new TimeoutException("No Response from Modbus Slave");
                            }
                            this.countRetries++;
                            this.WriteSingleRegister(startingAddress, value);
                        }
                    }
                }
            }
        }

        public void WriteMultipleCoils(int startingAddress, bool[] values)
        {
            string text = "";
            checked
            {
                for (int i = 0; i < values.Length; i++)
                {
                    text = text + values[i].ToString() + " ";
                }
                bool flag = this.debug;
                if (flag)
                {
                    StoreLogData.Instance.Store("FC15 (Write multiple coils to Master device), StartingAddress: " + startingAddress.ToString() + ", Values: " + text, DateTime.Now);
                }
                this.transactionIdentifierInternal += 1U;
                byte b = (byte)((values.Length % 8 != 0) ? (values.Length / 8 + 1) : (values.Length / 8));
                byte[] bytes = BitConverter.GetBytes(values.Length);
                byte b2 = 0;
                bool flag2 = this.serialport != null;
                if (flag2)
                {
                    bool flag3 = !this.serialport.IsOpen;
                    if (flag3)
                    {
                        bool flag4 = this.debug;
                        if (flag4)
                        {
                            StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", DateTime.Now);
                        }
                        throw new SerialPortNotOpenedException("serial port not opened");
                    }
                }
                bool flag5 = this.tcpClient == null & !this.udpFlag & this.serialport == null;
                if (flag5)
                {
                    bool flag6 = this.debug;
                    if (flag6)
                    {
                        StoreLogData.Instance.Store("ConnectionException Throwed", DateTime.Now);
                    }
                    throw new ConnectionException("connection error");
                }
                this.transactionIdentifier = BitConverter.GetBytes(this.transactionIdentifierInternal);
                this.protocolIdentifier = BitConverter.GetBytes(0);
                this.length = BitConverter.GetBytes((int)(7 + b));
                this.functionCode = 15;
                this.startingAddress = BitConverter.GetBytes(startingAddress);
                byte[] array = new byte[16 + ((values.Length % 8 != 0) ? (values.Length / 8) : (values.Length / 8 - 1))];
                array[0] = this.transactionIdentifier[1];
                array[1] = this.transactionIdentifier[0];
                array[2] = this.protocolIdentifier[1];
                array[3] = this.protocolIdentifier[0];
                array[4] = this.length[1];
                array[5] = this.length[0];
                array[6] = this.unitIdentifier;
                array[7] = this.functionCode;
                array[8] = this.startingAddress[1];
                array[9] = this.startingAddress[0];
                array[10] = bytes[1];
                array[11] = bytes[0];
                array[12] = b;
                for (int j = 0; j < values.Length; j++)
                {
                    bool flag7 = j % 8 == 0;
                    if (flag7)
                    {
                        b2 = 0;
                    }
                    bool flag8 = values[j];
                    byte b3;
                    if (flag8)
                    {
                        b3 = 1;
                    }
                    else
                    {
                        b3 = 0;
                    }
                    b2 = (byte)((int)b3 << j % 8 | (int)b2);
                    array[13 + j / 8] = b2;
                }
                this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array, (ushort)(array.Length - 8), 6));
                array[array.Length - 2] = this.crc[0];
                array[array.Length - 1] = this.crc[1];
                bool flag9 = this.serialport != null;
                if (flag9)
                {
                    this.dataReceived = false;
                    this.bytesToRead = 8;
                    this.serialport.Write(array, 6, array.Length - 6);
                    bool flag10 = this.debug;
                    if (flag10)
                    {
                        byte[] array2 = new byte[array.Length - 6];
                        Array.Copy(array, 6, array2, 0, array.Length - 6);
                        bool flag11 = this.debug;
                        if (flag11)
                        {
                            StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array2), DateTime.Now);
                        }
                    }
                    bool flag12 = this.SendDataChanged != null;
                    if (flag12)
                    {
                        this.sendData = new byte[array.Length - 6];
                        Array.Copy(array, 6, this.sendData, 0, array.Length - 6);
                        this.SendDataChanged(this);
                    }
                    array = new byte[2100];
                    this.readBuffer = new byte[256];
                    DateTime now = DateTime.Now;
                    byte b4 = byte.MaxValue;
                    while (b4 != this.unitIdentifier & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                    {
                        while (!this.dataReceived & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                        {
                            Thread.Sleep(1);
                        }
                        array = new byte[2100];
                        Array.Copy(this.readBuffer, 0, array, 6, this.readBuffer.Length);
                        b4 = array[6];
                    }
                    bool flag13 = b4 != this.unitIdentifier;
                    if (flag13)
                    {
                        array = new byte[2100];
                    }
                    else
                    {
                        this.countRetries = 0;
                    }
                }
                else
                {
                    bool flag14 = this.tcpClient.Client.Connected | this.udpFlag;
                    if (flag14)
                    {
                        bool flag15 = this.udpFlag;
                        if (flag15)
                        {
                            UdpClient udpClient = new UdpClient();
                            IPEndPoint ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.port);
                            udpClient.Send(array, array.Length - 2, ipendPoint);
                            this.portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                            udpClient.Client.ReceiveTimeout = 5000;
                            ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.portOut);
                            array = udpClient.Receive(ref ipendPoint);
                        }
                        else
                        {
                            this.stream.Write(array, 0, array.Length - 2);
                            bool flag16 = this.debug;
                            if (flag16)
                            {
                                byte[] array3 = new byte[array.Length - 2];
                                Array.Copy(array, 0, array3, 0, array.Length - 2);
                                bool flag17 = this.debug;
                                if (flag17)
                                {
                                    StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(array3), DateTime.Now);
                                }
                            }
                            bool flag18 = this.SendDataChanged != null;
                            if (flag18)
                            {
                                this.sendData = new byte[array.Length - 2];
                                Array.Copy(array, 0, this.sendData, 0, array.Length - 2);
                                this.SendDataChanged(this);
                            }
                            array = new byte[2100];
                            int num = this.stream.Read(array, 0, array.Length);
                            bool flag19 = this.ReceiveDataChanged != null;
                            if (flag19)
                            {
                                this.receiveData = new byte[num];
                                Array.Copy(array, 0, this.receiveData, 0, num);
                                bool flag20 = this.debug;
                                if (flag20)
                                {
                                    StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(this.receiveData), DateTime.Now);
                                }
                                this.ReceiveDataChanged(this);
                            }
                        }
                    }
                }
                bool flag21 = array[7] == 143 & array[8] == 1;
                if (flag21)
                {
                    bool flag22 = this.debug;
                    if (flag22)
                    {
                        StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", DateTime.Now);
                    }
                    throw new FunctionCodeNotSupportedException("Function code not supported by master");
                }
                bool flag23 = array[7] == 143 & array[8] == 2;
                if (flag23)
                {
                    bool flag24 = this.debug;
                    if (flag24)
                    {
                        StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", DateTime.Now);
                    }
                    throw new StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
                }
                bool flag25 = array[7] == 143 & array[8] == 3;
                if (flag25)
                {
                    bool flag26 = this.debug;
                    if (flag26)
                    {
                        StoreLogData.Instance.Store("QuantityInvalidException Throwed", DateTime.Now);
                    }
                    throw new QuantityInvalidException("quantity invalid");
                }
                bool flag27 = array[7] == 143 & array[8] == 4;
                if (flag27)
                {
                    bool flag28 = this.debug;
                    if (flag28)
                    {
                        StoreLogData.Instance.Store("ModbusException Throwed", DateTime.Now);
                    }
                    throw new ModbusException("error reading");
                }
                bool flag29 = this.serialport != null;
                if (flag29)
                {
                    this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array, 6, 6));
                    bool flag30 = (this.crc[0] != array[12] | this.crc[1] != array[13]) & this.dataReceived;
                    if (flag30)
                    {
                        bool flag31 = this.debug;
                        if (flag31)
                        {
                            StoreLogData.Instance.Store("CRCCheckFailedException Throwed", DateTime.Now);
                        }
                        bool flag32 = this.NumberOfRetries <= this.countRetries;
                        if (flag32)
                        {
                            this.countRetries = 0;
                            throw new CRCCheckFailedException("Response CRC check failed");
                        }
                        this.countRetries++;
                        this.WriteMultipleCoils(startingAddress, values);
                    }
                    else
                    {
                        bool flag33 = !this.dataReceived;
                        if (flag33)
                        {
                            bool flag34 = this.debug;
                            if (flag34)
                            {
                                StoreLogData.Instance.Store("TimeoutException Throwed", DateTime.Now);
                            }
                            bool flag35 = this.NumberOfRetries <= this.countRetries;
                            if (flag35)
                            {
                                this.countRetries = 0;
                                throw new TimeoutException("No Response from Modbus Slave");
                            }
                            this.countRetries++;
                            this.WriteMultipleCoils(startingAddress, values);
                        }
                    }
                }
            }
        }

        public void WriteMultipleRegisters(int startingAddress, int[] values)
        {
            string text = "";
            checked
            {
                for (int i = 0; i < values.Length; i++)
                {
                    text = text + values[i].ToString() + " ";
                }
                bool flag = this.debug;
                if (flag)
                {
                    StoreLogData.Instance.Store("FC16 (Write multiple Registers to Server device), StartingAddress: " + startingAddress.ToString() + ", Values: " + text, DateTime.Now);
                }
                this.transactionIdentifierInternal += 1U;
                byte b = (byte)(values.Length * 2);
                byte[] bytes = BitConverter.GetBytes(values.Length);
                bool flag2 = this.serialport != null;
                if (flag2)
                {
                    bool flag3 = !this.serialport.IsOpen;
                    if (flag3)
                    {
                        bool flag4 = this.debug;
                        if (flag4)
                        {
                            StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", DateTime.Now);
                        }
                        throw new SerialPortNotOpenedException("serial port not opened");
                    }
                }
                bool flag5 = this.tcpClient == null & !this.udpFlag & this.serialport == null;
                if (flag5)
                {
                    bool flag6 = this.debug;
                    if (flag6)
                    {
                        StoreLogData.Instance.Store("ConnectionException Throwed", DateTime.Now);
                    }
                    throw new ConnectionException("connection error");
                }
                this.transactionIdentifier = BitConverter.GetBytes(this.transactionIdentifierInternal);
                this.protocolIdentifier = BitConverter.GetBytes(0);
                this.length = BitConverter.GetBytes(7 + values.Length * 2);
                this.functionCode = 16;
                this.startingAddress = BitConverter.GetBytes(startingAddress);
                byte[] array = new byte[15 + values.Length * 2];
                array[0] = this.transactionIdentifier[1];
                array[1] = this.transactionIdentifier[0];
                array[2] = this.protocolIdentifier[1];
                array[3] = this.protocolIdentifier[0];
                array[4] = this.length[1];
                array[5] = this.length[0];
                array[6] = this.unitIdentifier;
                array[7] = this.functionCode;
                array[8] = this.startingAddress[1];
                array[9] = this.startingAddress[0];
                array[10] = bytes[1];
                array[11] = bytes[0];
                array[12] = b;
                for (int j = 0; j < values.Length; j++)
                {
                    byte[] bytes2 = BitConverter.GetBytes(values[j]);
                    array[13 + j * 2] = bytes2[1];
                    array[14 + j * 2] = bytes2[0];
                }
                this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array, (ushort)(array.Length - 8), 6));
                array[array.Length - 2] = this.crc[0];
                array[array.Length - 1] = this.crc[1];
                bool flag7 = this.serialport != null;
                if (flag7)
                {
                    this.dataReceived = false;
                    this.bytesToRead = 8;
                    this.serialport.Write(array, 6, array.Length - 6);
                    bool flag8 = this.debug;
                    if (flag8)
                    {
                        byte[] array2 = new byte[array.Length - 6];
                        Array.Copy(array, 6, array2, 0, array.Length - 6);
                        bool flag9 = this.debug;
                        if (flag9)
                        {
                            StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array2), DateTime.Now);
                        }
                    }
                    bool flag10 = this.SendDataChanged != null;
                    if (flag10)
                    {
                        this.sendData = new byte[array.Length - 6];
                        Array.Copy(array, 6, this.sendData, 0, array.Length - 6);
                        this.SendDataChanged(this);
                    }
                    array = new byte[2100];
                    this.readBuffer = new byte[256];
                    DateTime now = DateTime.Now;
                    byte b2 = byte.MaxValue;
                    while (b2 != this.unitIdentifier & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                    {
                        while (!this.dataReceived & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                        {
                            Thread.Sleep(1);
                        }
                        array = new byte[2100];
                        Array.Copy(this.readBuffer, 0, array, 6, this.readBuffer.Length);
                        b2 = array[6];
                    }
                    bool flag11 = b2 != this.unitIdentifier;
                    if (flag11)
                    {
                        array = new byte[2100];
                    }
                    else
                    {
                        this.countRetries = 0;
                    }
                }
                else
                {
                    bool flag12 = this.tcpClient.Client.Connected | this.udpFlag;
                    if (flag12)
                    {
                        bool flag13 = this.udpFlag;
                        if (flag13)
                        {
                            UdpClient udpClient = new UdpClient();
                            IPEndPoint ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.port);
                            udpClient.Send(array, array.Length - 2, ipendPoint);
                            this.portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                            udpClient.Client.ReceiveTimeout = 5000;
                            ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.portOut);
                            array = udpClient.Receive(ref ipendPoint);
                        }
                        else
                        {
                            this.stream.Write(array, 0, array.Length - 2);
                            bool flag14 = this.debug;
                            if (flag14)
                            {
                                byte[] array3 = new byte[array.Length - 2];
                                Array.Copy(array, 0, array3, 0, array.Length - 2);
                                bool flag15 = this.debug;
                                if (flag15)
                                {
                                    StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(array3), DateTime.Now);
                                }
                            }
                            bool flag16 = this.SendDataChanged != null;
                            if (flag16)
                            {
                                this.sendData = new byte[array.Length - 2];
                                Array.Copy(array, 0, this.sendData, 0, array.Length - 2);
                                this.SendDataChanged(this);
                            }
                            array = new byte[2100];
                            int num = this.stream.Read(array, 0, array.Length);
                            bool flag17 = this.ReceiveDataChanged != null;
                            if (flag17)
                            {
                                this.receiveData = new byte[num];
                                Array.Copy(array, 0, this.receiveData, 0, num);
                                bool flag18 = this.debug;
                                if (flag18)
                                {
                                    StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(this.receiveData), DateTime.Now);
                                }
                                this.ReceiveDataChanged(this);
                            }
                        }
                    }
                }
                bool flag19 = array[7] == 144 & array[8] == 1;
                if (flag19)
                {
                    bool flag20 = this.debug;
                    if (flag20)
                    {
                        StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", DateTime.Now);
                    }
                    throw new FunctionCodeNotSupportedException("Function code not supported by master");
                }
                bool flag21 = array[7] == 144 & array[8] == 2;
                if (flag21)
                {
                    bool flag22 = this.debug;
                    if (flag22)
                    {
                        StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", DateTime.Now);
                    }
                    throw new StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
                }
                bool flag23 = array[7] == 144 & array[8] == 3;
                if (flag23)
                {
                    bool flag24 = this.debug;
                    if (flag24)
                    {
                        StoreLogData.Instance.Store("QuantityInvalidException Throwed", DateTime.Now);
                    }
                    throw new QuantityInvalidException("quantity invalid");
                }
                bool flag25 = array[7] == 144 & array[8] == 4;
                if (flag25)
                {
                    bool flag26 = this.debug;
                    if (flag26)
                    {
                        StoreLogData.Instance.Store("ModbusException Throwed", DateTime.Now);
                    }
                    throw new ModbusException("error reading");
                }
                bool flag27 = this.serialport != null;
                if (flag27)
                {
                    this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array, 6, 6));
                    bool flag28 = (this.crc[0] != array[12] | this.crc[1] != array[13]) & this.dataReceived;
                    if (flag28)
                    {
                        bool flag29 = this.debug;
                        if (flag29)
                        {
                            StoreLogData.Instance.Store("CRCCheckFailedException Throwed", DateTime.Now);
                        }
                        bool flag30 = this.NumberOfRetries <= this.countRetries;
                        if (flag30)
                        {
                            this.countRetries = 0;
                            throw new CRCCheckFailedException("Response CRC check failed");
                        }
                        this.countRetries++;
                        this.WriteMultipleRegisters(startingAddress, values);
                    }
                    else
                    {
                        bool flag31 = !this.dataReceived;
                        if (flag31)
                        {
                            bool flag32 = this.debug;
                            if (flag32)
                            {
                                StoreLogData.Instance.Store("TimeoutException Throwed", DateTime.Now);
                            }
                            bool flag33 = this.NumberOfRetries <= this.countRetries;
                            if (flag33)
                            {
                                this.countRetries = 0;
                                throw new TimeoutException("No Response from Modbus Slave");
                            }
                            this.countRetries++;
                            this.WriteMultipleRegisters(startingAddress, values);
                        }
                    }
                }
            }
        }

        public int[] ReadWriteMultipleRegisters(int startingAddressRead, int quantityRead, int startingAddressWrite, int[] values)
        {
            string text = "";
            checked
            {
                for (int i = 0; i < values.Length; i++)
                {
                    text = text + values[i].ToString() + " ";
                }
                bool flag = this.debug;
                if (flag)
                {
                    StoreLogData.Instance.Store(string.Concat(new string[]
                    {
                        "FC23 (Read and Write multiple Registers to Server device), StartingAddress Read: ",
                        startingAddressRead.ToString(),
                        ", Quantity Read: ",
                        quantityRead.ToString(),
                        ", startingAddressWrite: ",
                        startingAddressWrite.ToString(),
                        ", Values: ",
                        text
                    }), DateTime.Now);
                }
                this.transactionIdentifierInternal += 1U;
                byte[] array = new byte[2];
                byte[] array2 = new byte[2];
                byte[] array3 = new byte[2];
                byte[] array4 = new byte[2];
                bool flag2 = this.serialport != null;
                if (flag2)
                {
                    bool flag3 = !this.serialport.IsOpen;
                    if (flag3)
                    {
                        bool flag4 = this.debug;
                        if (flag4)
                        {
                            StoreLogData.Instance.Store("SerialPortNotOpenedException Throwed", DateTime.Now);
                        }
                        throw new SerialPortNotOpenedException("serial port not opened");
                    }
                }
                bool flag5 = this.tcpClient == null & !this.udpFlag & this.serialport == null;
                if (flag5)
                {
                    bool flag6 = this.debug;
                    if (flag6)
                    {
                        StoreLogData.Instance.Store("ConnectionException Throwed", DateTime.Now);
                    }
                    throw new ConnectionException("connection error");
                }
                bool flag7 = startingAddressRead > 65535 | quantityRead > 125 | startingAddressWrite > 65535 | values.Length > 121;
                if (flag7)
                {
                    bool flag8 = this.debug;
                    if (flag8)
                    {
                        StoreLogData.Instance.Store("ArgumentException Throwed", DateTime.Now);
                    }
                    throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 2000");
                }
                this.transactionIdentifier = BitConverter.GetBytes(this.transactionIdentifierInternal);
                this.protocolIdentifier = BitConverter.GetBytes(0);
                this.length = BitConverter.GetBytes(11 + values.Length * 2);
                this.functionCode = 23;
                array = BitConverter.GetBytes(startingAddressRead);
                array2 = BitConverter.GetBytes(quantityRead);
                array3 = BitConverter.GetBytes(startingAddressWrite);
                array4 = BitConverter.GetBytes(values.Length);
                byte b = Convert.ToByte(values.Length * 2);
                byte[] array5 = new byte[19 + values.Length * 2];
                array5[0] = this.transactionIdentifier[1];
                array5[1] = this.transactionIdentifier[0];
                array5[2] = this.protocolIdentifier[1];
                array5[3] = this.protocolIdentifier[0];
                array5[4] = this.length[1];
                array5[5] = this.length[0];
                array5[6] = this.unitIdentifier;
                array5[7] = this.functionCode;
                array5[8] = array[1];
                array5[9] = array[0];
                array5[10] = array2[1];
                array5[11] = array2[0];
                array5[12] = array3[1];
                array5[13] = array3[0];
                array5[14] = array4[1];
                array5[15] = array4[0];
                array5[16] = b;
                for (int j = 0; j < values.Length; j++)
                {
                    byte[] bytes = BitConverter.GetBytes(values[j]);
                    array5[17 + j * 2] = bytes[1];
                    array5[18 + j * 2] = bytes[0];
                }
                this.crc = BitConverter.GetBytes(ModbusClient.calculateCRC(array5, (ushort)(array5.Length - 8), 6));
                array5[array5.Length - 2] = this.crc[0];
                array5[array5.Length - 1] = this.crc[1];
                bool flag9 = this.serialport != null;
                if (flag9)
                {
                    this.dataReceived = false;
                    this.bytesToRead = 5 + 2 * quantityRead;
                    this.serialport.Write(array5, 6, array5.Length - 6);
                    bool flag10 = this.debug;
                    if (flag10)
                    {
                        byte[] array6 = new byte[array5.Length - 6];
                        Array.Copy(array5, 6, array6, 0, array5.Length - 6);
                        bool flag11 = this.debug;
                        if (flag11)
                        {
                            StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array6), DateTime.Now);
                        }
                    }
                    bool flag12 = this.SendDataChanged != null;
                    if (flag12)
                    {
                        this.sendData = new byte[array5.Length - 6];
                        Array.Copy(array5, 6, this.sendData, 0, array5.Length - 6);
                        this.SendDataChanged(this);
                    }
                    array5 = new byte[2100];
                    this.readBuffer = new byte[256];
                    DateTime now = DateTime.Now;
                    byte b2 = byte.MaxValue;
                    while (b2 != this.unitIdentifier & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                    {
                        while (!this.dataReceived & DateTime.Now.Ticks - now.Ticks <= 10000L * unchecked((long)this.connectTimeout))
                        {
                            Thread.Sleep(1);
                        }
                        array5 = new byte[2100];
                        Array.Copy(this.readBuffer, 0, array5, 6, this.readBuffer.Length);
                        b2 = array5[6];
                    }
                    bool flag13 = b2 != this.unitIdentifier;
                    if (flag13)
                    {
                        array5 = new byte[2100];
                    }
                    else
                    {
                        this.countRetries = 0;
                    }
                }
                else
                {
                    bool flag14 = this.tcpClient.Client.Connected | this.udpFlag;
                    if (flag14)
                    {
                        bool flag15 = this.udpFlag;
                        if (flag15)
                        {
                            UdpClient udpClient = new UdpClient();
                            IPEndPoint ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.port);
                            udpClient.Send(array5, array5.Length - 2, ipendPoint);
                            this.portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                            udpClient.Client.ReceiveTimeout = 5000;
                            ipendPoint = new IPEndPoint(System.Net.IPAddress.Parse(this.ipAddress), this.portOut);
                            array5 = udpClient.Receive(ref ipendPoint);
                        }
                        else
                        {
                            this.stream.Write(array5, 0, array5.Length - 2);
                            bool flag16 = this.debug;
                            if (flag16)
                            {
                                byte[] array7 = new byte[array5.Length - 2];
                                Array.Copy(array5, 0, array7, 0, array5.Length - 2);
                                bool flag17 = this.debug;
                                if (flag17)
                                {
                                    StoreLogData.Instance.Store("Send ModbusTCP-Data: " + BitConverter.ToString(array7), DateTime.Now);
                                }
                            }
                            bool flag18 = this.SendDataChanged != null;
                            if (flag18)
                            {
                                this.sendData = new byte[array5.Length - 2];
                                Array.Copy(array5, 0, this.sendData, 0, array5.Length - 2);
                                this.SendDataChanged(this);
                            }
                            array5 = new byte[2100];
                            int num = this.stream.Read(array5, 0, array5.Length);
                            bool flag19 = this.ReceiveDataChanged != null;
                            if (flag19)
                            {
                                this.receiveData = new byte[num];
                                Array.Copy(array5, 0, this.receiveData, 0, num);
                                bool flag20 = this.debug;
                                if (flag20)
                                {
                                    StoreLogData.Instance.Store("Receive ModbusTCP-Data: " + BitConverter.ToString(this.receiveData), DateTime.Now);
                                }
                                this.ReceiveDataChanged(this);
                            }
                        }
                    }
                }
                bool flag21 = array5[7] == 151 & array5[8] == 1;
                if (flag21)
                {
                    bool flag22 = this.debug;
                    if (flag22)
                    {
                        StoreLogData.Instance.Store("FunctionCodeNotSupportedException Throwed", DateTime.Now);
                    }
                    throw new FunctionCodeNotSupportedException("Function code not supported by master");
                }
                bool flag23 = array5[7] == 151 & array5[8] == 2;
                if (flag23)
                {
                    bool flag24 = this.debug;
                    if (flag24)
                    {
                        StoreLogData.Instance.Store("StartingAddressInvalidException Throwed", DateTime.Now);
                    }
                    throw new StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
                }
                bool flag25 = array5[7] == 151 & array5[8] == 3;
                if (flag25)
                {
                    bool flag26 = this.debug;
                    if (flag26)
                    {
                        StoreLogData.Instance.Store("QuantityInvalidException Throwed", DateTime.Now);
                    }
                    throw new QuantityInvalidException("quantity invalid");
                }
                bool flag27 = array5[7] == 151 & array5[8] == 4;
                if (flag27)
                {
                    bool flag28 = this.debug;
                    if (flag28)
                    {
                        StoreLogData.Instance.Store("ModbusException Throwed", DateTime.Now);
                    }
                    throw new ModbusException("error reading");
                }
                int[] array8 = new int[quantityRead];
                for (int k = 0; k < quantityRead; k++)
                {
                    byte b3 = array5[9 + k * 2];
                    byte b4 = array5[9 + k * 2 + 1];
                    array5[9 + k * 2] = b4;
                    array5[9 + k * 2 + 1] = b3;
                    array8[k] = (int)BitConverter.ToInt16(array5, 9 + k * 2);
                }
                return array8;
            }
        }

        public void Disconnect()
        {
            bool flag = this.debug;
            if (flag)
            {
                StoreLogData.Instance.Store("Disconnect", DateTime.Now);
            }
            bool flag2 = this.serialport != null;
            if (flag2)
            {
                bool flag3 = this.serialport.IsOpen & !this.receiveActive;
                if (flag3)
                {
                    this.serialport.Close();
                }
                bool flag4 = this.ConnectedChanged != null;
                if (flag4)
                {
                    this.ConnectedChanged(this);
                }
            }
            else
            {
                bool flag5 = this.stream != null;
                if (flag5)
                {
                    this.stream.Close();
                }
                bool flag6 = this.tcpClient != null;
                if (flag6)
                {
                    this.tcpClient.Close();
                }
                this.connected = false;
                bool flag7 = this.ConnectedChanged != null;
                if (flag7)
                {
                    this.ConnectedChanged(this);
                }
            }
        }

        
        //protected override void Finalize()
        //{
        //    try
        //    {
        //        bool flag = this.debug;
        //        if (flag)
        //        {
        //            StoreLogData.Instance.Store("Destructor called - automatically disconnect", DateTime.Now);
        //        }
        //        bool flag2 = this.serialport != null;
        //        if (flag2)
        //        {
        //            bool isOpen = this.serialport.IsOpen;
        //            if (isOpen)
        //            {
        //                this.serialport.Close();
        //            }
        //        }
        //        else
        //        {
        //            bool flag3 = this.tcpClient != null & !this.udpFlag;
        //            if (flag3)
        //            {
        //                bool flag4 = this.stream != null;
        //                if (flag4)
        //                {
        //                    this.stream.Close();
        //                }
        //                this.tcpClient.Close();
        //            }
        //        }
        //    }
        //    finally
        //    {
        //        base.Finalize();
        //    }
        //}

        // Token: 0x17000002 RID: 2
        // (get) Token: 0x0600002E RID: 46 RVA: 0x000082F8 File Offset: 0x000064F8
        public bool Connected
        {
            get
            {
                bool flag = this.serialport != null;
                bool result;
                if (flag)
                {
                    result = this.serialport.IsOpen;
                }
                else
                {
                    bool flag2 = this.udpFlag & this.tcpClient != null;
                    if (flag2)
                    {
                        result = true;
                    }
                    else
                    {
                        bool flag3 = this.tcpClient == null;
                        result = (!flag3 && this.connected);
                    }
                }
                return result;
            }
        }

        public bool Available(int timeout)
        {
            Ping ping = new Ping();
            IPAddress ipaddress = System.Net.IPAddress.Parse(this.ipAddress);
            string text = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            PingReply pingReply = ping.Send(ipaddress, timeout, bytes);
            return pingReply.Status == 0;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public string IPAddress
        {
            get
            {
                return this.ipAddress;
            }
            set
            {
                this.ipAddress = value;
            }
        }

        public int Port
        {
            get
            {
                return this.port;
            }
            set
            {
                this.port = value;
            }
        }

        public bool UDPFlag
        {
            get
            {
                return this.udpFlag;
            }
            set
            {
                this.udpFlag = value;
            }
        }

        public byte UnitIdentifier
        {
            get
            {
                return this.unitIdentifier;
            }
            set
            {
                this.unitIdentifier = value;
            }
        }

      
        public int Baudrate
        {
            get
            {
                return this.baudRate;
            }
            set
            {
                this.baudRate = value;
            }
        }

        
        public Parity Parity
        {
            get
            {
                bool flag = this.serialport != null;
                Parity result;
                if (flag)
                {
                    result = this.parity;
                }
                else
                {
                    result = (Parity)2;
                }
                return result;
            }
            set
            {
                bool flag = this.serialport != null;
                if (flag)
                {
                    this.parity = value;
                }
            }
        }

        public StopBits StopBits
        {
            get
            {
                bool flag = this.serialport != null;
                StopBits result;
                if (flag)
                {
                    result = this.stopBits;
                }
                else
                {
                    result = (StopBits)1;
                }
                return result;
            }
            set
            {
                bool flag = this.serialport != null;
                if (flag)
                {
                    this.stopBits = value;
                }
            }
        }

        public int ConnectionTimeout
        {
            get
            {
                return this.connectTimeout;
            }
            set
            {
                this.connectTimeout = value;
            }
        }

        public string SerialPort
        {
            get
            {
                return this.serialport.PortName;
            }
            set
            {
                bool flag = value == null;
                if (flag)
                {
                    this.serialport = null;
                }
                else
                {
                    bool flag2 = this.serialport != null;
                    if (flag2)
                    {
                        this.serialport.Close();
                    }
                    this.serialport = new SerialPort();
                    this.serialport.PortName = value;
                    this.serialport.BaudRate = this.baudRate;
                    this.serialport.Parity = this.parity;
                    this.serialport.StopBits = this.stopBits;
                    this.serialport.WriteTimeout = 10000;
                    this.serialport.ReadTimeout = this.connectTimeout;
                    this.serialport.DataReceived += new SerialDataReceivedEventHandler(this.DataReceivedHandler);
                }
            }
        }

        public string LogFileFilename
        {
            get
            {
                return StoreLogData.Instance.Filename;
            }
            set
            {
                StoreLogData.Instance.Filename = value;
                bool flag = StoreLogData.Instance.Filename != null;
                if (flag)
                {
                    this.debug = true;
                }
                else
                {
                    this.debug = false;
                }
            }
        }

        private bool debug = false;

        private TcpClient tcpClient;

        private string ipAddress = "127.0.0.1";

        private int port = 502;

        private uint transactionIdentifierInternal = 0U;

        private byte[] transactionIdentifier = new byte[2];

        private byte[] protocolIdentifier = new byte[2];

        private byte[] crc = new byte[2];

        private byte[] length = new byte[2];

        private byte unitIdentifier = 1;

        private byte functionCode;

        private byte[] startingAddress = new byte[2];

        private byte[] quantity = new byte[2];

        private bool udpFlag = false;

        private int portOut;

        private int baudRate = 9600;

        private int connectTimeout = 1000;

        public byte[] receiveData;

        public byte[] sendData;

        private SerialPort serialport;

        private Parity parity = (Parity)2;

        private StopBits stopBits = (StopBits)1;

        private bool connected = false;

        private int countRetries = 0;

        private NetworkStream stream;

        private bool dataReceived = false;

        private bool receiveActive = false;

        private byte[] readBuffer = new byte[256];

        private int bytesToRead = 0;

        private int akjjjctualPositionToRead = 0;

        private DateTime dateTimeLastRead;

        public enum RegisterOrder
        {
            LowHigh,
            HighLow
        }

        public delegate void ReceiveDataChangedHandler(object sender);

        public delegate void SendDataChangedHandler(object sender);

        public delegate void ConnectedChangedHandler(object sender);
    }
}
