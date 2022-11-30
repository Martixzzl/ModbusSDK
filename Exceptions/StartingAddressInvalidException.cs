using System;
using System.Runtime.Serialization;

namespace ModbusSDK.Exceptions
{
	public class StartingAddressInvalidException : ModbusException
	{
		
		public StartingAddressInvalidException()
		{
		}

		
		public StartingAddressInvalidException(string message) : base(message)
		{
		}

		
		public StartingAddressInvalidException(string message, Exception innerException) : base(message, innerException)
		{
		}

		
		protected StartingAddressInvalidException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}
