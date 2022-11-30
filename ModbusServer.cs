using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ModbusSDK.Exceptions;

namespace ModbusSDK
{
    internal class ModbusServer
    {
        public bool FunctionCode1Disabled { get; set; }

        public bool FunctionCode2Disabled { get; set; }

        public bool FunctionCode3Disabled { get; set; }

        public bool FunctionCode4Disabled { get; set; }

        public bool FunctionCode5Disabled { get; set; }

        public bool FunctionCode6Disabled { get; set; }

        public bool FunctionCode15Disabled { get; set; }

        public bool FunctionCode16Disabled { get; set; }

        public bool FunctionCode23Disabled { get; set; }

        public bool PortChanged { get; set; }

        public IPAddress LocalIPAddress
        {
            get
            {
                return this.localIPAddress;
            }
            set
            {
                bool flag = this.listenerThread == null;
                if (flag)
                {
                    this.localIPAddress = value;
                }
            }
        }

        public ModbusServer()
        {
            this.holdingRegisters = new ModbusServer.HoldingRegisters(this);
            this.inputRegisters = new ModbusServer.InputRegisters(this);
            this.coils = new ModbusServer.Coils(this);
            this.discreteInputs = new ModbusServer.DiscreteInputs(this);
        }

        public event ModbusServer.CoilsChangedHandler CoilsChanged;

        public event ModbusServer.HoldingRegistersChangedHandler HoldingRegistersChanged;

        public event ModbusServer.NumberOfConnectedClientsChangedHandler NumberOfConnectedClientsChanged;

        public event ModbusServer.LogDataChangedHandler LogDataChanged;

        public void Listen()
        {
            this.listenerThread = new Thread(new ThreadStart(this.ListenerThread));
            this.listenerThread.Start();
        }

        public void StopListening()
        {
            bool flag = this.SerialFlag & this.serialport != null;
            if (flag)
            {
                bool isOpen = this.serialport.IsOpen;
                if (isOpen)
                {
                    this.serialport.Close();
                }
                this.shouldStop = true;
            }
            try
            {
                this.tcpHandler.Disconnect();
                this.listenerThread.Abort();
            }
            catch (Exception)
            {
            }
            this.listenerThread.Join();
            try
            {
                this.clientConnectionThread.Abort();
            }
            catch (Exception)
            {
            }
        }

