using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ModbusSDK;

namespace ModbusTcpExample
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private ModbusClient master;

        private OperateType operateType;

        private ObservableCollection<DataViewModel> dataViewModels;

        private int SelectedIndex;
        private bool isStartReadDatas;

        private int startAddress;
        private int addreCount;
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (o, e) =>
            {
                master = new ModbusClient();
                functionCode.ItemsSource = new string[] { "ReadCoils(0x01)", "ReadDiscreteInputs(0x02)", "ReadHoldingRegister(0x03)", "ReadInputRegister(0x04)", "WriteSingleCoil(0x05)", "WriteSingleRegisterI(0x06)", "WriteMultipleCoils(0x0f)", "WriteMultipleRegister(0x10)" };
                functionCode.SelectedIndex = 0;
                master.ConnectedChanged += Master_ConnectedChanged;
                dataViewModels = new ObservableCollection<DataViewModel>();
                dataTable.ItemsSource = dataViewModels;
                StartReadDatas();
            };
        }
        private async void StartReadDatas()
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    try
                    {
                        if (isStartReadDatas)
                        {
                            if (SelectedIndex == 0)
                            {

                                bool[] result = master.ReadCoils(startAddress, addreCount);

                                for (int i = 0; i < result.Length; i++)
                                {
                                    dataViewModels.ToArray()[i].Value = result[i].ToString();
                                }
                            }
                            else if (SelectedIndex == 1)
                            {
                                bool[] result = master.ReadDiscreteInputs(startAddress, addreCount);
                                for (int i = 0; i < result.Length; i++)
                                {
                                    dataViewModels.ToArray()[i].Value = result[i].ToString();
                                }
                            }
                            else if (SelectedIndex == 2)
                            {
                                int[] result = master.ReadHoldingRegisters(startAddress, addreCount);
                                for (int i = 0; i < result.Length; i++)
                                {
                                    dataViewModels.ToArray()[i].Value = result[i].ToString();
                                }
                            }
                            else if (SelectedIndex == 3)
                            {
                                int[] result = master.ReadInputRegisters(startAddress, addreCount);
                                for (int i = 0; i < result.Length; i++)
                                {
                                    dataViewModels.ToArray()[i].Value = result[i].ToString();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message.ToString());
                    }
                }
            }).GetAwaiter();
        }
        /// <summary>
        /// 连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConnect(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ipTbox.Text))
            {
                ShowMsgBox("请先输入IP", true);
                return;
            }

            if (string.IsNullOrWhiteSpace(portTbox.Text))
            {
                ShowMsgBox("请先输入端口", true);
                return;
            }
            try
            {
                master.Connect(ipTbox.Text, int.Parse(portTbox.Text));
            }
            catch (Exception ex)
            {

                ShowMsgBox(ex.Message, true);
                return;
            }
            if (master.Connected)
            {
                //master.OnResponseData += new ModbusTCP.Master.ResponseData(MBmaster_OnResponseData);
                //master.OnException += new ModbusTCP.Master.ExceptionData(MBmaster_OnException);

                master.ReceiveDataChanged += Master_ReceiveDataChanged;
                
            }

        }

        private void Master_ConnectedChanged(object sender)
        {
            ModbusClient modbus = sender as ModbusClient;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (modbus.Connected)
                {
                    status.Text = "已连接";
                    status.Foreground = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    status.Text = "未连接";
                    status.Foreground = new SolidColorBrush(Colors.Red);
                }
            }));            
        }

        private void Master_ReceiveDataChanged(object sender)
        {

        }

        private void MBmaster_OnResponseData(ushort ID, byte unit, byte function, byte[] values)
        {
            switch (ID)
            {
                case 1:
                    operateType = OperateType.ReadCoils;
                    break;
                case 2:
                    operateType = OperateType.ReadDiscreteInputs;
                    break;
                case 3:
                    operateType = OperateType.ReadHoldingRegister;
                    break;
                case 4:
                    operateType = OperateType.ReadInputRegister;
                    break;
                case 5:
                    operateType = OperateType.WriteSingleCoil;
                    break;
                case 6:
                    operateType = OperateType.WriteMultipleCoils;
                    break;
                case 7:
                    operateType = OperateType.WriteSingleRegister;
                    break;
                case 8:
                    operateType = OperateType.WriteMultipleRegister;
                    break;
            }
        }

        public enum OperateType
        {
            ReadCoils,
            ReadDiscreteInputs,
            ReadHoldingRegister,
            ReadInputRegister,
            WriteSingleCoil,
            WriteMultipleCoils,
            WriteSingleRegister,
            WriteMultipleRegister
        }


        /// <summary>
        /// 取消连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDisConnect(object sender, RoutedEventArgs e)
        {

        }
        /// <summary>
        /// 消息提示
        /// </summary>
        /// <param name="Caption"></param>
        /// <param name="IsError"></param>
        /// <returns></returns>
        public static MessageBoxResult ShowMsgBox(string Caption, bool IsError = false)
        {
            MessageBoxResult dialogResult = IsError
                ? MessageBox.Show(Caption, "错误提醒", MessageBoxButton.OK, MessageBoxImage.Error)
                : MessageBox.Show(Caption, "温馨提示", MessageBoxButton.OKCancel, MessageBoxImage.Information);
            return dialogResult;
        }

        private void OnReadData(object sender, RoutedEventArgs e)
        {
            try
            {
                if (master != null && master.Connected)
                {
                    if (!int.TryParse(StartAddress.Text, out startAddress))
                    {
                        ShowMsgBox("起始地址必须为整数！");
                        return;
                    }
                    else
                    {
                        if (startAddress < 0)
                        {
                            ShowMsgBox("起始地址必须大于等于0！");
                            return;
                        }
                    }

                    if (!int.TryParse(RegisterCount.Text, out addreCount))
                    {
                        ShowMsgBox("地址数量必须为整数！");
                        return;
                    }
                    else
                    {
                        if (addreCount <= 0)
                        {
                            ShowMsgBox("地址数量必须大于0！");
                            return;
                        }
                    }
                    SelectedIndex = functionCode.SelectedIndex;
                    isStartReadDatas = true;
                }
                else
                {
                    ShowMsgBox("请先建立连接！");
                }
            }
            catch (Exception ex)
            {

                ShowMsgBox(ex.Message);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRegisterCountLostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null)
            {
                if (int.TryParse(textBox.Text, out int value))
                {
                    if (value > 0)
                    {
                        dataViewModels = new ObservableCollection<DataViewModel>();
                        for (int i = 1; i <= value; i++)
                        {
                            dataViewModels.Add(new DataViewModel() { Index = i, Value = "" });
                        }
                    }
                }
            }
            dataTable.ItemsSource = dataViewModels;
        }

        private void functionCode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedIndex = functionCode.SelectedIndex;
        }
    }
}
