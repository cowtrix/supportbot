using Common;
using Common.Config;
using SupportBot.Tickets;
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SupportBot.SupportProviders
{
	public enum ESupportProviderState
	{
		INITIAL,
		IDLE,
		WAITING_FOR_AGREEMENT,
		WAITING_FOR_TICKETS,
		WAITING_FOR_CLAIM,
		IN_PROGRESS,
	}
	public class SupportProvider : MessageHandler
	{
		public const string HELP_NOW_TOKEN = "CAN_ACCEPT_TICKETS";
		public const string HELP_STOP_TOKEN = "CANNOT_ACCEPT_TICKETS";
		public const string CLAIM_TOKEN = "CLAIM_TOKEN";
		private static ConfigVariable<string> m_supporterGDRPPath = new ConfigVariable<string>("SupporterGDPRAgreementPath", "supporterGDPRAgreement.txt");

		public string Name = "User";
		public int TelegramID;
		public ESupportProviderState State;

		public SupportProvider() { }

		public SupportProvider(User from)
		{
			Name = from.FirstName;
			TelegramID = from.Id;
			State = ESupportProviderState.INITIAL;
		}

		protected Ticket CurrentTicket => Data.Tickets.SingleOrDefault(t => t.State == ETicketState.IN_PROGRESS
			&& t.Owner.TelegramID == TelegramID);

		public override async Task HandleMessage(object sender, MessageEventArgs e)
		{
			var userID = e.Message.From.Id;
			if (State == ESupportProviderState.INITIAL)
			{
				// New ticket -> user must accept GDPR agreement
				var rkm = new InlineKeyboardMarkup(new InlineKeyboardButton() { Text = "I Accept", CallbackData = ACCEPT_TOKEN });
				await Bot.SendTextMessageAsync(userID, System.IO.File.ReadAllText(m_supporterGDRPPath.Value), replyMarkup: rkm, parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2);
				State = ESupportProviderState.WAITING_FOR_AGREEMENT;
				return;
			}
			else if (State == ESupportProviderState.WAITING_FOR_AGREEMENT)
			{
				// User needs to send callback, so text input not valid. Just reprompt.
				await Bot.SendTextMessageAsync(userID, Resources.RePromptForAgreement);
				return;
			}
			else if(State == ESupportProviderState.WAITING_FOR_TICKETS)
			{
				await Bot.SendTextMessageAsync(userID, Resources.SupportUserNoActiveTicket);
				await ICantHelpAnymore(userID);
				return;
			}
			else if (State == ESupportProviderState.IN_PROGRESS)
			{
				// find the ticket for this support provider
				var activeTicket = CurrentTicket;
				if (activeTicket == null)
				{
					// User doesn't have an active ticket
					await Bot.SendTextMessageAsync(userID, Resources.SupportUserNoActiveTicket);
					await ICantHelpAnymore(userID);
					State = ESupportProviderState.WAITING_FOR_TICKETS;
					return;
				}
				if (e.Message.Text == "/endchat")
				{
					await activeTicket.Close();
					return;
				}
				// Just forward the message onwards
				Logger.Debug($"Forwarding message from {e.Message.From.Id} to {activeTicket.Target}");
				activeTicket.Messages.Add(new Message(userID, e.Message.Text));
				await ForwardMessage(TelegramID, activeTicket.Target, Name, e);
				return;
			}
			else
			{
				await Bot.SendTextMessageAsync(userID, Resources.SupportUserNoActiveTicket);
				await ICantHelpAnymore(userID);
				return;
			}
		}

		public static async Task Think()
		{
			if (Data.OpenTickets.Any())
			{
				// Notify support users who might be waiting that they can now claim a ticket
				var waitingSupportProviders = Data.SupportProviders
					.Where(sp => sp.State == ESupportProviderState.WAITING_FOR_TICKETS);
				foreach (var waiter in waitingSupportProviders)
				{
					waiter.State = ESupportProviderState.WAITING_FOR_CLAIM;
					var rkm = new InlineKeyboardMarkup(new InlineKeyboardButton() { Text = "Claim Ticket", CallbackData = CLAIM_TOKEN });
					await Bot.SendTextMessageAsync(waiter.TelegramID, Resources.SupportUserTicketCanBeClaimed, replyMarkup:rkm);
				}
			}
			else
			{
				var claimingSupportProviders = Data.SupportProviders
					.Where(sp => sp.State == ESupportProviderState.WAITING_FOR_CLAIM);
				foreach (var waiter in claimingSupportProviders)
				{
					waiter.State = ESupportProviderState.WAITING_FOR_TICKETS;
					await Bot.SendTextMessageAsync(waiter.TelegramID, Resources.SupportUserNoTicketsAvailable);
					await ICantHelpAnymore(waiter.TelegramID);
				}
			}
		}

		public override async Task HandleCallback(object sender, CallbackQueryEventArgs e)
		{
			if (State == ESupportProviderState.WAITING_FOR_AGREEMENT)
			{
				// We want the user to accept the agreement
				if (e.CallbackQuery.Data == ACCEPT_TOKEN)
				{
					await Bot.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
					State = ESupportProviderState.IDLE;
				}
				else
				{
					Logger.Error("Something went wrong...");
					State = ESupportProviderState.INITIAL;
					return;
				}
			}
			if (State == ESupportProviderState.IDLE)
			{
				if (e.CallbackQuery.Data == HELP_NOW_TOKEN)
				{
					// User has said that they're active and waiting for tickets
					State = ESupportProviderState.WAITING_FOR_TICKETS;
					var openTicket = Data.OpenTickets.FirstOrDefault();
					if (openTicket != null)
					{
						await openTicket.Claim(this);
						// Assign them a ticket if there are any
						State = ESupportProviderState.IN_PROGRESS;
						return;
					}
					await ICantHelpAnymore(TelegramID);
					return;
				}
				await ICanHelpNow(TelegramID);
				return;
			}
			else if(State == ESupportProviderState.WAITING_FOR_TICKETS)
			{
				if (e.CallbackQuery.Data == HELP_STOP_TOKEN)
				{
					// User idle and doesn't want tickets anymore
					State = ESupportProviderState.IDLE;
					await ICanHelpNow(TelegramID);
					return;
				}
			}
			else if (State == ESupportProviderState.WAITING_FOR_CLAIM)
			{
				if (e.CallbackQuery.Data == CLAIM_TOKEN)
				{
					var nextTicket = Data.OpenTickets.FirstOrDefault();
					if (nextTicket == null)
					{
						await Bot.SendTextMessageAsync(TelegramID, Resources.SupportUserBeatenInClaim);
						await ICantHelpAnymore(TelegramID);
						State = ESupportProviderState.WAITING_FOR_TICKETS;
						return;
					}
					await nextTicket.Claim(this);
				}
			}
		}

		private static async Task ICanHelpNow(int id)
		{
			var rkm = new InlineKeyboardMarkup(new InlineKeyboardButton() { Text = "I Can Help Now", CallbackData = HELP_NOW_TOKEN });
			await Bot.SendTextMessageAsync(id, Resources.SupportUserHelp, replyMarkup: rkm);
		}

		public static async Task ICantHelpAnymore(int id)
		{
			// Let them wait
			var rkm2 = new InlineKeyboardMarkup(new InlineKeyboardButton() { Text = "I Want To Stop Helping For Now", CallbackData = HELP_STOP_TOKEN });
			await Bot.SendTextMessageAsync(id, Resources.SupportUserWaitingForTicket, replyMarkup: rkm2);
		}
	}
}
