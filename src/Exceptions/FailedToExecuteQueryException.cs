using System;
using Newtonsoft.Json.Linq;

namespace ArmCop.Exceptions
{
	
	public class FailedToExecuteQueryException : SystemException
	{
		public FailedToExecuteQueryException()
		{
		}

		public FailedToExecuteQueryException(string message) : base(message)
		{
		}

		public FailedToExecuteQueryException(string message, JObject serverResponse) : base(message)
		{
			ServerResponse = serverResponse;
		}

		public FailedToExecuteQueryException(JObject serverResponse)
		{
			ServerResponse = serverResponse;
		}

		public FailedToExecuteQueryException(string message, string errorCode, JObject jo) : this(message)
		{
			ErrorCode = errorCode;
			ServerResponse = jo;
		}


		public string ErrorCode { get; set; }


		/// <summary>
		///     Gets or sets the server response.
		/// </summary>
		/// <value>
		///     The server response.
		/// </value>
		public JObject ServerResponse { get; set; }

		public static FailedToExecuteQueryException Parse(string data)
		{
			var jo = JObject.Parse(data);

			if (jo != null)
			{
				var errorCode = jo["error"].Value<string>("code");
				var message = jo["error"].Value<string>("message");
				return new FailedToExecuteQueryException(message, errorCode, jo);
			}

			return new FailedToExecuteQueryException();
		}
	}
}