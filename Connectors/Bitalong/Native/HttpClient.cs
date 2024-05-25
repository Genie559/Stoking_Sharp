namespace StockSharp.Bitalong.Native
{
	using System;
	using System.Collections.Generic;
	using System.Security;
	using System.Security.Cryptography;

	using Ecng.Common;
	using Ecng.Net;
	using Ecng.Serialization;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	using RestSharp;

	using StockSharp.Localization;
	using StockSharp.Logging;
	using StockSharp.Messages;
	using StockSharp.Bitalong.Native.Model;

	class HttpClient : BaseLogReceiver
	{
		private readonly SecureString _key;
		private readonly HashAlgorithm _hasher;

		private readonly string _baseUrl;

		public HttpClient(string domain, SecureString key, SecureString secret)
		{
			_baseUrl = $"https://www.{domain}/api/";
			_key = key;
			_hasher = secret.IsEmpty() ? null : new HMACSHA512(secret.UnSecure().UTF8());
		}

		protected override void DisposeManaged()
		{
			_hasher?.Dispose();
			base.DisposeManaged();
		}

		// to get readable name after obfuscation
		public override string Name => nameof(Bitalong) + "_" + nameof(HttpClient);

		public IDictionary<string, Symbol> GetSymbols()
		{
			dynamic response = MakeRequest<object>(CreateUrl("index/marketinfo"), CreateRequest(Method.Get));

			return ((JToken)response.pairs).DeserializeObject<IDictionary<string, Symbol>>();
		}

		public Ticker GetTicker(string symbol)
		{
			return MakeRequest<Ticker>(CreateUrl($"index/ticker/{symbol}"), CreateRequest(Method.Get));
		}

		public IDictionary<string, Ticker> GetTickers()
		{
			return MakeRequest<IDictionary<string, Ticker>>(CreateUrl("index/tickers"), CreateRequest(Method.Get));
		}

		public OrderBook GetOrderBook(string symbol)
		{
			return MakeRequest<OrderBook>(CreateUrl($"index/orderBook/{symbol}"), CreateRequest(Method.Get));
		}

		public IEnumerable<Trade> GetTradeHistory(string symbol)
		{
			return MakeRequest<IEnumerable<Trade>>(CreateUrl($"index/tradeHistory/{symbol}"), CreateRequest(Method.Get));
		}

		public Tuple<IDictionary<string, double>, IDictionary<string, double>> GetBalances()
		{
			dynamic response = MakeRequest<object>(CreateUrl("private/balances"), ApplySecret(CreateRequest(Method.Post), null));

			return Tuple.Create(((JToken)response.available).DeserializeObject<IDictionary<string, double>>(), ((JToken)response.locked).DeserializeObject<IDictionary<string, double>>());
		}

		public IEnumerable<Order> GetOrders()
		{
			dynamic response = MakeRequest<object>(CreateUrl("private/openOrders"), ApplySecret(CreateRequest(Method.Post), null));

			return ((JToken)response.orders).DeserializeObject<IEnumerable<Order>>();
		}

		public Order GetOrderInfo(string symbol, long orderId)
		{
			dynamic response = MakeRequest<object>(CreateUrl("private/getOrder"), ApplySecret(CreateRequest(Method.Post), new
			{
				currencyPair = symbol,
				orderNumber = orderId.To<string>(),
			}));

			return ((JToken)response.orders).DeserializeObject<Order>();
		}

		public long RegisterOrder(string symbol, string side, decimal price, decimal volume)
		{
			dynamic response = MakeRequest<object>(CreateUrl($"private/{side}"), ApplySecret(CreateRequest(Method.Post), new
			{
				currencyPair = symbol,
				rate = price.To<string>(),
				amount = volume.To<string>(),
			}));

			return (long)response.orderNumber;
		}

		public void CancelOrder(string symbol, long orderId)
		{
			MakeRequest<object>(CreateUrl("private/cancelOrder"), ApplySecret(CreateRequest(Method.Post), new
			{
				currencyPair = symbol,
				orderNumber = orderId.To<string>(),
			}));
		}

		public void CancelAllOrders(string symbol, Sides? side)
		{
			MakeRequest<object>(CreateUrl("private/cancelAllOrders"), ApplySecret(CreateRequest(Method.Post), new
			{
				currencyPair = symbol,
				type = side == null ? -1 : (side.Value == Sides.Buy ? 1 : 0)
			}));
		}

		public void Withdraw(string currency, decimal volume, WithdrawInfo info)
		{
			if (info == null)
				throw new ArgumentNullException(nameof(info));

			if (info.Type != WithdrawTypes.Crypto)
				throw new NotSupportedException(LocalizedStrings.WithdrawTypeNotSupported.Put(info.Type));

			MakeRequest<object>(CreateUrl("private/withdraw"), ApplySecret(CreateRequest(Method.Post), new
			{
				currency,
				address = info.CryptoAddress,
				amount = volume.To<string>(),
			}));
		}

		private Uri CreateUrl(string methodName)
		{
			if (methodName.IsEmpty())
				throw new ArgumentNullException(nameof(methodName));

			return $"{_baseUrl}/{methodName}".To<Uri>();
		}

		private static RestRequest CreateRequest(Method method)
		{
			return new RestRequest((string)null, method);
		}

		private static readonly JsonSerializerSettings _serializerSettings = JsonHelper.CreateJsonSerializerSettings();

		private RestRequest ApplySecret(RestRequest request, object body)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));

			var bodyStr = string.Empty;

			if (body != null)
			{
				bodyStr = JsonConvert.SerializeObject(body, _serializerSettings);
				request.AddBodyAsStr(bodyStr);
			}

			var signature = _hasher
			    .ComputeHash(bodyStr.UTF8())
			    .Digest()
			    .ToLowerInvariant();

			request
				.AddHeader("KEY", _key.UnSecure())
				.AddHeader("SIGN", signature);

			return request;
		}

		private T MakeRequest<T>(Uri url, RestRequest request)
		{
			dynamic obj = request.Invoke(url, this, this.AddVerboseLog);

			if (obj is JObject)
			{
				if ((bool?)obj.result == false)
					throw new InvalidOperationException((string)obj.message);

				if (obj.data != null)
					obj = obj.data;
			}

			return ((JToken)obj).DeserializeObject<T>();
		}
	}
}