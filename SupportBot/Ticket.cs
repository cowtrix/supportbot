using Common;
using Common.Config;
using Newtonsoft.Json;
using SupportBot.SupportProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;

namespace SupportBot.Tickets
{
	public enum ETicketState
	{
		INITIAL,
		WAITING_FOR_AGREEMENT,
		WAIT_IN_QUEUE,
		IN_PROGRESS,
		FINISHED
	}

	public class Ticket : MessageHandler
	{
		private static ConfigVariable<string> m_clientGDRPPath = 
			new ConfigVariable<string>("ClientGDPRAgreementPath", "clientGDPRAgreement.txt");

		public DateTime DateCreated { get; set; }
		[JsonProperty(ItemIsReference = true)]
		public SupportProvider Owner { get; set; }
		public List<Message> Messages { get; set; }
		public int Target { get; set; }
		public ETicketState State { get; set; }
		public bool IsActive => State != ETicketState.FINISHED;

		public Ticket(int target)
		{
			Target = target;
			Messages = new List<Message>();
			DateCreated = DateTime.Now;
			State = ETicketState.INITIAL;
		}

		public override async Task HandleMessage(object sender, MessageEventArgs e)
		{
			var userID = e.Message.From.Id;
			Messages.Add(new Message(userID, e.Message.Text));
			if (State == ETicketState.INITIAL)
			{
				// New ticker -> user must accept GDPR agreement
				var rkm = new InlineKeyboardMarkup(new InlineKeyboardButton() { Text = "I Accept", CallbackData = ACCEPT_TOKEN });
				await Bot.SendTextMessageAsync(userID, File.ReadAllText(m_clientGDRPPath.Value), replyMarkup: rkm, parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2);
				State = ETicketState.WAITING_FOR_AGREEMENT;
				return;
			}
			else if(State == ETicketState.WAITING_FOR_AGREEMENT)
			{
				await Bot.SendTextMessageAsync(userID, Resources.RePromptForAgreement);
				return;
			}

			if (e.Message.Text == "/endchat")
			{
				await Close();
				return;
			}

			if (State == ETicketState.WAIT_IN_QUEUE)
			{
				await SendQueueUpdate();
				return;
			}
			else if (State == ETicketState.IN_PROGRESS)
			{
				Logger.Debug($"Forwarding message from {Target} to {Owner.TelegramID}");
				await Bot.SendTextMessageAsync(Owner.TelegramID, $"User: {e.Message.Text}");
				return;
			}
		}

		public override async Task HandleCallback(object sender, CallbackQueryEventArgs e)
		{
			var userID = e.CallbackQuery.From.Id;
			if (State == ETicketState.WAITING_FOR_AGREEMENT)
			{
				// We want the user to accept the agreement
				if(e.CallbackQuery.Data == ACCEPT_TOKEN)
				{
					await Bot.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
					await Bot.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId,
						e.CallbackQuery.Message.Text);
					await Bot.SendTextMessageAsync(userID, string.Format(Resources.UserAcceptsAgreement, e.CallbackQuery.From.FirstName));
					await SendQueueUpdate();
					State = ETicketState.WAIT_IN_QUEUE;
				}
				else
				{
					Logger.Error("Something went wrong...");
					State = ETicketState.INITIAL;
					return;
				}
			}
		}

		public async Task Close()
		{
			// close the ticket
			State = ETicketState.FINISHED;
			// TODO send client feedback scale?
			await Bot.SendTextMessageAsync(Target, Resources.UserTicketClosed);
			if(Owner != null)
			{
				await Bot.SendTextMessageAsync(Owner.TelegramID, Resources.SupportUserTicketClosed);
				await SupportProvider.ICantHelpAnymore(Owner.TelegramID);
				Owner.State = ESupportProviderState.WAITING_FOR_TICKETS;
			}
			foreach(var shuffledTicket in Data.OpenTickets)
			{
				await shuffledTicket.SendQueueUpdate();
			}
		}

		public async Task Think()
		{
			
		}

		async Task SendQueueUpdate()
		{
			var index = Data.Tickets.IndexOf(this);
			await Bot.SendTextMessageAsync(Target, string.Format(Resources.UserQueueUpdate, (Data.Tickets.Count - index).ToString()));
		}

		public async Task Claim(SupportProvider supportProvider)
		{
			await Bot.SendTextMessageAsync(supportProvider.TelegramID, Resources.SupportUserAcceptTicket);
			await Bot.SendTextMessageAsync(Target,
				string.Format(Resources.UserTicketIsAccepted, supportProvider.Name));
			Owner = supportProvider;
			Owner.State = ESupportProviderState.IN_PROGRESS;
			State = ETicketState.IN_PROGRESS;

			foreach(var msg in Messages.Where(m => m.Sender == Target && m.Content != "/start"))
			{
				await Bot.SendTextMessageAsync(supportProvider.TelegramID, $"User: {msg.Content}");
			}
		}
	}
}