        private void ListenerThread()
        {
            bool flag = !this.udpFlag & !this.serialFlag;
            if (flag)
            {
                bool flag2 = this.udpClient != null;
                if (flag2)
                {
                    try
                    {
                        this.udpClient.Close();
                    }
                    catch (Exception)
                    {
                    }
                }
                this.tcpHandler = new TCPHandler(this.LocalIPAddress, this.port);
                bool flag3 = this.debug;
                if (flag3)
                {
                    StoreLogData.Instance.Store(string.Format("ModbusSDK Server listing for incomming data at Port {0}, local IP {1}", this.port, this.LocalIPAddress), DateTime.Now);
                }
                this.tcpHandler.dataChanged += this.ProcessReceivedData;
                this.tcpHandler.numberOfClientsChanged += this.numberOfClientsChanged;
            }
            else
            {
                bool flag4 = this.serialFlag;
                if (flag4)
                {
                    bool flag5 = this.serialport == null;
                    if (flag5)
                    {
                        bool flag6 = this.debug;
                        if (flag6)
                        {
                            StoreLogData.Instance.Store("ModbusSDK RTU-Server listing for incomming data at Serial Port " + this.serialPort, DateTime.Now);
                        }
                        this.serialport = new SerialPort();
                        this.serialport.PortName = this.serialPort;
                        this.serialport.BaudRate = this.baudrate;
                        this.serialport.Parity = this.parity;
                        this.serialport.StopBits = this.stopBits;
                        this.serialport.WriteTimeout = 10000;
                        this.serialport.ReadTimeout = 1000;
                        this.serialport.DataReceived += new SerialDataReceivedEventHandler(this.DataReceivedHandler);
                        this.serialport.Open();
                    }
                }
                else
                {
                    while (!this.shouldStop)
                    {
                        bool flag7 = this.udpFlag;
                        if (flag7)
                        {
                            bool flag8 = this.udpClient == null | this.PortChanged;
                            if (flag8)
                            {
                                IPEndPoint ipendPoint = new IPEndPoint(this.LocalIPAddress, this.port);
                                this.udpClient = new UdpClient(ipendPoint);
                                bool flag9 = this.debug;
                                if (flag9)
                                {
                                    StoreLogData.Instance.Store(string.Format("ModbusSDK Server listing for incomming data at Port {0}, local IP {1}", this.port, this.LocalIPAddress), DateTime.Now);
                                }
                                this.udpClient.Client.ReceiveTimeout = 1000;
                                this.iPEndPoint = new IPEndPoint(IPAddress.Any, this.port);
                                this.PortChanged = false;
                            }
                            bool flag10 = this.tcpHandler != null;
                            if (flag10)
                            {
                                this.tcpHandler.Disconnect();
                            }
                            try
                            {
                                this.bytes = this.udpClient.Receive(ref this.iPEndPoint);
                                this.portIn = this.iPEndPoint.Port;
                                NetworkConnectionParameter networkConnectionParameter = default(NetworkConnectionParameter);
                                networkConnectionParameter.bytes = this.bytes;
                                this.ipAddressIn = this.iPEndPoint.Address;
                                networkConnectionParameter.portIn = this.portIn;
                                networkConnectionParameter.ipAddressIn = this.ipAddressIn;
                                ParameterizedThreadStart parameterizedThreadStart = new ParameterizedThreadStart(this.ProcessReceivedData);
                                Thread thread = new Thread(parameterizedThreadStart);
                                thread.Start(networkConnectionParameter);
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                }
            }
        }

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            int num = 4000 / this.baudrate;
            checked
            {
                bool flag = DateTime.Now.Ticks - this.lastReceive.Ticks > 10000L * unchecked((long)num);
                if (flag)
                {
                    this.nextSign = 0;
                }
                SerialPort serialPort = (SerialPort)sender;
                int bytesToRead = serialPort.BytesToRead;
                byte[] array = new byte[bytesToRead];
                serialPort.Read(array, 0, bytesToRead);
                Array.Copy(array, 0, this.readBuffer, this.nextSign, array.Length);
                this.lastReceive = DateTime.Now;
                this.nextSign = bytesToRead + this.nextSign;
                bool flag2 = ModbusClient.DetectValidModbusFrame(this.readBuffer, this.nextSign);
                if (flag2)
                {
                    this.dataReceived = true;
                    this.nextSign = 0;
                    NetworkConnectionParameter networkConnectionParameter = default(NetworkConnectionParameter);
                    networkConnectionParameter.bytes = this.readBuffer;
                    ParameterizedThreadStart parameterizedThreadStart = new ParameterizedThreadStart(this.ProcessReceivedData);
                    Thread thread = new Thread(parameterizedThreadStart);
                    thread.Start(networkConnectionParameter);
                    this.dataReceived = false;
                }
                else
                {
                    this.dataReceived = false;
                }
            }
        }

        private void numberOfClientsChanged()
        {
            this.numberOfConnections = this.tcpHandler.NumberOfConnectedClients;
            bool flag = this.NumberOfConnectedClientsChanged != null;
            if (flag)
            {
                this.NumberOfConnectedClientsChanged();
            }
        }

        private void ProcessReceivedData(object networkConnectionParameter)
        {
            object obj = this.lockProcessReceivedData;
            checked
            {
                lock (obj)
                {
                    byte[] array = new byte[((NetworkConnectionParameter)networkConnectionParameter).bytes.Length];
                    bool flag2 = this.debug;
                    if (flag2)
                    {
                        StoreLogData.Instance.Store("Received Data: " + BitConverter.ToString(array), DateTime.Now);
                    }
                    NetworkStream stream = ((NetworkConnectionParameter)networkConnectionParameter).stream;
                    int num = ((NetworkConnectionParameter)networkConnectionParameter).portIn;
                    IPAddress ipaddress = ((NetworkConnectionParameter)networkConnectionParameter).ipAddressIn;
                    Array.Copy(((NetworkConnectionParameter)networkConnectionParameter).bytes, 0, array, 0, ((NetworkConnectionParameter)networkConnectionParameter).bytes.Length);
                    ModbusProtocol modbusProtocol = new ModbusProtocol();
                    ModbusProtocol modbusProtocol2 = new ModbusProtocol();
                    try
                    {
                        ushort[] array2 = new ushort[1];
                        byte[] array3 = new byte[2];
                        modbusProtocol.timeStamp = DateTime.Now;
                        modbusProtocol.request = true;
                        bool flag3 = !this.serialFlag;
                        if (flag3)
                        {
                            array3[1] = array[0];
                            array3[0] = array[1];
                            Buffer.BlockCopy(array3, 0, array2, 0, 2);
                            modbusProtocol.transactionIdentifier = array2[0];
                            array3[1] = array[2];
                            array3[0] = array[3];
                            Buffer.BlockCopy(array3, 0, array2, 0, 2);
                            modbusProtocol.protocolIdentifier = array2[0];
                            array3[1] = array[4];
                            array3[0] = array[5];
                            Buffer.BlockCopy(array3, 0, array2, 0, 2);
                            modbusProtocol.length = array2[0];
                        }
                        modbusProtocol.unitIdentifier = array[6 - 6 * Convert.ToInt32(this.serialFlag)];
                        bool flag4 = modbusProtocol.unitIdentifier != this.unitIdentifier & modbusProtocol.unitIdentifier > 0;
                        if (flag4)
                        {
                            return;
                        }
                        modbusProtocol.functionCode = array[7 - 6 * Convert.ToInt32(this.serialFlag)];
                        array3[1] = array[8 - 6 * Convert.ToInt32(this.serialFlag)];
                        array3[0] = array[9 - 6 * Convert.ToInt32(this.serialFlag)];
                        Buffer.BlockCopy(array3, 0, array2, 0, 2);
                        modbusProtocol.startingAdress = array2[0];
                        bool flag5 = modbusProtocol.functionCode <= 4;
                        if (flag5)
                        {
                            array3[1] = array[10 - 6 * Convert.ToInt32(this.serialFlag)];
                            array3[0] = array[11 - 6 * Convert.ToInt32(this.serialFlag)];
                            Buffer.BlockCopy(array3, 0, array2, 0, 2);
                            modbusProtocol.quantity = array2[0];
                        }
                        bool flag6 = modbusProtocol.functionCode == 5;
                        if (flag6)
                        {
                            modbusProtocol.receiveCoilValues = new ushort[1];
                            array3[1] = array[10 - 6 * Convert.ToInt32(this.serialFlag)];
                            array3[0] = array[11 - 6 * Convert.ToInt32(this.serialFlag)];
                            Buffer.BlockCopy(array3, 0, modbusProtocol.receiveCoilValues, 0, 2);
                        }
                        bool flag7 = modbusProtocol.functionCode == 6;
                        if (flag7)
                        {
                            modbusProtocol.receiveRegisterValues = new ushort[1];
                            array3[1] = array[10 - 6 * Convert.ToInt32(this.serialFlag)];
                            array3[0] = array[11 - 6 * Convert.ToInt32(this.serialFlag)];
                            Buffer.BlockCopy(array3, 0, modbusProtocol.receiveRegisterValues, 0, 2);
                        }
                        bool flag8 = modbusProtocol.functionCode == 15;
                        if (flag8)
                        {
                            array3[1] = array[10 - 6 * Convert.ToInt32(this.serialFlag)];
                            array3[0] = array[11 - 6 * Convert.ToInt32(this.serialFlag)];
                            Buffer.BlockCopy(array3, 0, array2, 0, 2);
                            modbusProtocol.quantity = array2[0];
                            modbusProtocol.byteCount = array[12 - 6 * Convert.ToInt32(this.serialFlag)];
                            bool flag9 = modbusProtocol.byteCount % 2 > 0;
                            if (flag9)
                            {
                                modbusProtocol.receiveCoilValues = new ushort[(int)(modbusProtocol.byteCount / 2 + 1)];
                            }
                            else
                            {
                                modbusProtocol.receiveCoilValues = new ushort[(int)(modbusProtocol.byteCount / 2)];
                            }
                            Buffer.BlockCopy(array, 13 - 6 * Convert.ToInt32(this.serialFlag), modbusProtocol.receiveCoilValues, 0, (int)modbusProtocol.byteCount);
                        }
                        bool flag10 = modbusProtocol.functionCode == 16;
                        if (flag10)
                        {
                            array3[1] = array[10 - 6 * Convert.ToInt32(this.serialFlag)];
                            array3[0] = array[11 - 6 * Convert.ToInt32(this.serialFlag)];
                            Buffer.BlockCopy(array3, 0, array2, 0, 2);
                            modbusProtocol.quantity = array2[0];
                            modbusProtocol.byteCount = array[12 - 6 * Convert.ToInt32(this.serialFlag)];
                            modbusProtocol.receiveRegisterValues = new ushort[(int)modbusProtocol.quantity];
                            for (int i = 0; i < (int)modbusProtocol.quantity; i++)
                            {
                                array3[1] = array[13 + i * 2 - 6 * Convert.ToInt32(this.serialFlag)];
                                array3[0] = array[14 + i * 2 - 6 * Convert.ToInt32(this.serialFlag)];
                                Buffer.BlockCopy(array3, 0, modbusProtocol.receiveRegisterValues, i * 2, 2);
                            }
                        }
                        bool flag11 = modbusProtocol.functionCode == 23;
                        if (flag11)
                        {
                            array3[1] = array[8 - 6 * Convert.ToInt32(this.serialFlag)];
                            array3[0] = array[9 - 6 * Convert.ToInt32(this.serialFlag)];
                            Buffer.BlockCopy(array3, 0, array2, 0, 2);
                            modbusProtocol.startingAddressRead = array2[0];
                            array3[1] = array[10 - 6 * Convert.ToInt32(this.serialFlag)];
                            array3[0] = array[11 - 6 * Convert.ToInt32(this.serialFlag)];
                            Buffer.BlockCopy(array3, 0, array2, 0, 2);
                            modbusProtocol.quantityRead = array2[0];
                            array3[1] = array[12 - 6 * Convert.ToInt32(this.serialFlag)];
                            array3[0] = array[13 - 6 * Convert.ToInt32(this.serialFlag)];
                            Buffer.BlockCopy(array3, 0, array2, 0, 2);
                            modbusProtocol.startingAddressWrite = array2[0];
                            array3[1] = array[14 - 6 * Convert.ToInt32(this.serialFlag)];
                            array3[0] = array[15 - 6 * Convert.ToInt32(this.serialFlag)];
                            Buffer.BlockCopy(array3, 0, array2, 0, 2);
                            modbusProtocol.quantityWrite = array2[0];
                            modbusProtocol.byteCount = array[16 - 6 * Convert.ToInt32(this.serialFlag)];
                            modbusProtocol.receiveRegisterValues = new ushort[(int)modbusProtocol.quantityWrite];
                            for (int j = 0; j < (int)modbusProtocol.quantityWrite; j++)
                            {
                                array3[1] = array[17 + j * 2 - 6 * Convert.ToInt32(this.serialFlag)];
                                array3[0] = array[18 + j * 2 - 6 * Convert.ToInt32(this.serialFlag)];
                                Buffer.BlockCopy(array3, 0, modbusProtocol.receiveRegisterValues, j * 2, 2);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                    this.CreateAnswer(modbusProtocol, modbusProtocol2, stream, num, ipaddress);
                    this.CreateLogData(modbusProtocol, modbusProtocol2);
                    bool flag12 = this.LogDataChanged != null;
                    if (flag12)
                    {
                        this.LogDataChanged();
                    }
                }
            }
        }

        private void CreateAnswer(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            byte functionCode = receiveData.functionCode;
            byte b = functionCode;
            checked
            {
                if (b <= 15)
                {
                    switch (b)
                    {
                        case 1:
                            {
                                bool flag = !this.FunctionCode1Disabled;
                                if (flag)
                                {
                                    this.ReadCoils(receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                else
                                {
                                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                                    sendData.exceptionCode = 1;
                                    this.sendException((int)sendData.errorCode, (int)sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                goto IL_3AE;
                            }
                        case 2:
                            {
                                bool flag2 = !this.FunctionCode2Disabled;
                                if (flag2)
                                {
                                    this.ReadDiscreteInputs(receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                else
                                {
                                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                                    sendData.exceptionCode = 1;
                                    this.sendException((int)sendData.errorCode, (int)sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                goto IL_3AE;
                            }
                        case 3:
                            {
                                bool flag3 = !this.FunctionCode3Disabled;
                                if (flag3)
                                {
                                    this.ReadHoldingRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                else
                                {
                                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                                    sendData.exceptionCode = 1;
                                    this.sendException((int)sendData.errorCode, (int)sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                goto IL_3AE;
                            }
                        case 4:
                            {
                                bool flag4 = !this.FunctionCode4Disabled;
                                if (flag4)
                                {
                                    this.ReadInputRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                else
                                {
                                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                                    sendData.exceptionCode = 1;
                                    this.sendException((int)sendData.errorCode, (int)sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                goto IL_3AE;
                            }
                        case 5:
                            {
                                bool flag5 = !this.FunctionCode5Disabled;
                                if (flag5)
                                {
                                    this.WriteSingleCoil(receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                else
                                {
                                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                                    sendData.exceptionCode = 1;
                                    this.sendException((int)sendData.errorCode, (int)sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                goto IL_3AE;
                            }
                        case 6:
                            {
                                bool flag6 = !this.FunctionCode6Disabled;
                                if (flag6)
                                {
                                    this.WriteSingleRegister(receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                else
                                {
                                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                                    sendData.exceptionCode = 1;
                                    this.sendException((int)sendData.errorCode, (int)sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                goto IL_3AE;
                            }
                        default:
                            if (b == 15)
                            {
                                bool flag7 = !this.FunctionCode15Disabled;
                                if (flag7)
                                {
                                    this.WriteMultipleCoils(receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                else
                                {
                                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                                    sendData.exceptionCode = 1;
                                    this.sendException((int)sendData.errorCode, (int)sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                                }
                                goto IL_3AE;
                            }
                            break;
                    }
                }
                else
                {
                    if (b == 16)
                    {
                        bool flag8 = !this.FunctionCode16Disabled;
                        if (flag8)
                        {
                            this.WriteMultipleRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                        }
                        else
                        {
                            sendData.errorCode = (byte)(receiveData.functionCode + 128);
                            sendData.exceptionCode = 1;
                            this.sendException((int)sendData.errorCode, (int)sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                        }
                        goto IL_3AE;
                    }
                    if (b == 23)
                    {
                        bool flag9 = !this.FunctionCode23Disabled;
                        if (flag9)
                        {
                            this.ReadWriteMultipleRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                        }
                        else
                        {
                            sendData.errorCode = (byte)(receiveData.functionCode + 128);
                            sendData.exceptionCode = 1;
                            this.sendException((int)sendData.errorCode, (int)sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
                        }
                        goto IL_3AE;
                    }
                }
                sendData.errorCode = (byte)(receiveData.functionCode + 128);
                sendData.exceptionCode = 1;
                this.sendException((int)sendData.errorCode, (int)sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
            IL_3AE:
                sendData.timeStamp = DateTime.Now;
            }
        }

        private void ReadCoils(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;
            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;
            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            bool flag = receiveData.quantity < 1 | receiveData.quantity > 2000;
            checked
            {
                if (flag)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 3;
                }
                bool flag2 = receiveData.startingAdress + 1 + receiveData.quantity > ushort.MaxValue | receiveData.startingAdress < 0;
                if (flag2)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 2;
                }
                bool flag3 = sendData.exceptionCode == 0;
                if (flag3)
                {
                    bool flag4 = receiveData.quantity % 8 == 0;
                    if (flag4)
                    {
                        sendData.byteCount = (byte)(receiveData.quantity / 8);
                    }
                    else
                    {
                        sendData.byteCount = (byte)(receiveData.quantity / 8 + 1);
                    }
                    sendData.sendCoilValues = new bool[(int)receiveData.quantity];
                    object obj = this.lockCoils;
                    lock (obj)
                    {
                        Array.Copy(this.coils.localArray, (int)(receiveData.startingAdress + 1), sendData.sendCoilValues, 0, (int)receiveData.quantity);
                    }
                }
                bool flag6 = sendData.exceptionCode > 0;
                byte[] array;
                if (flag6)
                {
                    array = new byte[9 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                else
                {
                    array = new byte[(int)(9 + sendData.byteCount) + 2 * Convert.ToInt32(this.serialFlag)];
                }
                byte[] array2 = new byte[2];
                sendData.length = (ushort)((byte)(array.Length - 6));
                array2 = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                array[0] = array2[1];
                array[1] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                array[2] = array2[1];
                array[3] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.length);
                array[4] = array2[1];
                array[5] = array2[0];
                array[6] = sendData.unitIdentifier;
                array[7] = sendData.functionCode;
                array[8] = sendData.byteCount;
                bool flag7 = sendData.exceptionCode > 0;
                if (flag7)
                {
                    array[7] = sendData.errorCode;
                    array[8] = sendData.exceptionCode;
                    sendData.sendCoilValues = null;
                }
                bool flag8 = sendData.sendCoilValues != null;
                if (flag8)
                {
                    for (int i = 0; i < (int)sendData.byteCount; i++)
                    {
                        array2 = new byte[2];
                        for (int j = 0; j < 8; j++)
                        {
                            bool flag9 = sendData.sendCoilValues[i * 8 + j];
                            byte b;
                            if (flag9)
                            {
                                b = 1;
                            }
                            else
                            {
                                b = 0;
                            }
                            array2[1] = (byte)((int)array2[1] | (int)b << j);
                            bool flag10 = i * 8 + j + 1 >= sendData.sendCoilValues.Length;
                            if (flag10)
                            {
                                break;
                            }
                        }
                        array[9 + i] = array2[1];
                    }
                }
                try
                {
                    bool flag11 = this.serialFlag;
                    if (flag11)
                    {
                        bool flag12 = !this.serialport.IsOpen;
                        if (flag12)
                        {
                            throw new SerialPortNotOpenedException("serial port not opened");
                        }
                        sendData.crc = ModbusClient.calculateCRC(array, Convert.ToUInt16(array.Length - 8), 6);
                        array2 = BitConverter.GetBytes((int)sendData.crc);
                        array[array.Length - 2] = array2[0];
                        array[array.Length - 1] = array2[1];
                        this.serialport.Write(array, 6, array.Length - 6);
                        bool flag13 = this.debug;
                        if (flag13)
                        {
                            byte[] array3 = new byte[array.Length - 6];
                            Array.Copy(array, 6, array3, 0, array.Length - 6);
                            bool flag14 = this.debug;
                            if (flag14)
                            {
                                StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array3), DateTime.Now);
                            }
                        }
                    }
                    else
                    {
                        bool flag15 = this.udpFlag;
                        if (flag15)
                        {
                            IPEndPoint ipendPoint = new IPEndPoint(ipAddressIn, portIn);
                            bool flag16 = this.debug;
                            if (flag16)
                            {
                                StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(array), DateTime.Now);
                            }
                            this.udpClient.Send(array, array.Length, ipendPoint);
                        }
                        else
                        {
                            stream.Write(array, 0, array.Length);
                            bool flag17 = this.debug;
                            if (flag17)
                            {
                                StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(array), DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void ReadDiscreteInputs(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;
            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;
            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            bool flag = receiveData.quantity < 1 | receiveData.quantity > 2000;
            checked
            {
                if (flag)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 3;
                }
                bool flag2 = receiveData.startingAdress + 1 + receiveData.quantity > ushort.MaxValue | receiveData.startingAdress < 0;
                if (flag2)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 2;
                }
                bool flag3 = sendData.exceptionCode == 0;
                if (flag3)
                {
                    bool flag4 = receiveData.quantity % 8 == 0;
                    if (flag4)
                    {
                        sendData.byteCount = (byte)(receiveData.quantity / 8);
                    }
                    else
                    {
                        sendData.byteCount = (byte)(receiveData.quantity / 8 + 1);
                    }
                    sendData.sendCoilValues = new bool[(int)receiveData.quantity];
                    Array.Copy(this.discreteInputs.localArray, (int)(receiveData.startingAdress + 1), sendData.sendCoilValues, 0, (int)receiveData.quantity);
                }
                bool flag5 = sendData.exceptionCode > 0;
                byte[] array;
                if (flag5)
                {
                    array = new byte[9 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                else
                {
                    array = new byte[(int)(9 + sendData.byteCount) + 2 * Convert.ToInt32(this.serialFlag)];
                }
                byte[] array2 = new byte[2];
                sendData.length = (ushort)((byte)(array.Length - 6));
                array2 = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                array[0] = array2[1];
                array[1] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                array[2] = array2[1];
                array[3] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.length);
                array[4] = array2[1];
                array[5] = array2[0];
                array[6] = sendData.unitIdentifier;
                array[7] = sendData.functionCode;
                array[8] = sendData.byteCount;
                bool flag6 = sendData.exceptionCode > 0;
                if (flag6)
                {
                    array[7] = sendData.errorCode;
                    array[8] = sendData.exceptionCode;
                    sendData.sendCoilValues = null;
                }
                bool flag7 = sendData.sendCoilValues != null;
                if (flag7)
                {
                    for (int i = 0; i < (int)sendData.byteCount; i++)
                    {
                        array2 = new byte[2];
                        for (int j = 0; j < 8; j++)
                        {
                            bool flag8 = sendData.sendCoilValues[i * 8 + j];
                            byte b;
                            if (flag8)
                            {
                                b = 1;
                            }
                            else
                            {
                                b = 0;
                            }
                            array2[1] = (byte)((int)array2[1] | (int)b << j);
                            bool flag9 = i * 8 + j + 1 >= sendData.sendCoilValues.Length;
                            if (flag9)
                            {
                                break;
                            }
                        }
                        array[9 + i] = array2[1];
                    }
                }
                try
                {
                    bool flag10 = this.serialFlag;
                    if (flag10)
                    {
                        bool flag11 = !this.serialport.IsOpen;
                        if (flag11)
                        {
                            throw new SerialPortNotOpenedException("serial port not opened");
                        }
                        sendData.crc = ModbusClient.calculateCRC(array, Convert.ToUInt16(array.Length - 8), 6);
                        array2 = BitConverter.GetBytes((int)sendData.crc);
                        array[array.Length - 2] = array2[0];
                        array[array.Length - 1] = array2[1];
                        this.serialport.Write(array, 6, array.Length - 6);
                        bool flag12 = this.debug;
                        if (flag12)
                        {
                            byte[] array3 = new byte[array.Length - 6];
                            Array.Copy(array, 6, array3, 0, array.Length - 6);
                            bool flag13 = this.debug;
                            if (flag13)
                            {
                                StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array3), DateTime.Now);
                            }
                        }
                    }
                    else
                    {
                        bool flag14 = this.udpFlag;
                        if (flag14)
                        {
                            IPEndPoint ipendPoint = new IPEndPoint(ipAddressIn, portIn);
                            this.udpClient.Send(array, array.Length, ipendPoint);
                        }
                        else
                        {
                            stream.Write(array, 0, array.Length);
                            bool flag15 = this.debug;
                            if (flag15)
                            {
                                StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(array), DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void ReadHoldingRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;
            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;
            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            bool flag = receiveData.quantity < 1 | receiveData.quantity > 125;
            checked
            {
                if (flag)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 3;
                }
                bool flag2 = receiveData.startingAdress + 1 + receiveData.quantity > ushort.MaxValue | receiveData.startingAdress < 0;
                if (flag2)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 2;
                }
                bool flag3 = sendData.exceptionCode == 0;
                if (flag3)
                {
                    sendData.byteCount = (byte)(2 * receiveData.quantity);
                    sendData.sendRegisterValues = new short[(int)receiveData.quantity];
                    object obj = this.lockHoldingRegisters;
                    lock (obj)
                    {
                        Buffer.BlockCopy(this.holdingRegisters.localArray, (int)(receiveData.startingAdress * 2 + 2), sendData.sendRegisterValues, 0, (int)(receiveData.quantity * 2));
                    }
                }
                bool flag5 = sendData.exceptionCode > 0;
                if (flag5)
                {
                    sendData.length = 3;
                }
                else
                {
                    sendData.length = (ushort)(3 + sendData.byteCount);
                }
                bool flag6 = sendData.exceptionCode > 0;
                byte[] array;
                if (flag6)
                {
                    array = new byte[9 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                else
                {
                    array = new byte[(int)(9 + sendData.byteCount) + 2 * Convert.ToInt32(this.serialFlag)];
                }
                byte[] array2 = new byte[2];
                sendData.length = (ushort)((byte)(array.Length - 6));
                array2 = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                array[0] = array2[1];
                array[1] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                array[2] = array2[1];
                array[3] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.length);
                array[4] = array2[1];
                array[5] = array2[0];
                array[6] = sendData.unitIdentifier;
                array[7] = sendData.functionCode;
                array[8] = sendData.byteCount;
                bool flag7 = sendData.exceptionCode > 0;
                if (flag7)
                {
                    array[7] = sendData.errorCode;
                    array[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                bool flag8 = sendData.sendRegisterValues != null;
                if (flag8)
                {
                    for (int i = 0; i < (int)(sendData.byteCount / 2); i++)
                    {
                        array2 = BitConverter.GetBytes(sendData.sendRegisterValues[i]);
                        array[9 + i * 2] = array2[1];
                        array[10 + i * 2] = array2[0];
                    }
                }
                try
                {
                    bool flag9 = this.serialFlag;
                    if (flag9)
                    {
                        bool flag10 = !this.serialport.IsOpen;
                        if (flag10)
                        {
                            throw new SerialPortNotOpenedException("serial port not opened");
                        }
                        sendData.crc = ModbusClient.calculateCRC(array, Convert.ToUInt16(array.Length - 8), 6);
                        array2 = BitConverter.GetBytes((int)sendData.crc);
                        array[array.Length - 2] = array2[0];
                        array[array.Length - 1] = array2[1];
                        this.serialport.Write(array, 6, array.Length - 6);
                        bool flag11 = this.debug;
                        if (flag11)
                        {
                            byte[] array3 = new byte[array.Length - 6];
                            Array.Copy(array, 6, array3, 0, array.Length - 6);
                            bool flag12 = this.debug;
                            if (flag12)
                            {
                                StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array3), DateTime.Now);
                            }
                        }
                    }
                    else
                    {
                        bool flag13 = this.udpFlag;
                        if (flag13)
                        {
                            IPEndPoint ipendPoint = new IPEndPoint(ipAddressIn, portIn);
                            this.udpClient.Send(array, array.Length, ipendPoint);
                        }
                        else
                        {
                            stream.Write(array, 0, array.Length);
                            bool flag14 = this.debug;
                            if (flag14)
                            {
                                StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(array), DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void ReadInputRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;
            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;
            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            bool flag = receiveData.quantity < 1 | receiveData.quantity > 125;
            checked
            {
                if (flag)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 3;
                }
                bool flag2 = receiveData.startingAdress + 1 + receiveData.quantity > ushort.MaxValue | receiveData.startingAdress < 0;
                if (flag2)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 2;
                }
                bool flag3 = sendData.exceptionCode == 0;
                if (flag3)
                {
                    sendData.byteCount = (byte)(2 * receiveData.quantity);
                    sendData.sendRegisterValues = new short[(int)receiveData.quantity];
                    Buffer.BlockCopy(this.inputRegisters.localArray, (int)(receiveData.startingAdress * 2 + 2), sendData.sendRegisterValues, 0, (int)(receiveData.quantity * 2));
                }
                bool flag4 = sendData.exceptionCode > 0;
                if (flag4)
                {
                    sendData.length = 3;
                }
                else
                {
                    sendData.length = (ushort)(3 + sendData.byteCount);
                }
                bool flag5 = sendData.exceptionCode > 0;
                byte[] array;
                if (flag5)
                {
                    array = new byte[9 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                else
                {
                    array = new byte[(int)(9 + sendData.byteCount) + 2 * Convert.ToInt32(this.serialFlag)];
                }
                byte[] array2 = new byte[2];
                sendData.length = (ushort)((byte)(array.Length - 6));
                array2 = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                array[0] = array2[1];
                array[1] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                array[2] = array2[1];
                array[3] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.length);
                array[4] = array2[1];
                array[5] = array2[0];
                array[6] = sendData.unitIdentifier;
                array[7] = sendData.functionCode;
                array[8] = sendData.byteCount;
                bool flag6 = sendData.exceptionCode > 0;
                if (flag6)
                {
                    array[7] = sendData.errorCode;
                    array[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                bool flag7 = sendData.sendRegisterValues != null;
                if (flag7)
                {
                    for (int i = 0; i < (int)(sendData.byteCount / 2); i++)
                    {
                        array2 = BitConverter.GetBytes(sendData.sendRegisterValues[i]);
                        array[9 + i * 2] = array2[1];
                        array[10 + i * 2] = array2[0];
                    }
                }
                try
                {
                    bool flag8 = this.serialFlag;
                    if (flag8)
                    {
                        bool flag9 = !this.serialport.IsOpen;
                        if (flag9)
                        {
                            throw new SerialPortNotOpenedException("serial port not opened");
                        }
                        sendData.crc = ModbusClient.calculateCRC(array, Convert.ToUInt16(array.Length - 8), 6);
                        array2 = BitConverter.GetBytes((int)sendData.crc);
                        array[array.Length - 2] = array2[0];
                        array[array.Length - 1] = array2[1];
                        this.serialport.Write(array, 6, array.Length - 6);
                        bool flag10 = this.debug;
                        if (flag10)
                        {
                            byte[] array3 = new byte[array.Length - 6];
                            Array.Copy(array, 6, array3, 0, array.Length - 6);
                            bool flag11 = this.debug;
                            if (flag11)
                            {
                                StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array3), DateTime.Now);
                            }
                        }
                    }
                    else
                    {
                        bool flag12 = this.udpFlag;
                        if (flag12)
                        {
                            IPEndPoint ipendPoint = new IPEndPoint(ipAddressIn, portIn);
                            this.udpClient.Send(array, array.Length, ipendPoint);
                        }
                        else
                        {
                            stream.Write(array, 0, array.Length);
                            bool flag13 = this.debug;
                            if (flag13)
                            {
                                StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(array), DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void WriteSingleCoil(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;
            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;
            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            sendData.startingAdress = receiveData.startingAdress;
            sendData.receiveCoilValues = receiveData.receiveCoilValues;
            bool flag = receiveData.receiveCoilValues[0] > 0 & receiveData.receiveCoilValues[0] != 65280;
            checked
            {
                if (flag)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 3;
                }
                bool flag2 = receiveData.startingAdress + 1 > ushort.MaxValue | receiveData.startingAdress < 0;
                if (flag2)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 2;
                }
                bool flag3 = sendData.exceptionCode == 0;
                if (flag3)
                {
                    bool flag4 = receiveData.receiveCoilValues[0] == 65280;
                    if (flag4)
                    {
                        object obj = this.lockCoils;
                        lock (obj)
                        {
                            this.coils[(int)(receiveData.startingAdress + 1)] = true;
                        }
                    }
                    bool flag6 = receiveData.receiveCoilValues[0] == 0;
                    if (flag6)
                    {
                        object obj2 = this.lockCoils;
                        lock (obj2)
                        {
                            this.coils[(int)(receiveData.startingAdress + 1)] = false;
                        }
                    }
                }
                bool flag8 = sendData.exceptionCode > 0;
                if (flag8)
                {
                    sendData.length = 3;
                }
                else
                {
                    sendData.length = 6;
                }
                bool flag9 = sendData.exceptionCode > 0;
                byte[] array;
                if (flag9)
                {
                    array = new byte[9 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                else
                {
                    array = new byte[12 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                byte[] array2 = new byte[2];
                sendData.length = (ushort)((byte)(array.Length - 6));
                array2 = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                array[0] = array2[1];
                array[1] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                array[2] = array2[1];
                array[3] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.length);
                array[4] = array2[1];
                array[5] = array2[0];
                array[6] = sendData.unitIdentifier;
                array[7] = sendData.functionCode;
                bool flag10 = sendData.exceptionCode > 0;
                if (flag10)
                {
                    array[7] = sendData.errorCode;
                    array[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    array2 = BitConverter.GetBytes((int)receiveData.startingAdress);
                    array[8] = array2[1];
                    array[9] = array2[0];
                    array2 = BitConverter.GetBytes((int)receiveData.receiveCoilValues[0]);
                    array[10] = array2[1];
                    array[11] = array2[0];
                }
                try
                {
                    bool flag11 = this.serialFlag;
                    if (flag11)
                    {
                        bool flag12 = !this.serialport.IsOpen;
                        if (flag12)
                        {
                            throw new SerialPortNotOpenedException("serial port not opened");
                        }
                        sendData.crc = ModbusClient.calculateCRC(array, Convert.ToUInt16(array.Length - 8), 6);
                        array2 = BitConverter.GetBytes((int)sendData.crc);
                        array[array.Length - 2] = array2[0];
                        array[array.Length - 1] = array2[1];
                        this.serialport.Write(array, 6, array.Length - 6);
                        bool flag13 = this.debug;
                        if (flag13)
                        {
                            byte[] array3 = new byte[array.Length - 6];
                            Array.Copy(array, 6, array3, 0, array.Length - 6);
                            bool flag14 = this.debug;
                            if (flag14)
                            {
                                StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array3), DateTime.Now);
                            }
                        }
                    }
                    else
                    {
                        bool flag15 = this.udpFlag;
                        if (flag15)
                        {
                            IPEndPoint ipendPoint = new IPEndPoint(ipAddressIn, portIn);
                            this.udpClient.Send(array, array.Length, ipendPoint);
                        }
                        else
                        {
                            stream.Write(array, 0, array.Length);
                            bool flag16 = this.debug;
                            if (flag16)
                            {
                                StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(array), DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
                bool flag17 = this.CoilsChanged != null;
                if (flag17)
                {
                    this.CoilsChanged((int)(receiveData.startingAdress + 1), 1);
                }
            }
        }

        private void WriteSingleRegister(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;
            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;
            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            sendData.startingAdress = receiveData.startingAdress;
            sendData.receiveRegisterValues = receiveData.receiveRegisterValues;
            bool flag = receiveData.receiveRegisterValues[0] < 0 | receiveData.receiveRegisterValues[0] > ushort.MaxValue;
            checked
            {
                if (flag)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 3;
                }
                bool flag2 = receiveData.startingAdress + 1 > ushort.MaxValue | receiveData.startingAdress < 0;
                if (flag2)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 2;
                }
                bool flag3 = sendData.exceptionCode == 0;
                if (flag3)
                {
                    object obj = this.lockHoldingRegisters;
                    lock (obj)
                    {
                        this.holdingRegisters[(int)(receiveData.startingAdress + 1)] = unchecked((short)receiveData.receiveRegisterValues[0]);
                    }
                }
                bool flag5 = sendData.exceptionCode > 0;
                if (flag5)
                {
                    sendData.length = 3;
                }
                else
                {
                    sendData.length = 6;
                }
                bool flag6 = sendData.exceptionCode > 0;
                byte[] array;
                if (flag6)
                {
                    array = new byte[9 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                else
                {
                    array = new byte[12 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                byte[] array2 = new byte[2];
                sendData.length = (ushort)((byte)(array.Length - 6));
                array2 = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                array[0] = array2[1];
                array[1] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                array[2] = array2[1];
                array[3] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.length);
                array[4] = array2[1];
                array[5] = array2[0];
                array[6] = sendData.unitIdentifier;
                array[7] = sendData.functionCode;
                bool flag7 = sendData.exceptionCode > 0;
                if (flag7)
                {
                    array[7] = sendData.errorCode;
                    array[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    array2 = BitConverter.GetBytes((int)receiveData.startingAdress);
                    array[8] = array2[1];
                    array[9] = array2[0];
                    array2 = BitConverter.GetBytes((int)receiveData.receiveRegisterValues[0]);
                    array[10] = array2[1];
                    array[11] = array2[0];
                }
                try
                {
                    bool flag8 = this.serialFlag;
                    if (flag8)
                    {
                        bool flag9 = !this.serialport.IsOpen;
                        if (flag9)
                        {
                            throw new SerialPortNotOpenedException("serial port not opened");
                        }
                        sendData.crc = ModbusClient.calculateCRC(array, Convert.ToUInt16(array.Length - 8), 6);
                        array2 = BitConverter.GetBytes((int)sendData.crc);
                        array[array.Length - 2] = array2[0];
                        array[array.Length - 1] = array2[1];
                        this.serialport.Write(array, 6, array.Length - 6);
                        bool flag10 = this.debug;
                        if (flag10)
                        {
                            byte[] array3 = new byte[array.Length - 6];
                            Array.Copy(array, 6, array3, 0, array.Length - 6);
                            bool flag11 = this.debug;
                            if (flag11)
                            {
                                StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array3), DateTime.Now);
                            }
                        }
                    }
                    else
                    {
                        bool flag12 = this.udpFlag;
                        if (flag12)
                        {
                            IPEndPoint ipendPoint = new IPEndPoint(ipAddressIn, portIn);
                            this.udpClient.Send(array, array.Length, ipendPoint);
                        }
                        else
                        {
                            stream.Write(array, 0, array.Length);
                            bool flag13 = this.debug;
                            if (flag13)
                            {
                                StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(array), DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
                bool flag14 = this.HoldingRegistersChanged != null;
                if (flag14)
                {
                    this.HoldingRegistersChanged((int)(receiveData.startingAdress + 1), 1);
                }
            }
        }

        private void WriteMultipleCoils(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;
            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;
            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            sendData.startingAdress = receiveData.startingAdress;
            sendData.quantity = receiveData.quantity;
            bool flag = receiveData.quantity == 0 | receiveData.quantity > 1968;
            checked
            {
                if (flag)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 3;
                }
                bool flag2 = receiveData.startingAdress + 1 + receiveData.quantity > ushort.MaxValue | receiveData.startingAdress < 0;
                if (flag2)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 2;
                }
                bool flag3 = sendData.exceptionCode == 0;
                if (flag3)
                {
                    object obj = this.lockCoils;
                    lock (obj)
                    {
                        for (int i = 0; i < (int)receiveData.quantity; i++)
                        {
                            int num = i % 16;
                            int num2 = 1;
                            num2 <<= num;
                            bool flag5 = (receiveData.receiveCoilValues[i / 16] & (ushort)num2) == 0;
                            if (flag5)
                            {
                                this.coils[(int)receiveData.startingAdress + i + 1] = false;
                            }
                            else
                            {
                                this.coils[(int)receiveData.startingAdress + i + 1] = true;
                            }
                        }
                    }
                }
                bool flag6 = sendData.exceptionCode > 0;
                if (flag6)
                {
                    sendData.length = 3;
                }
                else
                {
                    sendData.length = 6;
                }
                bool flag7 = sendData.exceptionCode > 0;
                byte[] array;
                if (flag7)
                {
                    array = new byte[9 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                else
                {
                    array = new byte[12 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                byte[] array2 = new byte[2];
                sendData.length = (ushort)((byte)(array.Length - 6));
                array2 = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                array[0] = array2[1];
                array[1] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                array[2] = array2[1];
                array[3] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.length);
                array[4] = array2[1];
                array[5] = array2[0];
                array[6] = sendData.unitIdentifier;
                array[7] = sendData.functionCode;
                bool flag8 = sendData.exceptionCode > 0;
                if (flag8)
                {
                    array[7] = sendData.errorCode;
                    array[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    array2 = BitConverter.GetBytes((int)receiveData.startingAdress);
                    array[8] = array2[1];
                    array[9] = array2[0];
                    array2 = BitConverter.GetBytes((int)receiveData.quantity);
                    array[10] = array2[1];
                    array[11] = array2[0];
                }
                try
                {
                    bool flag9 = this.serialFlag;
                    if (flag9)
                    {
                        bool flag10 = !this.serialport.IsOpen;
                        if (flag10)
                        {
                            throw new SerialPortNotOpenedException("serial port not opened");
                        }
                        sendData.crc = ModbusClient.calculateCRC(array, Convert.ToUInt16(array.Length - 8), 6);
                        array2 = BitConverter.GetBytes((int)sendData.crc);
                        array[array.Length - 2] = array2[0];
                        array[array.Length - 1] = array2[1];
                        this.serialport.Write(array, 6, array.Length - 6);
                        bool flag11 = this.debug;
                        if (flag11)
                        {
                            byte[] array3 = new byte[array.Length - 6];
                            Array.Copy(array, 6, array3, 0, array.Length - 6);
                            bool flag12 = this.debug;
                            if (flag12)
                            {
                                StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array3), DateTime.Now);
                            }
                        }
                    }
                    else
                    {
                        bool flag13 = this.udpFlag;
                        if (flag13)
                        {
                            IPEndPoint ipendPoint = new IPEndPoint(ipAddressIn, portIn);
                            this.udpClient.Send(array, array.Length, ipendPoint);
                        }
                        else
                        {
                            stream.Write(array, 0, array.Length);
                            bool flag14 = this.debug;
                            if (flag14)
                            {
                                StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(array), DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
                bool flag15 = this.CoilsChanged != null;
                if (flag15)
                {
                    this.CoilsChanged((int)(receiveData.startingAdress + 1), (int)receiveData.quantity);
                }
            }
        }

        private void WriteMultipleRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;
            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;
            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            sendData.startingAdress = receiveData.startingAdress;
            sendData.quantity = receiveData.quantity;
            bool flag = receiveData.quantity == 0 | receiveData.quantity > 1968;
            checked
            {
                if (flag)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 3;
                }
                bool flag2 = receiveData.startingAdress + 1 + receiveData.quantity > ushort.MaxValue | receiveData.startingAdress < 0;
                if (flag2)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 2;
                }
                bool flag3 = sendData.exceptionCode == 0;
                if (flag3)
                {
                    object obj = this.lockHoldingRegisters;
                    lock (obj)
                    {
                        for (int i = 0; i < (int)receiveData.quantity; i++)
                        {
                            this.holdingRegisters[(int)receiveData.startingAdress + i + 1] = unchecked((short)receiveData.receiveRegisterValues[i]);
                        }
                    }
                }
                bool flag5 = sendData.exceptionCode > 0;
                if (flag5)
                {
                    sendData.length = 3;
                }
                else
                {
                    sendData.length = 6;
                }
                bool flag6 = sendData.exceptionCode > 0;
                byte[] array;
                if (flag6)
                {
                    array = new byte[9 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                else
                {
                    array = new byte[12 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                byte[] array2 = new byte[2];
                sendData.length = (ushort)((byte)(array.Length - 6));
                array2 = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                array[0] = array2[1];
                array[1] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                array[2] = array2[1];
                array[3] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.length);
                array[4] = array2[1];
                array[5] = array2[0];
                array[6] = sendData.unitIdentifier;
                array[7] = sendData.functionCode;
                bool flag7 = sendData.exceptionCode > 0;
                if (flag7)
                {
                    array[7] = sendData.errorCode;
                    array[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    array2 = BitConverter.GetBytes((int)receiveData.startingAdress);
                    array[8] = array2[1];
                    array[9] = array2[0];
                    array2 = BitConverter.GetBytes((int)receiveData.quantity);
                    array[10] = array2[1];
                    array[11] = array2[0];
                }
                try
                {
                    bool flag8 = this.serialFlag;
                    if (flag8)
                    {
                        bool flag9 = !this.serialport.IsOpen;
                        if (flag9)
                        {
                            throw new SerialPortNotOpenedException("serial port not opened");
                        }
                        sendData.crc = ModbusClient.calculateCRC(array, Convert.ToUInt16(array.Length - 8), 6);
                        array2 = BitConverter.GetBytes((int)sendData.crc);
                        array[array.Length - 2] = array2[0];
                        array[array.Length - 1] = array2[1];
                        this.serialport.Write(array, 6, array.Length - 6);
                        bool flag10 = this.debug;
                        if (flag10)
                        {
                            byte[] array3 = new byte[array.Length - 6];
                            Array.Copy(array, 6, array3, 0, array.Length - 6);
                            bool flag11 = this.debug;
                            if (flag11)
                            {
                                StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array3), DateTime.Now);
                            }
                        }
                    }
                    else
                    {
                        bool flag12 = this.udpFlag;
                        if (flag12)
                        {
                            IPEndPoint ipendPoint = new IPEndPoint(ipAddressIn, portIn);
                            this.udpClient.Send(array, array.Length, ipendPoint);
                        }
                        else
                        {
                            stream.Write(array, 0, array.Length);
                            bool flag13 = this.debug;
                            if (flag13)
                            {
                                StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(array), DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
                bool flag14 = this.HoldingRegistersChanged != null;
                if (flag14)
                {
                    this.HoldingRegistersChanged((int)(receiveData.startingAdress + 1), (int)receiveData.quantity);
                }
            }
        }

        private void ReadWriteMultipleRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;
            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;
            sendData.unitIdentifier = this.unitIdentifier;
            sendData.functionCode = receiveData.functionCode;
            checked
            {
                bool flag = receiveData.quantityRead < 1 | receiveData.quantityRead > 125 | receiveData.quantityWrite < 1 | receiveData.quantityWrite > 121 | (ushort)receiveData.byteCount != receiveData.quantityWrite * 2;
                if (flag)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 3;
                }
                bool flag2 = receiveData.startingAddressRead + 1 + receiveData.quantityRead > ushort.MaxValue | receiveData.startingAddressWrite + 1 + receiveData.quantityWrite > ushort.MaxValue | receiveData.quantityWrite < 0 | receiveData.quantityRead < 0;
                if (flag2)
                {
                    sendData.errorCode = (byte)(receiveData.functionCode + 128);
                    sendData.exceptionCode = 2;
                }
                bool flag3 = sendData.exceptionCode == 0;
                if (flag3)
                {
                    sendData.sendRegisterValues = new short[(int)receiveData.quantityRead];
                    object obj = this.lockHoldingRegisters;
                    lock (obj)
                    {
                        Buffer.BlockCopy(this.holdingRegisters.localArray, (int)(receiveData.startingAddressRead * 2 + 2), sendData.sendRegisterValues, 0, (int)(receiveData.quantityRead * 2));
                    }
                    ModbusServer.HoldingRegisters holdingRegisters = this.holdingRegisters;
                    lock (holdingRegisters)
                    {
                        for (int i = 0; i < (int)receiveData.quantityWrite; i++)
                        {
                            this.holdingRegisters[(int)receiveData.startingAddressWrite + i + 1] = unchecked((short)receiveData.receiveRegisterValues[i]);
                        }
                    }
                    sendData.byteCount = (byte)(2 * receiveData.quantityRead);
                }
                bool flag6 = sendData.exceptionCode > 0;
                if (flag6)
                {
                    sendData.length = 3;
                }
                else
                {
                    sendData.length = Convert.ToUInt16((int)(3 + 2 * receiveData.quantityRead));
                }
                bool flag7 = sendData.exceptionCode > 0;
                byte[] array;
                if (flag7)
                {
                    array = new byte[9 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                else
                {
                    array = new byte[(int)(9 + sendData.byteCount) + 2 * Convert.ToInt32(this.serialFlag)];
                }
                byte[] array2 = new byte[2];
                array2 = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                array[0] = array2[1];
                array[1] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                array[2] = array2[1];
                array[3] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.length);
                array[4] = array2[1];
                array[5] = array2[0];
                array[6] = sendData.unitIdentifier;
                array[7] = sendData.functionCode;
                array[8] = sendData.byteCount;
                bool flag8 = sendData.exceptionCode > 0;
                if (flag8)
                {
                    array[7] = sendData.errorCode;
                    array[8] = sendData.exceptionCode;
                    sendData.sendRegisterValues = null;
                }
                else
                {
                    bool flag9 = sendData.sendRegisterValues != null;
                    if (flag9)
                    {
                        for (int j = 0; j < (int)(sendData.byteCount / 2); j++)
                        {
                            array2 = BitConverter.GetBytes(sendData.sendRegisterValues[j]);
                            array[9 + j * 2] = array2[1];
                            array[10 + j * 2] = array2[0];
                        }
                    }
                }
                try
                {
                    bool flag10 = this.serialFlag;
                    if (flag10)
                    {
                        bool flag11 = !this.serialport.IsOpen;
                        if (flag11)
                        {
                            throw new SerialPortNotOpenedException("serial port not opened");
                        }
                        sendData.crc = ModbusClient.calculateCRC(array, Convert.ToUInt16(array.Length - 8), 6);
                        array2 = BitConverter.GetBytes((int)sendData.crc);
                        array[array.Length - 2] = array2[0];
                        array[array.Length - 1] = array2[1];
                        this.serialport.Write(array, 6, array.Length - 6);
                        bool flag12 = this.debug;
                        if (flag12)
                        {
                            byte[] array3 = new byte[array.Length - 6];
                            Array.Copy(array, 6, array3, 0, array.Length - 6);
                            bool flag13 = this.debug;
                            if (flag13)
                            {
                                StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array3), DateTime.Now);
                            }
                        }
                    }
                    else
                    {
                        bool flag14 = this.udpFlag;
                        if (flag14)
                        {
                            IPEndPoint ipendPoint = new IPEndPoint(ipAddressIn, portIn);
                            this.udpClient.Send(array, array.Length, ipendPoint);
                        }
                        else
                        {
                            stream.Write(array, 0, array.Length);
                            bool flag15 = this.debug;
                            if (flag15)
                            {
                                StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(array), DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
                bool flag16 = this.HoldingRegistersChanged != null;
                if (flag16)
                {
                    this.HoldingRegistersChanged((int)(receiveData.startingAddressWrite + 1), (int)receiveData.quantityWrite);
                }
            }
        }

        private void sendException(int errorCode, int exceptionCode, ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
        {
            sendData.response = true;
            sendData.transactionIdentifier = receiveData.transactionIdentifier;
            sendData.protocolIdentifier = receiveData.protocolIdentifier;
            sendData.unitIdentifier = receiveData.unitIdentifier;
            checked
            {
                sendData.errorCode = (byte)errorCode;
                sendData.exceptionCode = (byte)exceptionCode;
                bool flag = sendData.exceptionCode > 0;
                if (flag)
                {
                    sendData.length = 3;
                }
                else
                {
                    sendData.length = (ushort)(3 + sendData.byteCount);
                }
                bool flag2 = sendData.exceptionCode > 0;
                byte[] array;
                if (flag2)
                {
                    array = new byte[9 + 2 * Convert.ToInt32(this.serialFlag)];
                }
                else
                {
                    array = new byte[(int)(9 + sendData.byteCount) + 2 * Convert.ToInt32(this.serialFlag)];
                }
                byte[] array2 = new byte[2];
                sendData.length = (ushort)((byte)(array.Length - 6));
                array2 = BitConverter.GetBytes((int)sendData.transactionIdentifier);
                array[0] = array2[1];
                array[1] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.protocolIdentifier);
                array[2] = array2[1];
                array[3] = array2[0];
                array2 = BitConverter.GetBytes((int)sendData.length);
                array[4] = array2[1];
                array[5] = array2[0];
                array[6] = sendData.unitIdentifier;
                array[7] = sendData.errorCode;
                array[8] = sendData.exceptionCode;
                try
                {
                    bool flag3 = this.serialFlag;
                    if (flag3)
                    {
                        bool flag4 = !this.serialport.IsOpen;
                        if (flag4)
                        {
                            throw new SerialPortNotOpenedException("serial port not opened");
                        }
                        sendData.crc = ModbusClient.calculateCRC(array, Convert.ToUInt16(array.Length - 8), 6);
                        array2 = BitConverter.GetBytes((int)sendData.crc);
                        array[array.Length - 2] = array2[0];
                        array[array.Length - 1] = array2[1];
                        this.serialport.Write(array, 6, array.Length - 6);
                        bool flag5 = this.debug;
                        if (flag5)
                        {
                            byte[] array3 = new byte[array.Length - 6];
                            Array.Copy(array, 6, array3, 0, array.Length - 6);
                            bool flag6 = this.debug;
                            if (flag6)
                            {
                                StoreLogData.Instance.Store("Send Serial-Data: " + BitConverter.ToString(array3), DateTime.Now);
                            }
                        }
                    }
                    else
                    {
                        bool flag7 = this.udpFlag;
                        if (flag7)
                        {
                            IPEndPoint ipendPoint = new IPEndPoint(ipAddressIn, portIn);
                            this.udpClient.Send(array, array.Length, ipendPoint);
                        }
                        else
                        {
                            stream.Write(array, 0, array.Length);
                            bool flag8 = this.debug;
                            if (flag8)
                            {
                                StoreLogData.Instance.Store("Send Data: " + BitConverter.ToString(array), DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void CreateLogData(ModbusProtocol receiveData, ModbusProtocol sendData)
        {
            checked
            {
                for (int i = 0; i < 98; i++)
                {
                    this.modbusLogData[99 - i] = this.modbusLogData[99 - i - 2];
                }
                this.modbusLogData[0] = receiveData;
                this.modbusLogData[1] = sendData;
            }
        }

        public int NumberOfConnections
        {
            get
            {
                return this.numberOfConnections;
            }
        }

        public ModbusProtocol[] ModbusLogData
        {
            get
            {
                return this.modbusLogData;
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

        public bool SerialFlag
        {
            get
            {
                return this.serialFlag;
            }
            set
            {
                this.serialFlag = value;
            }
        }

        public int Baudrate
        {
            get
            {
                return this.baudrate;
            }
            set
            {
                this.baudrate = value;
            }
        }

        public Parity Parity
        {
            get
            {
                return this.parity;
            }
            set
            {
                this.parity = value;
            }
        }

        public StopBits StopBits
        {
            get
            {
                return this.stopBits;
            }
            set
            {
                this.stopBits = value;
            }
        }

        public string SerialPort
        {
            get
            {
                return this.serialPort;
            }
            set
            {
                this.serialPort = value;
                bool flag = this.serialPort != null;
                if (flag)
                {
                    this.serialFlag = true;
                }
                else
                {
                    this.serialFlag = false;
                }
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

        private int port = 502;

        private ModbusProtocol receiveData;

        private ModbusProtocol sendData = new ModbusProtocol();

        private byte[] bytes = new byte[2100];

        public ModbusServer.HoldingRegisters holdingRegisters;

        public ModbusServer.InputRegisters inputRegisters;

        public ModbusServer.Coils coils;

        public ModbusServer.DiscreteInputs discreteInputs;

        private int numberOfConnections = 0;

        private bool udpFlag;

        private bool serialFlag;

        private int baudrate = 9600;

        private Parity parity = (Parity)2;

        private StopBits stopBits = (StopBits)1;

        private string serialPort = "COM1";

        private SerialPort serialport;

        private byte unitIdentifier = 1;

        private int portIn;

        private IPAddress ipAddressIn;

        private UdpClient udpClient;

        private IPEndPoint iPEndPoint;

        private TCPHandler tcpHandler;

        private Thread listenerThread;

        private Thread clientConnectionThread;

        private ModbusProtocol[] modbusLogData = new ModbusProtocol[100];

        private object lockCoils = new object();

        private object lockHoldingRegisters = new object();

        private volatile bool shouldStop;

        private IPAddress localIPAddress = IPAddress.Any;

        private bool dataReceived = false;

        private byte[] readBuffer = new byte[2094];

        private DateTime lastReceive;

        private int nextSign = 0;

        private object lockProcessReceivedData = new object();

        public delegate void CoilsChangedHandler(int coil, int numberOfCoils);

        public delegate void HoldingRegistersChangedHandler(int register, int numberOfRegisters);

        public delegate void NumberOfConnectedClientsChangedHandler();

        public delegate void LogDataChangedHandler();

        public class HoldingRegisters
        {
            public HoldingRegisters(ModbusServer modbusServer)
            {
                this.modbusServer = modbusServer;
            }

            public short this[int x]
            {
                get
                {
                    return this.localArray[x];
                }
                set
                {
                    this.localArray[x] = value;
                }
            }

            public short[] localArray = new short[65535];

            private ModbusServer modbusServer;
        }

        public class InputRegisters
        {
            public InputRegisters(ModbusServer modbusServer)
            {
                this.modbusServer = modbusServer;
            }

            public short this[int x]
            {
                get
                {
                    return this.localArray[x];
                }
                set
                {
                    this.localArray[x] = value;
                }
            }

            public short[] localArray = new short[65535];

            private ModbusServer modbusServer;
        }

        public class Coils
        {
            public Coils(ModbusServer modbusServer)
            {
                this.modbusServer = modbusServer;
            }

            public bool this[int x]
            {
                get
                {
                    return this.localArray[x];
                }
                set
                {
                    this.localArray[x] = value;
                }
            }

            public bool[] localArray = new bool[65535];

            private ModbusServer modbusServer;
        }

        public class DiscreteInputs
        {
            public DiscreteInputs(ModbusServer modbusServer)
            {
                this.modbusServer = modbusServer;
            }

            public bool this[int x]
            {
                get
                {
                    return this.localArray[x];
                }
                set
                {
                    this.localArray[x] = value;
                }
            }

            public bool[] localArray = new bool[65535];

            private ModbusServer modbusServer;
        }
    }
}
