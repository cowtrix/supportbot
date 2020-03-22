using Common.Config;
using System;
using System.Collections.Generic;

namespace SupportBot
{
	public static class Resources
	{
		public static string UserAcceptsAgreement => String("UserAcceptsAgreement");
		public static string RePromptForAgreement => String("RePromptForAgreement");
		public static string SupportUserHelp => String("SupportUserHelp");
		public static string SupportUserNoActiveTicket => String("SupportUserNoActiveTicket");
		public static string SupportUserWaitingForTicket => String("SupportUserWaitingForTicket");
		public static string SupportUserAcceptTicket => String("SupportUserAcceptTicket");
		public static string UserTicketClosed => String("UserTicketClosed");
		public static string SupportUserTicketClosed => String("SupportUserTicketClosed");
		public static string SupportUserTicketCanBeClaimed => String("SupportUserTicketCanBeClaimed");
		public static string UserQueueUpdate => String("UserQueueUpdate");
		public static string UserTicketIsAccepted => String("UserTicketIsAccepted");
		public static string SupportUserBeatenInClaim => String("SupportUserBeatenInClaim");

		private static ConfigVariable<Dictionary<string, string>> m_resourceStrings = new ConfigVariable<Dictionary<string, string>>("ResourceStrings",
			new Dictionary<string, string>()
			{
				{ "UserAcceptsAgreement", "Thanks {0}, you will be connected with someone to help you as soon as possible." },
				{ "RePromptForAgreement", "Please press the \"I Accept\" button above to proceed." },
				{ "SupportUserHelp", "When you're ready to take a support ticket, press the \"I Can Help Now\" button below, and you'll be connected with someone needing help. You'll send messages through this chat, and that person will receive those messages from this bot. When you've finished helping someone, say \"/endchat\" and you'll be disconnected." },
				{ "SupportUserNoActiveTicket", "You don't currently have an open support ticket. Please wait for a new ticket to come in." },
				{ "SupportUserWaitingForTicket", "There aren't any open support tickets right now, but I'll let you know when there is." },
				{ "SupportUserAcceptTicket", "You've been connected with a user needing support. Just send messages in this chat and they will receive them. When you're finished providing support, say \"/endchat\"." },
				{ "UserTicketClosed", "The chat session has been ended. If you need help again, you can just send us another message." },
				{ "SupportUserTicketClosed", "The chat session has been ended." },
				{ "SupportUserTicketCanBeClaimed", "A new support ticket needs help." },
				{ "UserQueueUpdate", "You are number {0} in the queue. Thank you for waiting." },
				{ "UserTicketIsAccepted", "You've been connected to {0}." },
				{ "SupportUserBeatenInClaim", "Sorry, someone else claimed the ticket before you. Please wait for the next one." },
			});

		public static string String(string key)
		{
			return m_resourceStrings.Value[key];
		}
	}
}
