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
	}
}
