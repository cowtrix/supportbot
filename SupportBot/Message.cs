using System;

namespace SupportBot
{
	public class Message
	{
		public int Sender { get; set; }
		public string Content { get; set; }
		public DateTime Timestamp { get; set; }

		public Message() { }

		public Message(int sender, string content)
		{
			Sender = sender;
			Content = content;
			Timestamp = DateTime.Now;
		}
	}
}
