using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using ArmCop.Exceptions;
using CommandLine;
using Common.Logging;
using GeoJSON.Net.Feature;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServiceStack;

namespace ArmCop
{
	internal class Program
	{
		/// <summary>
		///     The Log (Common.Logging)
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static List<int> _failedObjectIds = new List<int>();

		private static List<int> _nullGeometryIds = new List<int>();

		private static List<int> _emptyGeometryIds = new List<int>();

		private static List<int> _successIds = new List<int>();

		private static Dictionary<string, string> _chunkFileNames = new Dictionary<string, string>();

		private static int ChunkSize { get; set; } = 50;

		public static int ChunkCounter { get; set; } = 1;

		public static int TotalChunkCount { get; set; }

		private static int Timeout { get; set; } = 300000;

		private static void Main(string[] args)
		{
			var versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
			var fileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);

			var version = versionInfo.ProductVersion;
			string assemblyDescription = null;

			var descriptionAttribute = Assembly.GetExecutingAssembly()
				.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false)
				.OfType<AssemblyDescriptionAttribute>()
				.FirstOrDefault();

			if (descriptionAttribute != null)
				assemblyDescription = descriptionAttribute.Description;

			var executableInfoMessage =
				$"{assemblyDescription}{Environment.NewLine}{Path.GetFileName(Assembly.GetExecutingAssembly().Location)} v.{version} (last modified on {fileInfo.LastWriteTime})";
			Console.WriteLine(executableInfoMessage);
			Log.Info(executableInfoMessage);

			Parser.Default.ParseArguments<CommandLineOptions>(args)
				.WithParsed(RunOptionsAndReturnExitCode)
				.WithNotParsed(HandleParseError);

