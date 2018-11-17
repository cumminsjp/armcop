using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ArmCop
{
	public class ArcGISServerErrorException : SystemException
	{
		public ArcGISServerErrorException() : base()
		{

		}

		public ArcGISServerErrorException(string message) : base(message)
		{

		}

		public ArcGISServerErrorException(string message, JObject serverResponse) : base(message)
		{
			ServerResponse = serverResponse;
		}

		public ArcGISServerErrorException(JObject serverResponse)
		{
			ServerResponse = serverResponse;
		}


		/// <summary>
		/// Gets or sets the server response.
		/// </summary>
		/// <value>
		/// The server response.
		/// </value>
		public JObject ServerResponse { get; set; }
	}
}
