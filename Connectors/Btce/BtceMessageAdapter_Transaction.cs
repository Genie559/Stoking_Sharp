#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: StockSharp.Btce.Btce
File: BtceMessageAdapter_Transaction.cs
Created: 2015, 11, 11, 2:32 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License
namespace StockSharp.Btce
{
	using System;
	using System.Linq;
	using System.Collections.Generic;

	using Ecng.Collections;
	using Ecng.Common;

	using StockSharp.Messages;
	using StockSharp.Localization;

	using Order = StockSharp.Btce.Native.Order;
	using Trade = StockSharp.Btce.Native.Trade;

	partial class BtceMessageAdapter
	{
		private long _lastMyTradeId;
		private readonly Dictionary<long, RefPair<long, decimal>> _orderInfo = new();
		//private readonly Dictionary<string, List<ExecutionMessage>> _unkOrds = new Dictionary<string, List<ExecutionMessage>>(StringComparer.InvariantCultureIgnoreCase);

		private readonly Dictionary<SecurityId, decimal> _positions = new();

		private string PortfolioName => nameof(Btce) + "_" + Key.ToId();

		private void ProcessOrderRegister(OrderRegisterMessage regMsg)
		{
			switch (regMsg.OrderType)
			{
				case null:
				case OrderTypes.Limit:
				//case OrderTypes.Market:
					break;
				case OrderTypes.Conditional:
				{
					var condition = (BtceOrderCondition)regMsg.Condition;

					if (!condition.IsWithdraw)
						throw new NotSupportedException(LocalizedStrings.OrderUnsupportedType.Put(regMsg.OrderType, regMsg.TransactionId));

					var withdrawId = _httpClient.Withdraw(regMsg.SecurityId.SecurityCode, regMsg.Volume, condition.WithdrawInfo);

					SendOutMessage(new ExecutionMessage
					{
						DataTypeEx = DataType.Transactions,
						OrderId = withdrawId,
						ServerTime = CurrentTime.ConvertToUtc(),
						OriginalTransactionId = regMsg.TransactionId,
						OrderState = OrderStates.Done,
						HasOrderInfo = true,
					});

					ProcessPortfolioLookup(null);
					return;
				}
				default:
					throw new NotSupportedException(LocalizedStrings.OrderUnsupportedType.Put(regMsg.OrderType, regMsg.TransactionId));
			}

			var currency = regMsg.SecurityId.ToCurrency();
			var reply = _httpClient.MakeOrder(
				currency,
				regMsg.Side.ToBtce(),
				regMsg.Price,
				regMsg.Volume
			);

			var isDone = reply.Command.OrderId == 0;
			var msg = new ExecutionMessage
			{
				OriginalTransactionId = regMsg.TransactionId,
				DataTypeEx = DataType.Transactions,
				OrderId = isDone ? null : reply.Command.OrderId,
				Balance = isDone ? 0m : (decimal)reply.Command.Remains,
				OrderState = isDone ? OrderStates.Done : OrderStates.Active,
				HasOrderInfo = true,

				OrderPrice = regMsg.Price,
				OrderVolume = regMsg.Volume,
				Side = regMsg.Side,
				SecurityId = regMsg.SecurityId,
				OrderType = OrderTypes.Limit
			};

			if (isDone)
			{
				//_unkOrds.SafeAdd(currency).Add(msg);

				var trades = GetTrades();

				foreach (var trade in trades)
				{
					if (!_orderInfo.ContainsKey(trade.OrderId) && msg != null)
					{
						msg.OrderId = trade.OrderId;

						_orderInfo.Add(trade.OrderId, RefTuple.Create(regMsg.TransactionId, regMsg.Volume));
						
						// send only 1 time
						SendOutMessage(msg);
						msg = null;
					}

					ProcessTrade(trade);
				}
			}
			else
			{
				_orderInfo.Add(reply.Command.OrderId, RefTuple.Create(regMsg.TransactionId, regMsg.Volume));
				SendOutMessage(msg);
			}

			ProcessFunds(reply.Command.Funds);
		}

