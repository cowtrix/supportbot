using Common;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;

namespace SupportBot
{
	public abstract class MessageHandler
	{
		public const string ACCEPT_TOKEN = "USER_ACCEPT";
		protected static ProgramData Data => Program.Data;
		protected static TelegramBotClient Bot => Program.Bot;
		public abstract Task HandleCallback(object sender, CallbackQueryEventArgs e);
		public abstract Task HandleMessage(object sender, MessageEventArgs e);

		protected virtual async Task ForwardMessage(int from, int to, string name, MessageEventArgs e)
		{
			Logger.Debug($"Forwarding message from {from} to {to}");
			if(!string.IsNullOrEmpty(e.Message.Text))
			{
				await Bot.SendTextMessageAsync(to, $"{name}: {e.Message.Text}");
			}
			if (e.Message.Photo != null && e.Message.Photo.Any())
			{
				await Bot.SendPhotoAsync(to, e.Message.Photo.First().FileId);
			}
		}
	}
}
