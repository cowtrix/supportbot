using SupportBot.SupportProviders;
using SupportBot.Tickets;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SupportBot
{
	public class ProgramData
	{
		[JsonIgnore]
		public IEnumerable<Ticket> OpenTickets => Tickets
			.Where(t => t.State == ETicketState.WAIT_IN_QUEUE)
			.OrderBy(t => t.DateCreated);

		public List<SupportProvider> SupportProviders = new List<SupportProvider>();
		public List<Ticket> Tickets = new List<Ticket>();
		public List<string> SupportProviderTokens = new List<string>();
		public List<int> Administrators = new List<int>();
	}
}