		private void ProcessOrderCancel(OrderCancelMessage cancelMsg)
		{
			if (cancelMsg.OrderId == null)
				throw new InvalidOperationException(LocalizedStrings.OrderNoExchangeId.Put(cancelMsg.OriginalTransactionId));

			var reply = _httpClient.CancelOrder(cancelMsg.OrderId.Value);

			//SendOutMessage(new ExecutionMessage
			//{
			//	OriginalTransactionId = cancelMsg.TransactionId,
			//	OrderId = cancelMsg.OrderId,
			//	OrderState = OrderStates.Done,
			//	DataTypeEx = DataType.Transactions,
			//	HasOrderInfo = true,
			//	ServerTime = CurrentTime.ConvertToUtc(),
			//});

			ProcessOrderStatus(null);
			ProcessFunds(reply.Command.Funds);
		}

		private void ProcessOrder(Order order, decimal balance, long transId, long origTransId)
		{
			SendOutMessage(new ExecutionMessage
			{
				DataTypeEx = DataType.Transactions,
				OrderId = order.Id,
				TransactionId = transId,
				OriginalTransactionId = origTransId,
				OrderPrice = (decimal)order.Price,
				Balance = balance,
				OrderVolume = (decimal)order.Volume,
				Side = order.Side.ToSide(),
				SecurityId = order.Instrument.ToStockSharp(),
				ServerTime = transId != 0 ? order.Timestamp.ApplyUtc() : CurrentTime.ConvertToUtc(),
				PortfolioName = PortfolioName,
				//OrderState = order.Status.ToOrderState(),
				OrderState = OrderStates.Active,
				HasOrderInfo = true,
				OrderType = OrderTypes.Limit
			});
		}

		//private const decimal _epsilon = 0.000000001m;

		private void ProcessTrade(Trade trade)
		{
			var serverTime = trade.Timestamp.ApplyUtc();
			var secId = trade.Instrument.ToStockSharp();
			var side = trade.Side.ToSide();

			var info = _orderInfo.TryGetValue(trade.OrderId);
			if (info == null)
			{
				return;

				//if (!_unkOrds.TryGetValue(trade.Instrument, out var que) || que.Count == 0)
				//	return;

				//// ��������� ����������� ����� � ����
				//var msg = que.FindLast(o => o.SecurityId == secId);
				//if (msg == null)
				//	return;

				//que.Remove(msg);
				//msg.OrderId = trade.OrderId;
				//msg.ServerTime = serverTime;
				//msg.PortfolioName = PortfolioName;
				//msg.Balance = msg.OrderVolume - (decimal)trade.Volume;

				//var waitingMoreTrades = msg.Balance > _epsilon;
				//if (waitingMoreTrades)
				//	msg.OrderState = OrderStates.Active;

				//SendOutMessage(msg);

				//if (waitingMoreTrades)
				//{
				//	info = new RefPair<long, decimal>
				//	{
				//		First = msg.OriginalTransactionId,
				//		Second = msg.OrderVolume.Value
				//	};

				//	_orderInfo.Add(trade.OrderId, info);
				//}
			}

			SendOutMessage(new ExecutionMessage
			{
				DataTypeEx = DataType.Transactions,
				OrderId = trade.OrderId,
				OriginalTransactionId = info.First,
				TradeId = trade.Id,
				TradePrice = (decimal)trade.Price,
				TradeVolume = (decimal)trade.Volume,
				Side = side,
				SecurityId = secId,
				ServerTime = serverTime,
				PortfolioName = PortfolioName,
				OriginSide = trade.IsMyOrder ? side.Invert() : side,
			});

			//if (info == null || info.Second <= 0)
			//	return;

			info.Second -= (decimal)trade.Volume;

			if (info.Second < 0)
				throw new InvalidOperationException(LocalizedStrings.OrderBalanceNotEnough.Put(trade.OrderId, info.Second));

			SendOutMessage(new ExecutionMessage
			{
				DataTypeEx = DataType.Transactions,
				OrderId = trade.OrderId,
				OriginalTransactionId = info.First,
				Balance = info.Second,
				OrderState = info.Second > 0 ? OrderStates.Active : OrderStates.Done,
				HasOrderInfo = true,
				ServerTime = serverTime,
			});

			if (info.Second == 0)
				_orderInfo.Remove(trade.OrderId);
		}

