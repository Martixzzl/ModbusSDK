using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace ModbusSDK
{
	internal class TCPHandler
	{
		public event TCPHandler.DataChanged dataChanged;

		public event TCPHandler.NumberOfClientsChanged numberOfClientsChanged;

		public int NumberOfConnectedClients { get; set; }

		public IPAddress LocalIPAddress
		{
			get
			{
				return this.localIPAddress;
			}
		}

		public TCPHandler(int port)
		{
			this.server = new TcpListener(this.LocalIPAddress, port);
			this.server.Start();
			this.server.BeginAcceptTcpClient(new AsyncCallback(this.AcceptTcpClientCallback), null);
		}

		public TCPHandler(IPAddress localIPAddress, int port)
		{
			this.localIPAddress = localIPAddress;
			this.server = new TcpListener(this.LocalIPAddress, port);
			this.server.Start();
			this.server.BeginAcceptTcpClient(new AsyncCallback(this.AcceptTcpClientCallback), null);
		}

		private void AcceptTcpClientCallback(IAsyncResult asyncResult)
		{
			TcpClient tcpClient = new TcpClient();
			try
			{
				tcpClient = this.server.EndAcceptTcpClient(asyncResult);
				tcpClient.ReceiveTimeout = 4000;
				bool flag = this.ipAddress != null;
				if (flag)
				{
					string text = tcpClient.Client.RemoteEndPoint.ToString();
					text = text.Split(new char[]
					{
						':'
					})[0];
					bool flag2 = text != this.ipAddress;
					if (flag2)
					{
						tcpClient.Client.Disconnect(false);
						return;
					}
				}
			}
			catch (Exception)
			{
			}
			try
			{
				this.server.BeginAcceptTcpClient(new AsyncCallback(this.AcceptTcpClientCallback), null);
				TCPHandler.Client client = new TCPHandler.Client(tcpClient);
				NetworkStream networkStream = client.NetworkStream;
				networkStream.ReadTimeout = 4000;
				networkStream.BeginRead(client.Buffer, 0, client.Buffer.Length, new AsyncCallback(this.ReadCallback), client);
			}
			catch (Exception)
			{
			}
		}

		private int GetAndCleanNumberOfConnectedClients(TCPHandler.Client client)
		{
			int count;
			lock (this)
			{
				bool flag2 = false;
				foreach (TCPHandler.Client client2 in this.tcpClientLastRequestList)
				{
					bool flag3 = client.Equals(client2);
					if (flag3)
					{
						flag2 = true;
					}
				}
				try
				{
					this.tcpClientLastRequestList.RemoveAll((TCPHandler.Client c) => checked(DateTime.Now.Ticks - c.Ticks) > 40000000L);
				}
				catch (Exception)
				{
				}
				bool flag4 = !flag2;
				if (flag4)
				{
					this.tcpClientLastRequestList.Add(client);
				}
				count = this.tcpClientLastRequestList.Count;
			}
			return count;
		}

		private void ReadCallback(IAsyncResult asyncResult)
		{
			NetworkConnectionParameter networkConnectionParameter = default(NetworkConnectionParameter);
			TCPHandler.Client client = asyncResult.AsyncState as TCPHandler.Client;
			client.Ticks = DateTime.Now.Ticks;
			this.NumberOfConnectedClients = this.GetAndCleanNumberOfConnectedClients(client);
			bool flag = this.numberOfClientsChanged != null;
			if (flag)
			{
				this.numberOfClientsChanged();
			}
			bool flag2 = client != null;
			if (flag2)
			{
				NetworkStream networkStream = null;
				int num;
				try
				{
					networkStream = client.NetworkStream;
					num = networkStream.EndRead(asyncResult);
				}
				catch (Exception ex)
				{
					return;
				}
				bool flag3 = num == 0;
				if (!flag3)
				{
					byte[] array = new byte[num];
					Buffer.BlockCopy(client.Buffer, 0, array, 0, num);
					networkConnectionParameter.bytes = array;
					networkConnectionParameter.stream = networkStream;
					bool flag4 = this.dataChanged != null;
					if (flag4)
					{
						this.dataChanged(networkConnectionParameter);
					}
					try
					{
						networkStream.BeginRead(client.Buffer, 0, client.Buffer.Length, new AsyncCallback(this.ReadCallback), client);
					}
					catch (Exception)
					{
					}
				}
			}
		}

		public void Disconnect()
		{
			try
			{
				foreach (TCPHandler.Client client in this.tcpClientLastRequestList)
				{
					client.NetworkStream.Close(0);
				}
			}
			catch (Exception)
			{
			}
			this.server.Stop();
		}

		private TcpListener server = null;

		private List<TCPHandler.Client> tcpClientLastRequestList = new List<TCPHandler.Client>();

		public string ipAddress = null;

		private IPAddress localIPAddress = IPAddress.Any;

		public delegate void DataChanged(object networkConnectionParameter);

		public delegate void NumberOfClientsChanged();

		internal class Client
		{
			public long Ticks { get; set; }

			public Client(TcpClient tcpClient)
			{
				this.tcpClient = tcpClient;
				int receiveBufferSize = tcpClient.ReceiveBufferSize;
				this.buffer = new byte[receiveBufferSize];
			}

			public TcpClient TcpClient
			{
				get
				{
					return this.tcpClient;
				}
			}

			public byte[] Buffer
			{
				get
				{
					return this.buffer;
				}
			}

			public NetworkStream NetworkStream
			{
				get
				{
					return this.tcpClient.GetStream();
				}
			}

			private readonly TcpClient tcpClient;

			private readonly byte[] buffer;
		}
	}
}
