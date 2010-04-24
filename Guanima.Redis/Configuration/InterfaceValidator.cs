using System;
using System.Configuration;

namespace Guanima.Redis.Configuration
{
	public class InterfaceValidator : ConfigurationValidatorBase
	{
		private readonly Type _interfaceType;

        public InterfaceValidator():base(){}

		public InterfaceValidator(Type type)
		{
			if (!type.IsInterface)
				throw new ArgumentException(type + " must be an interface");

			_interfaceType = type;
		}

		public override bool CanValidate(Type type)
		{
			return (type == typeof(Type)) || base.CanValidate(type);
		}

		public override void Validate(object value)
		{
			if (value != null)
				ConfigurationHelper.CheckForInterface((Type)value, _interfaceType);
		}
	}

	public sealed class InterfaceValidatorAttribute : ConfigurationValidatorAttribute
	{
		private readonly Type _interfaceType;

        public InterfaceValidatorAttribute():base(){}

		public InterfaceValidatorAttribute(Type type)
		{
			if (!type.IsInterface)
				throw new ArgumentException(type + " must be an interface");

			_interfaceType = type;
		}

		public override ConfigurationValidatorBase ValidatorInstance
		{
			get { return new InterfaceValidator(_interfaceType); }
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