			Console.WriteLine("Copy Complete!");
		}

		private static void HandleParseError(IEnumerable<Error> errs)
		{
			Log.Debug("Enter");

			/*
			foreach (var err in errs)
			{
				string message = null;

				switch (err.Tag)
				{
					case ErrorType.BadFormatTokenError:
						break;

					case ErrorType.MissingValueOptionError:
						break;

					case ErrorType.UnknownOptionError:
						break;

					case ErrorType.MissingRequiredOptionError:
						message = $"Missing Required Parameter: {((MissingRequiredOptionError)err).NameInfo.NameText}";

						break;

					case ErrorType.MutuallyExclusiveSetError:
						break;

					case ErrorType.BadFormatConversionError:
						break;

					case ErrorType.SequenceOutOfRangeError:
						break;

					case ErrorType.RepeatedOptionError:
						break;

					case ErrorType.NoVerbSelectedError:
						break;

					case ErrorType.BadVerbSelectedError:
						break;

					case ErrorType.HelpRequestedError:
						break;

					case ErrorType.HelpVerbRequestedError:
						break;

					case ErrorType.VersionRequestedError:
						break;

					default:
						Console.WriteLine($"Error: {err.Tag}");
						break;
				}

				if (message != null && !message.IsNullOrEmpty())
				{
				}
				;
			}		*/
		}

		private static void RunOptionsAndReturnExitCode(CommandLineOptions opts)
		{
			Log.Debug("Enter");

			CopyMapServiceData(opts);
		}

		public static void CopyMapServiceData(CommandLineOptions opts)
		{
			Timeout = opts.TimeoutSeconds * 1000;

			if (opts.ChunkSize > 0)
				ChunkSize = opts.ChunkSize;

			var layerNames = new List<string>();
			var url = opts.MapServerUrl;

			var outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			if (!string.IsNullOrEmpty(opts.OutputDirectory))
				outputDirectory = opts.OutputDirectory;

			Helper.CreateDirectoryIfNotExists(outputDirectory);

			if (!string.IsNullOrEmpty(opts.LayerNames)) layerNames.AddRange(opts.LayerNames.Split(','));

			var outputFileNameBase = string.Empty;

			if (!string.IsNullOrEmpty(opts.OutputFileNameBase)) outputFileNameBase = opts.OutputFileNameBase;

			if (string.IsNullOrEmpty(outputFileNameBase))
				outputFileNameBase = url.Split('/').Reverse().Skip(1).Take(1).FirstOrDefault();

			Console.WriteLine("Obtaining Map Server Info...");
			var mapServerInfo = GetMapServerInfo(url);

			var maxRecordCount = mapServerInfo.Value<int?>("maxRecordCount");

			if (maxRecordCount.HasValue && maxRecordCount.Value > 0) ChunkSize = maxRecordCount.Value;

			// ReSharper disable once AssignNullToNotNullAttribute
			var mapServerInfoFilePath = Path.Combine(outputDirectory, $"{outputFileNameBase}-MapServerInfo.json");
			File.WriteAllText(mapServerInfoFilePath, JsonConvert.SerializeObject(mapServerInfo, Formatting.Indented));
			Console.WriteLine($"Map Server Info written to: {mapServerInfoFilePath}");

			var layers = (JArray) mapServerInfo["layers"];

			foreach (var layer in layers)
			{
				_chunkFileNames = new Dictionary<string, string>(); // Reset the chunk file names directory
				_failedObjectIds = new List<int>();
				_nullGeometryIds = new List<int>();
				_emptyGeometryIds = new List<int>();
				_successIds = new List<int>();
				TotalChunkCount = 0;
				ChunkCounter = 1;

				var layerId = layer.Value<int>("id");
				var layerName = layer.Value<string>("name");

				var layerNameDirectory = layerName.RemoveSpecialCharacters();

				if (layerNames.Count > 0 && layerNames.Contains(layerName).Equals(false)) continue;

				if (!(layer["subLayerIds"] is JArray))
				{
					// ReSharper disable once UnusedVariable
					var layerUrl = $"{url}/{layerId}";

					var layerQueryUrl = $"{url}/{layerId}/query";

					var layerDirectoryPath = Path.Combine(outputDirectory, layerNameDirectory);

					Helper.CreateDirectoryIfNotExists(layerDirectoryPath);

					var totalFeatureCount = GetObjectIdCount(layerQueryUrl);

					Console.WriteLine($"Total Feature Count: {totalFeatureCount}");

					Console.WriteLine("Getting Object IDs...");
					var oidJsonObject = GetObjectIds(layerQueryUrl);
					var oids = ((JArray) oidJsonObject["objectIds"]).ToObject<List<int>>();
					Console.WriteLine($"Received {oids.Count} Object IDs!");

					var oidsFilePath = Path.Combine(layerDirectoryPath, $"{layerNameDirectory}-ObjectIds.json");
					File.WriteAllText(oidsFilePath, JsonConvert.SerializeObject(oidJsonObject, Formatting.Indented));
					Console.WriteLine($"Object IDs written to: {mapServerInfoFilePath}");

					var chunks = oids.ChunkBy(ChunkSize);
					TotalChunkCount = chunks.Count;
					Console.WriteLine($"Retrieving features, {ChunkSize} chunks at a time...");

					if (!Directory.Exists(layerDirectoryPath))
						Directory.CreateDirectory(layerDirectoryPath);

					foreach (var chunk in chunks)
					{
						if (ChunkCounter == 9)
							Log.Debug("9");

						DownloadChunk(chunk, layerQueryUrl, layerNameDirectory, layerDirectoryPath,
							opts.WaitMilliseconds);

						if (opts.MaximumNumberChunks > 0 && opts.MaximumNumberChunks == ChunkCounter)
						{
							Console.WriteLine(
								$"Stopping on chunk #{ChunkCounter}. Max Chunk (--max-chunks) = {opts.MaximumNumberChunks}.");
							break;
						}
					}

					var mergedGeoJsonFeatureCollection = MergeGeoJsonFeatureCollections(_chunkFileNames.Keys.ToList());

					var checkCount = _failedObjectIds.Count + mergedGeoJsonFeatureCollection.Features.Count +
					                 _nullGeometryIds.Count + _emptyGeometryIds.Count;

					if (checkCount != totalFeatureCount)
						throw new ApplicationException(
							$"The check count ({checkCount}) does not match the total feature count({totalFeatureCount}).");

					var mergedGeoJsonFeatureCollectionFileName = Path.Combine(layerDirectoryPath,
						$"{layerNameDirectory}.geojson");

					var failedObjectIdsFileName = Path.Combine(layerDirectoryPath,
						$"{layerNameDirectory}-failed-object-ids.geojson");

					File.WriteAllText(mergedGeoJsonFeatureCollectionFileName,
						JsonConvert.SerializeObject(mergedGeoJsonFeatureCollection, Formatting.Indented));

					File.WriteAllText(failedObjectIdsFileName,
						JsonConvert.SerializeObject(_failedObjectIds, Formatting.Indented));
					
				}
				else
				{
					// Group layer
					Console.WriteLine($"Skipping {layerName} ({layerId}) because it is a group layer.");
				}
			}
		}

		private static void DownloadChunk(List<int> chunk, string layerQueryUrl, string layerNameDirectory,
			string layerDirectoryPath, int waitMilliseconds)
		{
			Console.WriteLine($"Retrieving chunk {ChunkCounter} of {TotalChunkCount}...");

			JObject geoJsonFeatureCollection = null;

			try
			{
				geoJsonFeatureCollection = GetGeoJson(layerQueryUrl, chunk);
			}
			catch (FailedToExecuteQueryException failedToExecuteQueryException)
			{
				Log.Error(failedToExecuteQueryException);

				if (chunk.Count > 1)
				{
					var split = chunk.SplitInHalf();

					foreach (var splitChunk in split)
						if (splitChunk.Count > 0)
						{
							TotalChunkCount++;

							try
							{
								DownloadChunk(splitChunk, layerQueryUrl, layerNameDirectory, layerDirectoryPath,
									waitMilliseconds);
							}
							catch (Exception e)
							{
								Console.WriteLine(e);
								_failedObjectIds.AddRange(_failedObjectIds);
							}
						}
				}
				else if (chunk.Count == 1)
				{
					_failedObjectIds.Add(chunk.First());
				}
			}
			catch (Exception e)
			{
				Log.Error(e);

				throw;
			}

			if (geoJsonFeatureCollection != null)
			{
				var cleanedGeoJsonFeatureCollection = CleanGeoJsonFeatureCollection(geoJsonFeatureCollection);

				var features = cleanedGeoJsonFeatureCollection["features"];

				foreach (var feature in features)
				{
					var id = feature.Value<int>("id");

					if (_successIds.Contains(id))
						throw new ApplicationException();

					_successIds.Add(id);
				}

				Console.WriteLine($"Retrieval of chunk {ChunkCounter} of {TotalChunkCount} complete!");

				var chunkFilename = Path.Combine(layerDirectoryPath,
					$"{layerNameDirectory}-chunk-{ChunkCounter}.json");

				File.WriteAllText(chunkFilename,
					JsonConvert.SerializeObject(cleanedGeoJsonFeatureCollection, Formatting.Indented));

				Console.WriteLine($"Saved chunk {ChunkCounter} to {chunkFilename}");
				_chunkFileNames.Add(chunkFilename, chunkFilename);
				ChunkCounter++;

				if (waitMilliseconds <= 0) return;

				Console.Write($"Waiting for {waitMilliseconds}ms...");
				Thread.Sleep(waitMilliseconds);
				Console.WriteLine("proceeding.");
			}
		}

		/// <summary>
		///     Determines whether [is failed to execute query response] [the specified json].
		/// </summary>
		/// <param name="json">The json.</param>
		/// <returns>
		///     <c>true</c> if [is failed to execute query response] [the specified json]; otherwise, <c>false</c>.
		/// </returns>
		/// <exception cref="ArmCop.ArcGISServerErrorException"></exception>
		private static bool IsFailedToExecuteQueryResponse(string json)
		{
			if (json.IndexOf("Failed", StringComparison.CurrentCultureIgnoreCase) != -1) return true;

			var jo = JObject.Parse(json);

			if (!jo["error"].IsNullOrEmpty())
			{
				Log.Error(jo);

				throw new ArcGISServerErrorException(jo);
			}

			return false;
		}

		/// <summary>
		///     Cleans up bad geometry that can come out of ArcGIS Map Services
		/// </summary>
		/// <param name="geoJsonFeatureCollection">The geo json feature collection.</param>
		/// <returns></returns>
		private static JObject CleanGeoJsonFeatureCollection(JObject geoJsonFeatureCollection)
		{
			if (geoJsonFeatureCollection == null)
				return null;

			var features = (JArray) geoJsonFeatureCollection["features"];

			var featuresNullGeometry = features.Where(x => x["geometry"].IsNullOrEmpty()).ToList();

			if (featuresNullGeometry.Count > 0)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(
					$"Excluding the following features because they have null geometry: {string.Join(",", featuresNullGeometry.Select(x => x.Value<string>("id")))}");
				Console.ResetColor();

				_nullGeometryIds.AddRange(featuresNullGeometry.Select(x => x.Value<int>("id")));
			}

			var newArray = new JArray();
			foreach (var f in features.Where(x => x["geometry"].IsNullOrEmpty().Equals(false)))
			{
				var id = f.Value<int>("id");

				var featureCoords = (JArray) f["geometry"]["coordinates"];

				var cleaned = CleanGeoJsonGeometryArray(featureCoords);

				f["geometry"]["coordinates"] = cleaned;

				// Some ArcGIS Map Server Output has null measures, which breaks geojson parsers.
				//Check for it and remove it here:
				if (featureCoords.Any())
				{
					newArray.Add(f);
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"Skipping feature: {id} because the geometry has no coordinates. :(");
					Console.ResetColor();

					_emptyGeometryIds.Add(id);
				}
			}

			geoJsonFeatureCollection["features"] = newArray;

			return geoJsonFeatureCollection;
		}

		/// <summary>
		///     Cleans the GeoJson geometry array.
		/// </summary>
		/// <param name="array">The array.</param>
		/// <returns></returns>
		/// <exception cref="NotSupportedException"></exception>
		public static JArray CleanGeoJsonGeometryArray(JArray array)
		{
			if (JArrayIsNumericArray(array)) return CleanCoordinateArray(array);

			var returnItem = new JArray();

			for (var i = 0; i < array.Count; i++)
			{
				var token = array[i];

				if (token is JArray)
				{
					var childArray = token as JArray;

					JArray updatedJArray;

					if (JArrayIsNumericArray(childArray))
						updatedJArray = CleanCoordinateArray(childArray);
					else
						updatedJArray = CleanGeoJsonGeometryArray(childArray);

					returnItem.Add(updatedJArray);
				}
				else
				{
					throw new NotSupportedException();
				}
			}

			return returnItem;
		}

		/// <summary>
		///     Cleans the coordinate array and returns a new clean JArray
		/// </summary>
		/// <param name="childArray">The child array.</param>
		/// <returns></returns>
		private static JArray CleanCoordinateArray(JArray childArray)
		{
			var newCoordinateArray = new JArray();

			for (var j = 0; j < childArray.Count; j++)
			{
				var value = childArray[j].Value<double?>();

				if (value.HasValue)
					newCoordinateArray.Add(value);
				else
					break; // We can't shift values down.  It is XYZM XYZ or XY. Never XYM
			}

			return newCoordinateArray;
		}

		public static bool JArrayIsNumericArray(JArray array)
		{
			try
			{
				var c = 0;
				var v = 0;
				foreach (var token in array)
				{
					c++;

					if (token is JValue)
					{
						var value = (token as JValue).Value<int?>();
						v++;
					}
					else
					{
						break;
					}
				}

				return c == v;
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				throw;
			}

			return false;
		}

		public static FeatureCollection MergeGeoJsonFeatureCollections(List<string> fileNames)
		{
			FeatureCollection mergedFeatureCollection = null;

			foreach (var fileName in fileNames)
			{
				Console.Write($"Reading file: {fileName}...");
				var gjo = JsonConvert.DeserializeObject<FeatureCollection>(File.ReadAllText(fileName));

				if (mergedFeatureCollection == null)
					mergedFeatureCollection = gjo;
				else
					mergedFeatureCollection.Features.AddRange(gjo.Features);
				Console.WriteLine("complete.");
			}

			return mergedFeatureCollection;
		}

		private static JObject GetMapServerInfo(string mapServerUrl)
		{
			var queryStringParams = "?f=pjson";

			var url = $"{mapServerUrl}{queryStringParams}";

			return JObject.Parse(
				url.GetJsonFromUrl()
			);
		}

		private static JObject GetGeoJson(string layerQueryUrl, List<int> oids)
		{
			var body =
				$"where=&text=&objectIds={string.Join(",", oids)}&time=&geometry=&geometryType=esriGeometryEnvelope&inSR=&spatialRel=esriSpatialRelIntersects&relationParam=&outFields=*&returnGeometry=true&returnTrueCurves=false&maxAllowableOffset=&geometryPrecision=&outSR=&returnIdsOnly=false&returnCountOnly=false&orderByFields=&groupByFieldsForStatistics=&outStatistics=&returnZ=false&returnM=false&gdbVersion=&returnDistinctValues=false&resultOffset=&resultRecordCount=&queryByDistance=&returnExtentsOnly=false&datumTransformation=&parameterValues=&rangeValues=&f=geojson";

			var url = $"{layerQueryUrl}";
			try
			{
				var data = url.PostStringToUrl(body, "application/x-www-form-urlencoded");

				if (IsFailedToExecuteQueryResponse(data))
				{
					data = url.PostStringToUrl(body, "application/x-www-form-urlencoded");

					if (IsFailedToExecuteQueryResponse(data)) throw FailedToExecuteQueryException.Parse(data);
				}

				return JObject.Parse(data);
			}
			catch (FailedToExecuteQueryException failedToExecuteQueryException)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"Error while retrieving: {url} for ObjectIds:{string.Join(",", oids)}");

				if (failedToExecuteQueryException.ServerResponse != null)
					Console.WriteLine(
						$"Server Response: {JsonConvert.SerializeObject(failedToExecuteQueryException.ServerResponse, Formatting.None)}");
				Console.ResetColor();

				throw;
			}
			catch (ArcGISServerErrorException arcGISServerErrorException)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"Error while retrieving: {url} for ObjectIds:{string.Join(",", oids)}");

				if (arcGISServerErrorException.ServerResponse != null)
					Console.WriteLine(
						$"Server Response: {JsonConvert.SerializeObject(arcGISServerErrorException.ServerResponse, Formatting.None)}");
				Console.ResetColor();

				return null;
			}
			catch (Exception e)
			{
				Log.Error(e);

				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"Error while retrieving: {url} for ObjectIds:{string.Join(",", oids)}");

				Console.ResetColor();

				throw;
			}
		}

		/// <summary>
		///     Gets the object ids.
		/// </summary>
		/// <param name="layerQueryUrl">The layer query URL.</param>
		/// <returns></returns>
		private static JObject GetObjectIds(string layerQueryUrl)
		{
			var queryStringParams =
				"?where=1%3D1&text=&objectIds=&time=&geometry=&geometryType=esriGeometryEnvelope&inSR=&spatialRel=esriSpatialRelIntersects&relationParam=&outFields=&returnGeometry=true&returnTrueCurves=false&maxAllowableOffset=&geometryPrecision=&outSR=&returnIdsOnly=true&returnCountOnly=false&orderByFields=&groupByFieldsForStatistics=&outStatistics=&returnZ=false&returnM=false&gdbVersion=&returnDistinctValues=false&resultOffset=&resultRecordCount=&queryByDistance=&returnExtentsOnly=false&datumTransformation=&parameterValues=&rangeValues=&f=pjson";

			var url = $"{layerQueryUrl}/{queryStringParams}";

			return JObject.Parse(
				// ReSharper disable once ArgumentsStyleAnonymousFunction
				url.GetJsonFromUrl(requestFilter: req => { req.Timeout = Timeout; })
			);
		}

		private static int GetObjectIdCount(string layerQueryUrl)
		{
			var queryStringParams =
				"?where=1%3D1&text=&objectIds=&time=&geometry=&geometryType=esriGeometryEnvelope&inSR=&spatialRel=esriSpatialRelIntersects&relationParam=&outFields=&returnGeometry=true&returnTrueCurves=false&maxAllowableOffset=&geometryPrecision=&outSR=&returnIdsOnly=true&returnCountOnly=true&orderByFields=&groupByFieldsForStatistics=&outStatistics=&returnZ=false&returnM=false&gdbVersion=&returnDistinctValues=false&resultOffset=&resultRecordCount=&queryByDistance=&returnExtentsOnly=false&datumTransformation=&parameterValues=&rangeValues=&f=pjson";

			var url = $"{layerQueryUrl}/{queryStringParams}";
			var result = JObject.Parse(
				// ReSharper disable once ArgumentsStyleAnonymousFunction
				url.GetJsonFromUrl(requestFilter: req => { req.Timeout = Timeout; })
			);

			return result.Value<int>("count");
		}
	}
}