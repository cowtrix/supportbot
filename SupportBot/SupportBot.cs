using Common;
using Common.Commands;
using Common.Config;
using Common.Security;
using Newtonsoft.Json;
using SupportBot.SupportProviders;
using SupportBot.Tickets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;

namespace SupportBot
{
	public static class SupportBot
	{
		public static ProgramData Data { get; private set; }
		public static TelegramBotClient Bot { get; private set; }

		private static ConfigVariable<string> m_botKey = new ConfigVariable<string>("TelegramBotToken", "");
		private static ConfigVariable<List<int>> m_admins = new ConfigVariable<List<int>>("Administrators", new List<int>());
		private static string m_dataPath;

		static bool LoadFromFile(string path)
		{
			if(string.IsNullOrEmpty(path))
			{
				return false;
			}
			path = Path.GetFullPath(path);
			if (!File.Exists(path))
			{
				return false;
			}
			m_dataPath = path;
			Data = JsonConvert.DeserializeObject<ProgramData>(File.ReadAllText(m_dataPath));
			Logger.Info($"Loaded data from {m_dataPath}");
			Logger.Info($"Admins: \n" + string.Join("\n", m_admins.Value));
			return true;
		}

		static void SaveToFile(string path)
		{
			File.WriteAllText(path, JsonConvert.SerializeObject(Data, Formatting.Indented));
			if (!File.Exists(path))
			{
				throw new FileNotFoundException($"Failed to save data to {path}!");
			}
		}

		public static async Task Initialise(bool dummy = false)
		{
			if (!LoadFromFile(m_dataPath) && !LoadFromFile("data.json"))
			{
				Logger.Warning($"No data path specified, new bot...");
				m_dataPath = Path.GetFullPath("data.json");
				Data = new ProgramData();
			}
			Task saveTask = new Task(async () =>
			{
				while (true)
				{
					SaveToFile(m_dataPath);
					await Task.Delay(TimeSpan.FromSeconds(30));
				}
			});
			saveTask.Start();
			// Hack, remove soon
			if (Data.Tickets.Any(t => Data.SupportProviders.Any(sp => sp.TelegramID == t.Target)))
			{
				Data.Tickets = Data.Tickets.Where(t => !Data.SupportProviders.Any(sp => sp.TelegramID == t.Target)).ToList();
				Logger.Warning("Successfully removed bad tickets");
			}

			if(!dummy)
			{
				var token = m_botKey.Value;
				if (string.IsNullOrEmpty(token))
				{
					Console.WriteLine("Bot Token: ");
					token = Console.ReadLine();
				}
				Bot = new TelegramBotClient(token);
				var me = await Bot.GetMeAsync();
				Logger.Info($"Logged in as {me.FirstName} ({me.Id})");
				Bot.OnMessage += HandleMessage;
				Bot.OnCallbackQuery += HandleCallback;
				Bot.StartReceiving();
			}
			
			var thinkTask = new Task(async () =>
			{
				while (true)
				{
					await SupportProvider.Think();
					foreach (var ticket in Data.Tickets.Where(t => t.State != ETicketState.FINISHED))
					{
						await ticket.Think();
					}
					await Task.Delay(TimeSpan.FromSeconds(1));
				}
			});
			thinkTask.Start();

			while (true)
			{
				var input = Console.ReadLine();
				CommandManager.Execute(input);
			}
		}

		static async Task Main(string[] args)
		{
			m_dataPath = args.FirstOrDefault();
			await Initialise();
		}

		private static void HandleCallback(object sender, CallbackQueryEventArgs e)
		{
			var task = new Task(async () => await HandleCallbackAsync(sender, e));
			task.Start();
		}

		public static async Task HandleCallbackAsync(object sender, CallbackQueryEventArgs e)
		{
			var userID = e.CallbackQuery.From.Id;
			Logger.Debug($"Callback from {userID}: {e.CallbackQuery.Data}");

			// delete button on press
			await Bot.EditMessageReplyMarkupAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId);

			// Check if user is a support provider
			var sup = Data.SupportProviders.SingleOrDefault(s => s.TelegramID == userID);
			if (sup != null)
			{
				await sup.HandleCallback(sender, e);
				return;
			}

			// Otherwise its a rebel needing support
			var ticket = Data.Tickets.SingleOrDefault(t => t.IsActive && t.Target == userID);
			if (ticket == null)
			{
				ticket = new Ticket(userID);
				Data.Tickets.Add(ticket);
			}
			await ticket.HandleCallback(sender, e);
		}

		private static void HandleMessage(object sender, MessageEventArgs e)
		{
			var task = new Task(async () => await HandleMessageAsync(sender, e));
			task.Start();
		}

		public static async Task HandleMessageAsync(object sender, MessageEventArgs e)
		{
			var messageContent = e.Message.Text;
			var userID = e.Message.From.Id;
			Logger.Debug($"Message from {userID}: {messageContent}");
			if(e.Message.ForwardFrom != null)
			{
				Logger.Debug($"Message forwarded from {e.Message.ForwardFrom.Id}");
			}

			// Check if user is a support provider
			var sup = Data.SupportProviders.SingleOrDefault(s => s.TelegramID == userID);

			// Check if user is admin adding new Support Provider
			if (m_admins.Value.Any(u => u == userID))
			{
				if(messageContent == "/addnew")
				{
					var newCode = KeyGenerator.GetUniqueKey(32);
					Data.SupportProviderTokens.Add(newCode);
					await Bot.SendTextMessageAsync(userID, Resources.AdminNewSupportProviderToken);
					await Bot.SendTextMessageAsync(userID, newCode);
					return;
				}
				if(sup == null)
				{
					sup = new SupportProvider(e.Message.From);
					Data.SupportProviders.Add(sup);
					await Bot.SendTextMessageAsync(userID, Resources.AdminHelp);
				}
			}

			if (Data.SupportProviderTokens.Contains(messageContent))
			{
				// Redeem support provider
				if(sup == null)
				{
					Data.SupportProviderTokens.Remove(messageContent);
					sup = new SupportProvider(e.Message.From);
					Data.SupportProviders.Add(sup);
					await Bot.SendTextMessageAsync(userID, Resources.SupportProviderTokenRedeemed);
				}
				else
				{
					await Bot.SendTextMessageAsync(userID, Resources.SupportProviderTokenAlreadyRedeemed);
				}
				// Remove any open tickets by new SP
				Data.Tickets = Data.Tickets.Where(t => !Data.SupportProviders.Any(sp => sp.TelegramID == t.Target)).ToList();
			}

			if (sup != null)
			{
				await sup.HandleMessage(sender, e);
				return;
			}

			// Otherwise its a rebel needing support
			var ticket = Data.Tickets.SingleOrDefault(t => t.IsActive && t.Target == userID);
			if (ticket == null)
			{
				ticket = new Ticket(userID);
				Data.Tickets.Add(ticket);
			}
			await ticket.HandleMessage(sender, e);
		}

		[Command("^faketicket", "faketicket /id:<telegramID>", "Add a an artifical ticket with the given ID")]
		public static async Task AddAdminCommand(CommandArguments args)
		{
			var id = args.MustGetValue<int>("id");
			Data.Tickets.Add(new Ticket(id));
			Logger.Info($"Added artificial ticket with id {id}");
		}

		[Command("^shutdown", "shutdown", "Save and shutdown")]
		public static async Task Shutdown(CommandArguments args)
		{
			SaveToFile(m_dataPath);
			Environment.Exit(0);
		}
	}
}
