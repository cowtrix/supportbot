using Common;
using Common.Commands;
using Common.Config;
using Common.Security;
using Newtonsoft.Json;
using SupportBot.SupportProviders;
using SupportBot.Tickets;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;

namespace SupportBot
{
	class Program
	{
		public static ProgramData Data { get; private set; }
		public static TelegramBotClient Bot { get; private set; }

		private static ConfigVariable<string> m_botKey = new ConfigVariable<string>("TelegramBotToken", "");
		private static string m_dataPath;

		static bool LoadFromFile(string path)
		{
			path = Path.GetFullPath(path);
			if (!File.Exists(path))
			{
				return false;
			}
			m_dataPath = path;
			Data = JsonConvert.DeserializeObject<ProgramData>(File.ReadAllText(m_dataPath));
			Logger.Info($"Loaded data from {m_dataPath}");
			return true;
		}

		static async Task Main(string[] args)
		{
			if ((args.Length > 0 && !LoadFromFile(args[0])) || !LoadFromFile("data.json"))
			{
				Logger.Warning($"No data path specified, new bot...");
				m_dataPath = Path.GetFullPath("data.json");
				Data = new ProgramData();
				Data.SupportProviders.Add(new SupportProvider()
				{
					TelegramID = 834876848,
					Name = "Sean",
				});
			}
			Task saveTask = new Task(async () =>
			{
				while (true)
				{
					File.WriteAllText(m_dataPath, JsonConvert.SerializeObject(Data, Formatting.Indented));
					if(!File.Exists(m_dataPath))
					{
						throw new FileNotFoundException($"Failed to save data to {m_dataPath}!");
					}
					Logger.Debug($"Saved data to {m_dataPath}");
					await Task.Delay(TimeSpan.FromSeconds(30));
				}
			});
			saveTask.Start();

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

			var thinkTask = new Task(async () =>
			{
				while(true)
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

		private static void HandleCallback(object sender, CallbackQueryEventArgs e)
		{
			var task = new Task(async () => await HandleCallbackAsync(sender, e));
			task.Start();
		}

		private static async Task HandleCallbackAsync(object sender, CallbackQueryEventArgs e)
		{
			var userID = e.CallbackQuery.From.Id;
			Logger.Debug($"Callback from {userID}: {e.CallbackQuery.Data}");

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

		private static async Task HandleMessageAsync(object sender, MessageEventArgs e)
		{
			var messageContent = e.Message.Text;
			var userID = e.Message.From.Id;
			Logger.Debug($"Message from {userID}: {messageContent}");

			// Check if user is admin adding new Support Provider
			if(Data.Administrators.Contains(userID) && messageContent == "/addnew")
			{
				var newCode = KeyGenerator.GetUniqueKey(32);
				Data.SupportProviderTokens.Add(newCode);
				await Bot.SendTextMessageAsync(userID, Resources.AdminNewSupportProviderToken);
				await Bot.SendTextMessageAsync(userID, newCode);
				return;
			}

			// Check if user is a support provider
			var sup = Data.SupportProviders.SingleOrDefault(s => s.TelegramID == userID);
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
	}
}
