#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: StockSharp.Btce.Btce
File: Extensions.cs
Created: 2015, 11, 11, 2:32 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License
namespace StockSharp.Btce
{
	using System;

	using StockSharp.Messages;
	using StockSharp.Localization;

	static class Extensions
	{
		public static string ToBtce(this Sides side)
		{
			switch (side)
			{
				case Sides.Buy:
					return "buy";
				case Sides.Sell:
					return "sell";
				default:
					throw new ArgumentOutOfRangeException(nameof(side), side, LocalizedStrings.InvalidValue);
			}
		}

		public static Sides ToSide(this string side)
		{
			switch (side)
			{
				case "sell":
				case "ask":
					return Sides.Sell;
				case "buy":
				case "bid":
					return Sides.Buy;
				default:
					throw new ArgumentOutOfRangeException(nameof(side), side, LocalizedStrings.InvalidValue);
			}
		}

		public static OrderStates ToOrderState(this int status)
		{
			switch (status)
			{
				case 0:
					return OrderStates.Active;
				case 1: // executed
				case 2: // canceled
				case 3: // canceled but partially executed
					return OrderStates.Done;
				default:
					throw new ArgumentOutOfRangeException(nameof(status), status, LocalizedStrings.InvalidValue);
			}
		}

		//public static DateTime ToTime(this long timeStamp)
		//{
		//	return Converter.GregorianStart + TimeSpan.FromMilliseconds(timeStamp);
		//}

		public static string ToCurrency(this SecurityId securityId)
		{
			return securityId.SecurityCode.Replace('/', '_').ToLowerInvariant();
		}

		public static SecurityId ToStockSharp(this string currency)
		{
			return new SecurityId
			{
				SecurityCode = currency.Replace('_', '/').ToUpperInvariant(),
				BoardCode = BoardCodes.Btce,
			};
		}

		//public static string ToStockSharpCode(this string btceCode)
		//{
		//	return btceCode.Replace('_', '/').ToUpperInvariant();
		//}

		//public static string ToBtceCode(this string stockSharpCode)
		//{
		//	return stockSharpCode.Replace('/', '_').ToLowerInvariant();
		//}
	}
}