		private void ProcessFunds(IEnumerable<KeyValuePair<string, double>> funds, bool init = false)
		{
			foreach (var fund in funds)
			{
				//var currency = fund.Key;

				//if (!currency.StartsWithIgnoreCase("usd") &&
				//	!currency.StartsWithIgnoreCase("eur") &&
				//    !currency.StartsWithIgnoreCase("rur"))
				//{
				//	currency += "_usd";
				//}

				var pos = fund.Value.ToDecimal();

				if (pos == null)
					continue;

				var secId = new SecurityId
				{
					SecurityCode = fund.Key,
					BoardCode = BoardCodes.Btce,
				};

				if (init)
				{
					_positions[secId] = pos.Value;

					if (pos.Value == 0)
						continue;
				}
				else
				{
					if (_positions.ContainsKey(secId))
					{
						if (_positions[secId] == pos.Value)
							continue;
					}	
				}
				
				SendOutMessage(this
					.CreatePositionChangeMessage(PortfolioName, secId)
					.Add(PositionChangeTypes.CurrentValue, pos.Value));
			}
		}

		private void ProcessOrderStatus(OrderStatusMessage message)
		{
			if (message == null)
			{
				var portfolioRefresh = false;

				var orders = _httpClient.GetActiveOrders().Items.Values;

				var ids = _orderInfo.Keys.ToSet();
				
				foreach (var order in orders)
				{
					ids.Remove(order.Id);

					var info = _orderInfo.TryGetValue(order.Id);

					if (info == null)
					{
						info = RefTuple.Create(TransactionIdGenerator.GetNextId(), (decimal)order.Volume);

						_orderInfo.Add(order.Id, info);

						ProcessOrder(order, (decimal)order.Volume, info.First, 0);

						portfolioRefresh = true;
					}
					else
					{
						// balance existing orders tracked by trades
					}
				}

				var trades = GetTrades();

				foreach (var trade in trades)
				{
					ProcessTrade(trade);
				}

				foreach (var id in ids)
				{
					portfolioRefresh = true;

					// can be removed from ProcessTrade
					if (!_orderInfo.TryGetAndRemove(id, out var info))
						return;

					SendOutMessage(new ExecutionMessage
					{
						DataTypeEx = DataType.Transactions,
						HasOrderInfo = true,
						OrderId = id,
						OriginalTransactionId = info.First,
						ServerTime = CurrentTime.ConvertToUtc(),
						OrderState = OrderStates.Done,
					});
				}

				if (portfolioRefresh)
					ProcessPortfolioLookup(null);
			}
			else
			{
				if (!message.IsSubscribe)
					return;

				var orders = _httpClient.GetActiveOrders().Items.Values;

				foreach (var order in orders)
				{
					var info = RefTuple.Create(TransactionIdGenerator.GetNextId(), (decimal)order.Volume);

					_orderInfo.Add(order.Id, info);

					ProcessOrder(order, (decimal)order.Volume, info.First, message.TransactionId);
				}

				var trades = GetTrades();

				foreach (var trade in trades)
				{
					ProcessTrade(trade);
				}
			
				SendSubscriptionResult(message);
			}
		}

		private IEnumerable<Trade> GetTrades()
		{
			const int pageSize = 1000;
			const int maxIter = 10;

			var trades = new List<Trade>();

			var i = 0;

			while (true)
			{
				var batch = _httpClient.GetMyTrades(_lastMyTradeId + 1).Items.Values;

				var anyLess = false;
				trades.AddRange(batch.Where(t =>
				{
					if (t.Id > _lastMyTradeId)
						return true;

					anyLess = true;
					return false;
				}).ToArray());

				if (anyLess || batch.Count < pageSize)
					break;

				if (++i >= maxIter)
					break;
			}

			if (trades.Count > 0)
			{
				trades = trades.DistinctBy(t => t.Id).OrderBy(t => t.Id).ToList();
				_lastMyTradeId = trades.Last().Id;
			}

			return trades;
		}

		private void ProcessPortfolioLookup(PortfolioLookupMessage message)
		{
			if (message != null)
			{
				if (!message.IsSubscribe)
					return;
			}

			var transId = message?.TransactionId ?? 0;

			SendOutMessage(new PortfolioMessage
			{
				PortfolioName = PortfolioName,
				BoardCode = BoardCodes.Btce,
				OriginalTransactionId = transId
			});

			var reply = _httpClient.GetInfo();
			ProcessFunds(reply.State.Funds, true);

			if (!reply.State.Rights.CanTrade)
			{
				SendOutMessage(this
					.CreatePortfolioChangeMessage(PortfolioName)
					.Add(PositionChangeTypes.State, PortfolioStates.Blocked));
			}

			if (message != null)
				SendSubscriptionResult(message);

			_lastTimeBalanceCheck = CurrentTime;
		}
	}
}