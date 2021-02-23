using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FontAwesome5;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pinpoint.Core;
using Pinpoint.Core.Results;

namespace Pinpoint.Plugin.Lists
{
    public class ListsPlugin: IPlugin
    {
        private readonly string _remindersDirectoryPath = $"{AppConstants.MainDirectory}/reminders";
        private ReminderService _reminderService;

        private List<Reminder> _reminders = new List<Reminder>()
        {
            new Reminder
            {
                Content = "This is a reminder of something important",
                DueTime = DateTime.Now.AddMinutes(30),
                Dismissed = false
            }
        };
        public PluginMeta Meta { get; set; } = new PluginMeta("Lists Plugin", PluginPriority.Highest);
        public void Load()
        {
            if (!Directory.Exists(_remindersDirectoryPath))
            {
                Directory.CreateDirectory(_remindersDirectoryPath);
                var json = JsonConvert.SerializeObject(new ListPluginData());

                File.WriteAllText($"{_remindersDirectoryPath}/reminders.json", json);
            }

            _reminderService = new ReminderService(_remindersDirectoryPath);

            _reminderService.GetReminders().ContinueWith(async remindersTask =>
            {
                var reminders = await remindersTask;

                _reminders = reminders;
            });
        }

        public void Unload()
        {
            _reminders.Clear();
        }

        public async Task<bool> Activate(Query query)
        {
            //Activate if any reminders are due within the next hour.
            var shouldActivate = _reminders.Any(ReminderIsUrgentPredicate);

            return shouldActivate;
        }

        public async IAsyncEnumerable<AbstractQueryResult> Process(Query query)
        {
            foreach (var reminder in _reminders.Where(ReminderIsUrgentPredicate))
            {
                yield return new ReminderResult(reminder);
            }

            if (query.RawQuery.StartsWith("remind"))
            {
                yield return new AddReminderResult();
            }
        }

        private async Task AddReminder(Reminder reminder)
        {
            _reminders.Add(reminder);
            await _reminderService.AddReminder(reminder);
        }

        private bool ReminderIsUrgentPredicate(Reminder reminder)
        {
            return reminder.DueTime > DateTime.Now &&
                   DateTime.Now.Subtract(new TimeSpan(0, 1, 0, 0)) <= reminder.DueTime &&
                   !reminder.Dismissed;
        }
    }

    public class ReminderResult: AbstractFontAwesomeQueryResult
    {
        private readonly Reminder _reminder;

        public ReminderResult(Reminder reminder): base(reminder.Content, "Select to dismiss")
        {
            _reminder = reminder;
        }
        public override void OnSelect()
        {
            _reminder.Dismissed = true;
        }

        public override EFontAwesomeIcon FontAwesomeIcon => EFontAwesomeIcon.Solid_Clock;
    }

    public class AddReminderResult: AbstractFontAwesomeQueryResult
    {
        private readonly ReminderService _reminderService;
        private readonly Reminder _reminder;

        public AddReminderResult(ReminderService reminderService, Reminder reminder): base("Add reminder")
        {
            _reminderService = reminderService;
            _reminder = reminder;
        }
        public override void OnSelect()
        {
            _reminderService.AddReminder(_reminder);
        }

        public override EFontAwesomeIcon FontAwesomeIcon => EFontAwesomeIcon.Solid_Clock;
    }

    public class ReminderService
    {
        private readonly string _remindersFilePath;

        public ReminderService(string remindersDirectoryPath)
        {
            _remindersFilePath = $"{remindersDirectoryPath}/reminders.json";
        }

        public async Task<List<Reminder>> GetReminders()
        {
            if (!File.Exists(_remindersFilePath)) return new List<Reminder>();

            var json = await File.ReadAllTextAsync(_remindersFilePath);

            if (string.IsNullOrWhiteSpace(json)) return new List<Reminder>();

            var data = JsonConvert.DeserializeObject<ListPluginData>(json);

            return data.Reminders ?? new List<Reminder>();
        }

        public async Task AddReminder(Reminder reminder)
        {
            var json = await File.ReadAllTextAsync(_remindersFilePath);

            if (!File.Exists(_remindersFilePath) || string.IsNullOrWhiteSpace(json)) return;

            var data = JsonConvert.DeserializeObject<ListPluginData>(json);

            data.Reminders.Add(reminder);

            var dataJson = JsonConvert.SerializeObject(data);

            await File.WriteAllTextAsync(_remindersFilePath, dataJson);
        }
    }

    public class Reminder
    {
        public string Content { get; set; }
        public DateTime DueTime { get; set; }
        public bool Dismissed { get; set; }
    }

    public class ListPluginData
    {
        public List<Reminder> Reminders { get; set; } = new List<Reminder>();
    }
}
