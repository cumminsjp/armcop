using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ArmCop
{
	public static class Helper
	{
		public static void CreateDirectoryIfNotExists(string path)
		{
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
		}

		public static bool IsNullOrEmpty(this JToken token)
		{
			return (token == null) ||
				   (token.Type == JTokenType.Array && !token.HasValues) ||
				   (token.Type == JTokenType.Object && !token.HasValues) ||
				   (token.Type == JTokenType.String && token.ToString() == String.Empty) ||
				   (token.Type == JTokenType.Null);
		}

		/// <summary>
		/// Splits a list in half, if even. If odd count, the first list will have the larger group.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		public static List<List<T>> SplitInHalf<T>(this List<T> source)
		{
			var group1Count = (int)Math.Round((source.Count / 2d), 0);

			var group2Count = source.Count - group1Count;

			var group1 = source.Take(group1Count).ToList();

			var group2 = source.Skip(group1Count).Take(group2Count).ToList();

			List<List<T>> items = new List<List<T>>();

			  items.Add(group1);

			if (group2.Count > 0)
				items.Add(group2);

			return items;
		}



		public static List<List<T>> ChunkBy<T>(this List<T> source, int chunkSize)
		{
			return source
				.Select((x, i) => new { Index = i, Value = x })
				.GroupBy(x => x.Index / chunkSize)
				.Select(x => x.Select(v => v.Value).ToList())
				.ToList();
		}

		/// <summary>
		/// Removes the special characters.
		/// </summary>
		/// <param name="str">The string.</param>
		/// <returns></returns>
		public static string RemoveSpecialCharacters(this string str)
		{
			StringBuilder sb = new StringBuilder(str.Length);
			foreach (char c in str)
			{
				if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '_')
				{
					sb.Append(c);
				}
			}
			return sb.ToString();
		}
	}
}
