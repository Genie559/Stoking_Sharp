﻿namespace StockSharp.FTX
{
	using System;
	using System.ComponentModel.DataAnnotations;
	using System.Security;

	using Ecng.ComponentModel;
	using Ecng.Common;
	using Ecng.Serialization;

	using StockSharp.Localization;
	using StockSharp.Messages;

	/// <summary>
	/// The message adapter for <see cref="FTX"/>.
	/// </summary>
	[MediaIcon("ftx_logo.svg")]
	[Doc("topics/ftx.html")]
	[Display(
		ResourceType = typeof(LocalizedStrings),
		Name = LocalizedStrings.FTXKey,
		Description = LocalizedStrings.CryptoConnectorKey,
		GroupName = LocalizedStrings.CryptocurrencyKey)]
	[MessageAdapterCategory(MessageAdapterCategories.Crypto | MessageAdapterCategories.RealTime |
		MessageAdapterCategories.Free | MessageAdapterCategories.Ticks | MessageAdapterCategories.MarketDepth |
		MessageAdapterCategories.Level1 | MessageAdapterCategories.Transactions)]
	public partial class FtxMessageAdapter : IKeySecretAdapter
	{
		/// <summary>
		/// Default value for <see cref="MessageAdapter.HeartbeatInterval"/>.
		/// </summary>
		public static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(1);

		/// <inheritdoc />
		[Display(
				ResourceType = typeof(LocalizedStrings),
				Name = LocalizedStrings.KeyKey,
				Description = LocalizedStrings.KeyKey + LocalizedStrings.Dot,
				GroupName = LocalizedStrings.ConnectionKey,
				Order = 0)]
		public SecureString Key { get; set; }

		/// <inheritdoc />
		[Display(
				ResourceType = typeof(LocalizedStrings),
				Name = LocalizedStrings.SecretKey,
				Description = LocalizedStrings.SecretDescKey,
				GroupName = LocalizedStrings.ConnectionKey,
				Order = 1)]
		public SecureString Secret { get; set; }
		/// <summary>
		/// SubAccount name from GUI
		/// </summary>
		[Display(
			        ResourceType = typeof(LocalizedStrings),
			        Name = LocalizedStrings.SubAccKey,
			        Description = LocalizedStrings.SubAccKey,
			        GroupName = LocalizedStrings.ConnectionKey,
			        Order = 2)]
		public string SubaccountName { get; set; }

		/// <inheritdoc />
		public override void Save(SettingsStorage storage)
		{
			base.Save(storage);

			storage.SetValue(nameof(Key), Key);
			storage.SetValue(nameof(Secret), Secret);
			storage.SetValue(nameof(SubaccountName), SubaccountName);
		}

		/// <inheritdoc />
		public override void Load(SettingsStorage storage)
		{
			base.Load(storage);

			Key = storage.GetValue<SecureString>(nameof(Key));
			Secret = storage.GetValue<SecureString>(nameof(Secret));
			SubaccountName = storage.GetValue<string>(nameof(SubaccountName));
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return base.ToString() + ": " + LocalizedStrings.Key + " = " + Key.ToId();
		}
	}
}