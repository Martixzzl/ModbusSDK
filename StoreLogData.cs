using System;
using System.IO;

namespace ModbusSDK
{
	public sealed class StoreLogData
	{
		private StoreLogData()
		{
		}

		public static StoreLogData Instance
		{
			get
			{
				bool flag = StoreLogData.instance == null;
				if (flag)
				{
					object obj = StoreLogData.syncObject;
					lock (obj)
					{
						bool flag3 = StoreLogData.instance == null;
						if (flag3)
						{
							StoreLogData.instance = new StoreLogData();
						}
					}
				}
				return StoreLogData.instance;
			}
		}

		public void Store(string message)
		{
			bool flag = this.filename == null;
			if (!flag)
			{
				using (StreamWriter streamWriter = new StreamWriter(this.Filename, true))
				{
					streamWriter.WriteLine(message);
				}
			}
		}

		public void Store(string message, DateTime timestamp)
		{
			try
			{
				using (StreamWriter streamWriter = new StreamWriter(this.Filename, true))
				{
					streamWriter.WriteLine(timestamp.ToString("dd.MM.yyyy H:mm:ss.ff ") + message);
				}
			}
			catch (Exception ex)
			{
			}
		}

		public string Filename
		{
			get
			{
				return this.filename;
			}
			set
			{
				this.filename = value;
			}
		}

		private string filename = null;

		private static volatile StoreLogData instance;

		private static object syncObject = new object();
	}
}
