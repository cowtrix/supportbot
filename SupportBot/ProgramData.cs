using Common;
using Common.Config;
using Newtonsoft.Json;
using SupportBot.SupportProviders;
using SupportBot.Tickets;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace SupportBot
{
	public class ProgramData
	{
		[JsonIgnore]
		public IEnumerable<Ticket> OpenTickets => Tickets
			.Where(t => t.State == ETicketState.WAIT_IN_QUEUE)
			.OrderBy(t => t.DateCreated);

		[JsonIgnore]
		public static string DataPath = "data.json";

		private ConfigVariable<int> m_backupInterval = new ConfigVariable<int>("BackupIntervalMinutes", 60 * 12);

		public List<SupportProvider> SupportProviders = new List<SupportProvider>();
		public List<Ticket> Tickets = new List<Ticket>();
		public List<string> SupportProviderTokens = new List<string>();

		public static ProgramData LoadFromFile()
		{
			if (string.IsNullOrEmpty(DataPath))
			{
				return null;
			}
			DataPath = Path.GetFullPath(DataPath);
			if (!File.Exists(DataPath))
			{
				Logger.Warning($"No data file found at {DataPath}");
				return null;
			}
			Logger.Info($"Loaded data from {DataPath}");
			return JsonConvert.DeserializeObject<ProgramData>(File.ReadAllText(DataPath));
		}

		public void SaveToFile()
		{
			var str = JsonConvert.SerializeObject(this, Formatting.Indented);
			File.WriteAllText(DataPath, str);
			if (!File.Exists(DataPath))
			{
				throw new FileNotFoundException($"Failed to save data to {DataPath}!");
			}
		}

		public void BeginSaveTask()
		{
			Task saveTask = new Task(async () =>
			{
				while (true)
				{
					await Task.Delay(TimeSpan.FromSeconds(30));
					SaveToFile();
					Logger.Debug($"Saved data to {DataPath}");
				}
			});
			saveTask.Start();

			Task backupTask = new Task(async () =>
			{
				string backupName = Path.GetFullPath("Backups.zip");
				while (true)
				{
					await Task.Delay(TimeSpan.FromMinutes(m_backupInterval.Value));
					var tempPath = $"{DataPath}temp";
					File.Copy(DataPath, tempPath);
					using (ZipArchive archive = ZipFile.Open(backupName, File.Exists(backupName) ? ZipArchiveMode.Update : ZipArchiveMode.Create))
					{
						archive.CreateEntryFromFile(tempPath, $"{DateTime.Now.ToFileTime()}.json");
					}
					File.Delete(tempPath);
					Logger.Debug($"Saved compressed backup to {backupName}");
				}
			});
			backupTask.Start();
		}
	}
}