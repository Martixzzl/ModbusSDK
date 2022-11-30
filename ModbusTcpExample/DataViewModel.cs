using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusTcpExample
{
    public class DataViewModel : GalaSoft.MvvmLight.ObservableObject
    {
        private int index;
        private string _value;

        public int Index
        {
            get { return index; }
            set
            {
                if (index != value)
                {
                    index = value;
                    RaisePropertyChanged("Index");
                }
            }
        }

        public string Value
        {
            get { return _value; }
            set
            {
                if (_value != value)
                {
                    _value = value;
                    RaisePropertyChanged("Value");
                }
            }
        }
    }
}
