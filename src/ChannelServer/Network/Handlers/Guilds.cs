﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.World;
using Aura.Channel.World.Entities;
using Aura.Mabi.Const;
using Aura.Mabi.Network;
using Aura.Shared.Network;
using Aura.Shared.Util;
using System.Linq;

namespace Aura.Channel.Network.Handlers
{
	public partial class ChannelServerHandlers : PacketHandlerManager<ChannelClient>
	{
		/// <summary>
		/// Sent when choosing to convert play points to guild points.
		/// </summary>
		/// <example>
		/// No parameters.
		/// </example>
		[PacketHandler(Op.GuildConvertPlayPoints)]
		public void GuildConvertPlayPoints(ChannelClient client, Packet packet)
		{
			var creature = client.GetCreatureSafe(packet.Id);

			if (creature.Guild == null)
			{
				Log.Warning("GuildConvertPlayPoints: User '{0}' is not in a guild.", client.Account.Id);
				Send.GuildConvertPlayPointsR(creature, false, 0);
				return;
			}

			Send.GuildConvertPlayPointsR(creature, true, creature.PlayPoints);
		}

		/// <summary>
		/// Sent when confirming the conversion of play points to guild points.
		/// </summary>
		/// <example>
		/// No parameters.
		/// </example>
		[PacketHandler(Op.GuildConvertPlayPointsConfirm)]
		public void GuildConvertPlayPointsConfirm(ChannelClient client, Packet packet)
		{
			var creature = client.GetCreatureSafe(packet.Id);

			if (creature.Guild == null)
			{
				Log.Warning("GuildConvertPlayPointsConfirm: User '{0}' is not in a guild.", client.Account.Id);
				Send.GuildConvertPlayPointsConfirmR(creature, false);
				return;
			}

			ChannelServer.Instance.GuildManager.ConvertPlayPoints(creature, creature.Guild);

			Send.GuildConvertPlayPointsConfirmR(creature, true);
		}

		/// <summary>
		/// Sent when clicking OK on the guild gold donation window.
		/// </summary>
		/// <example>
		/// 001 [........00000159] Int    : 345
		/// 002 [0000000000000000] Long   : 0
		/// </example>
		[PacketHandler(Op.GuildDonate)]
		public void GuildDonate(ChannelClient client, Packet packet)
		{
			var amount = packet.GetInt();
			var checkId = packet.GetLong();

			var creature = client.GetCreatureSafe(packet.Id);

			if (creature.Guild == null)
			{
				Log.Warning("ConvertGpConfirm: User '{0}' is not in a guild.", client.Account.Id);
				Send.GuildDonateR(creature, false);
				return;
			}

			// Check
			if (checkId != 0)
			{
				var check = creature.Inventory.GetItem(checkId);
				if (check == null)
				{
					Log.Warning("GuildDonate: User '{0}' tried to donate non-exitent check.", client.Account.Id);
					Send.GuildDonateR(creature, false);
					return;
				}
				else if (!check.HasTag("/check/"))
				{
					Log.Warning("GuildDonate: User '{0}' tried to donate invalid check ({1}).", client.Account.Id, check.Info.Id);
					Send.GuildDonateR(creature, false);
					return;
				}

				ChannelServer.Instance.GuildManager.DonateItem(creature, creature.Guild, check);
			}
			// Gold
			else
			{
				if (amount == 0)
				{
					Send.GuildDonateR(creature, false);
					return;
				}
				else if (creature.Inventory.Gold < amount)
				{
					Send.MsgBox(creature, Localization.Get("You don't have enough gold."));
					Send.GuildDonateR(creature, false);
					return;
				}

				ChannelServer.Instance.GuildManager.DonateGold(creature, creature.Guild, amount);
			}

			Send.GuildDonateR(creature, true);
		}

