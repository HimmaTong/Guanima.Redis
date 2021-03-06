using System;
using System.Text;
using Guanima.Redis.Utils;

namespace Guanima.Redis.KeyTransformers
{
	/// <summary>
	/// A key transformer which converts the item keys into their Tiger hash.
	/// </summary>
	public class TigerHashKeyTransformer : KeyTransformerBase
	{
		public override string Transform(string key)
		{
			var th = new TigerHash();
			byte[] data = th.ComputeHash(Encoding.Unicode.GetBytes(key));

			return Convert.ToBase64String(data, Base64FormattingOptions.None);
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