using System;
using System.Collections.Generic;
using System.Net;

namespace Guanima.Redis
{
	/// <summary>
	/// Represents the statistics of a Redis node.
	/// </summary>
	public sealed class ServerStats
	{
		private const int OpAllowsSum = 1;
		private static log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServerStats));

		/// <summary>
		/// Defines a value which indicates that the statstics should be retrieved for all servers in the pool.
		/// </summary>
		public static readonly IPEndPoint All = new IPEndPoint(IPAddress.Any, 0);
		#region [ readonly int[] Optable       ]
		// defines which values can be summed and which not
		private static readonly int[] Optable = 
		{
			0, 0, 0, 1, 1, 1, 1, 1,
			1, 1, 1, 1, 1, 1, 1, 1
		};
		#endregion
		#region [ readonly string[] StatKeys   ]
		private static readonly string[] StatKeys = 
		{
			"uptime",
			"time",
			"version",
			"curr_items",
			"total_items",
			"curr_connections",
			"total_connections",
            "total_commands_processed",
			"connection_structures",
			"cmd_get",
			"cmd_set",
			"get_hits",
			"get_misses",
			"bytes",
			"bytes_read",
			"bytes_written",
			"limit_maxbytes",
		};
		#endregion

		private Dictionary<IPEndPoint, Dictionary<string, string>> results;

		internal ServerStats(Dictionary<IPEndPoint, Dictionary<string, string>> results)
		{
			this.results = results;
		}

		/// <summary>
		/// Gets a stat value for the specified server.
		/// </summary>
		/// <param name="server">The adress of the server. If <see cref="IPAddress.Any"/> is specified it will return the sum of all server stat values.</param>
		/// <param name="item">The stat to be returned</param>
		/// <returns>The value of the specified stat item</returns>
		public long GetValue(IPEndPoint server, StatItem item)
		{
			// asked for a specific server
			if (server.Address != IPAddress.Any)
			{
				// error check
				string tmp = GetRaw(server, item);
				if (String.IsNullOrEmpty(tmp))
					throw new ArgumentException("Item was not found: " + item);

				long value;
				// return the value
				if (Int64.TryParse(tmp, out value))
					return value;

				throw new ArgumentException("Invalid value string was returned: " + tmp);
			}

			// check if we can sum the value for all servers
			if ((Optable[(int)item] & OpAllowsSum) != OpAllowsSum)
				throw new ArgumentException("The " + item + " values cannot be summarized");

			long retval = 0;

			// sum & return
			foreach (IPEndPoint ep in results.Keys)
			{
				retval += GetValue(ep, item);
			}

			return retval;
		}

		/// <summary>
		/// Returns the server of Redis running on the specified server.
		/// </summary>
		/// <param name="server">The adress of the server</param>
		/// <returns>The version of Redis</returns>
		public Version GetVersion(IPEndPoint server)
		{
			string version = GetRaw(server, StatItem.Version);
			if (String.IsNullOrEmpty(version))
				throw new ArgumentException("No version found for the server " + server);

			return new Version(version);
		}

		/// <summary>
		/// Returns the uptime of the specific server.
		/// </summary>
		/// <param name="server">The adress of the server</param>
		/// <returns>A value indicating how long the server is running</returns>
		public TimeSpan GetUptime(IPEndPoint server)
		{
			string uptime = GetRaw(server, StatItem.Uptime);
			if (String.IsNullOrEmpty(uptime))
				throw new ArgumentException("No uptime found for the server " + server);

			long value;
			if (!Int64.TryParse(uptime, out value))
				throw new ArgumentException("Invalid uptime string was returned: " + uptime);

			return TimeSpan.FromSeconds(value);
		}

		/// <summary>
		/// Returns the stat value for a specific server. The value is not converted but returned as the server returned it.
		/// </summary>
		/// <param name="server">The adress of the server</param>
		/// <param name="key">The name of the stat value</param>
		/// <returns>The value of the stat item</returns>
		public string GetRaw(IPEndPoint server, string key)
		{
			Dictionary<string, string> serverValues;
			string retval;

			if (results.TryGetValue(server, out serverValues))
			{
				if (serverValues.TryGetValue(key, out retval))
					return retval;

				if (log.IsDebugEnabled)
					log.DebugFormat("The stat item {0} does not exist for {1}", key, server);
			}
			else
			{
				if (log.IsDebugEnabled)
					log.DebugFormat("No stats are stored for {0}", server);
			}

			return null;
		}

		/// <summary>
		/// Returns the stat value for a specific server. The value is not converted but returned as the server returned it.
		/// </summary>
		/// <param name="server">The adress of the server</param>
		/// <param name="item">The stat value to be returned</param>
		/// <returns>The value of the stat item</returns>
		public string GetRaw(IPEndPoint server, StatItem item)
		{
			if ((int)item < StatKeys.Length && (int)item >= 0)
				return GetRaw(server, StatKeys[(int)item]);

			throw new ArgumentOutOfRangeException("item");
		}
	}
}

#region [ License information          ]
/* ************************************************************
 *
 * Copyright (c) Attila Kisk�, enyim.com
 *
 * This source code is subject to terms and conditions of 
 * Microsoft Permissive License (Ms-PL).
 * 
 * A copy of the license can be found in the License.html
 * file at the root of this distribution. If you can not 
 * locate the License, please send an email to a@enyim.com
 * 
 * By using this source code in any fashion, you are 
 * agreeing to be bound by the terms of the Microsoft 
 * Permissive License.
 *
 * You must not remove this notice, or any other, from this
 * software.
 *
 * ************************************************************/
#endregion