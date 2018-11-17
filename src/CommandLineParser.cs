using System.Reflection;
using CommandLine;
using Common.Logging;
// ReSharper disable UnusedMember.Global

namespace ArmCop
{
	public class CommandLineOptions
	{
		/// <summary>
		///     The Log (Common.Logging)
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		[Option('m', "map-server-url", Required = true,
			HelpText =
				"ArcGIS Map Server URL (e.g. http://sampleserver1.arcgisonline.com/ArcGIS/rest/services/Petroleum/KGS_OilGasFields_Kansas/MapServer")]
		public string MapServerUrl { get; set; }


		[Option('o', "output-name", Required = false,
			HelpText = "The base output file name (e.g. KGS_OilGasFields_Kansas)")]
		public string OutputFileNameBase { get; set; }

		[Option('d', Required = false,
			HelpText = "The output directory. (e.g. c:\\temp\\KGS_OilGasFields_Kansas")]
		public string OutputDirectory { get; set; }

		[Option('t', "timeout", Required = false, Default = 120,
			HelpText = "The timeout, in seconds, for ArcGIS Map Server Requests.")]
		public int TimeoutSeconds { get; set; }

		[Option('l', "layer-names", Required = false,
			HelpText = "An optional list of layer names. If none are specified, all layers will be downloaded.")]
		public string LayerNames { get; set; }

		[Option('v', Required = false, HelpText = "Verbose output")]
		public bool Verbose { get; set; }

		[Option("max-chunks", Required = false,
			HelpText = "The maximum number of chunks to retrieve.  Useful for testing.")]
		public int MaximumNumberChunks { get; set; }

		[Option('s', "chunk-size", Required = false, Default = 50,
			HelpText = "The number of ObjectIDs for each chunk.")]
		public int ChunkSize { get; set; }

		[Option('w', "wait", Required = false, Default = 250,
			HelpText = "The number of milliseconds to wait between each request.")]
		public int WaitMilliseconds { get; set; }
	}
}