		/// <summary>
		/// Sent choosing to destroy the guild stone.
		/// </summary>
		/// <example>
		/// No parameters.
		/// </example>
		[PacketHandler(Op.GuildDestroyStone)]
		public void GuildDestroyStone(ChannelClient client, Packet packet)
		{
			var creature = client.GetCreatureSafe(packet.Id);

			if (creature.Guild == null)
			{
				Log.Warning("GuildDestroyStone: User '{0}' is not in a guild.", client.Account.Id);
				Send.GuildDonateR(creature, false);
				return;
			}
			else if (creature.GuildMember.Rank != GuildMemberRank.Leader)
			{
				Log.Warning("GuildDestroyStone: User '{0}' tried to destroy stone without being leader.", client.Account.Id);
				Send.GuildDonateR(creature, false);
				return;
			}

			ChannelServer.Instance.GuildManager.DestroyStone(creature.Guild);
		}

		/// <summary>
		/// Sent when applying at a guild.
		/// </summary>
		/// <example>
		/// 001 [0300000000500002] Long   : 216172782119026690
		/// 002 [................] String : aaaaaaaaaabbbbbbbbbbccccccccccddddddddddeeeeeeeeeeffffffffffgggggggggghhhhhhhhhhjjjjjjjjjjiiiiiiiiii
		/// </example>
		[PacketHandler(Op.GuildApply)]
		public void GuildApply(ChannelClient client, Packet packet)
		{
			var guildId = packet.GetLong();
			var application = packet.GetString();

			var creature = client.GetCreatureSafe(packet.Id);

			// Check creature's guild
			if (creature.Guild != null)
			{
				Log.Warning("GuildApply: User '{0}' is already in a guild.", client.Account.Id);
				Send.GuildDonateR(creature, false);
				return;
			}

			// Check guild
			var guild = ChannelServer.Instance.GuildManager.GetGuild(guildId);
			if (guild == null)
			{
				Send.MsgBox(creature, Localization.Get("Guild not found."));
				Send.GuildApplyR(creature, false);
				return;
			}

			// Check application
			if (application.Length > 100)
			{
				Log.Warning("GuildApply: User '{0}' sent an application that was >100 characters, truncated.", client.Account.Id);
				application = application.Substring(0, 100);
			}

			ChannelServer.Instance.GuildManager.Apply(creature, guild, application);

			Send.GuildApplyR(creature, true);
		}

		/// <summary>
		/// Sent choosing to change the look of the guild stone.
		/// </summary>
		/// <example>
		/// 001 [........00000001] Int    : 1
		/// </example>
		[PacketHandler(Op.GuildChangeStone)]
		public void GuildChangeStone(ChannelClient client, Packet packet)
		{
			var stoneType = (GuildStoneType)packet.GetInt();

			var creature = client.GetCreatureSafe(packet.Id);

			// Check guild
			var guild = creature.Guild;
			if (guild == null)
			{
				Log.Warning("GuildChangeStone: User '{0}' is not in a guild.", client.Account.Id);
				return;
			}

			// Check rank
			if (!creature.GuildMember.IsLeader)
			{
				Log.Warning("GuildChangeStone: User '{0}' is not the guild's leader.", client.Account.Id);
				return;
			}

			// Check guild level
			var level = GuildLevel.Beginner;
			switch (stoneType)
			{
				case GuildStoneType.Hope: level = GuildLevel.Advanced; break;
				case GuildStoneType.Courage: level = GuildLevel.Grand; break;
			}

			if (guild.Level < level)
			{
				Log.Warning("GuildChangeStone: User '{0}' tried to change stone without having the necessary guild level.", client.Account.Id);
				return;
			}

			// Check resources
			var points = 0;
			var gold = 0;
			switch (stoneType)
			{
				case GuildStoneType.Hope: points = 10000; gold = 100000; break;
				case GuildStoneType.Courage: points = 100000; gold = 1000000; break;
			}

			if (guild.Points < points || guild.Gold < gold)
			{
				Send.MsgBox(creature, Localization.Get("You need {0:n0} Guild Points and {1:n0} Gold to change the guild stone."), points, gold);
				return;
			}

			// Update
			guild.Points -= points;
			guild.Gold -= gold;

			ChannelServer.Instance.Database.UpdateGuildResources(guild);
			ChannelServer.Instance.GuildManager.ChangeStone(creature.Guild, stoneType);
		}
	}
}
