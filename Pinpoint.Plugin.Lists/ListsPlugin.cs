using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FontAwesome5;
using Pinpoint.Core;
using Pinpoint.Core.Results;

namespace Pinpoint.Plugin.Lists
{
    public class ListsPlugin: IPlugin
    {
        public PluginMeta Meta { get; set; } = new PluginMeta("Lists Plugin", PluginPriority.Highest);
        public void Load()
        {
        }

        public void Unload()
        {
        }

        public Task<bool> Activate(Query query)
        {
            var shouldActivate = query.RawQuery.StartsWith("listadd") || query.RawQuery.StartsWith("l+");

            return Task.FromResult(shouldActivate);
        }

        public async IAsyncEnumerable<AbstractQueryResult> Process(Query query)
        {
            yield return new AddListItemResult();
        }
    }

    public class AddListItemResult : AbstractFontAwesomeQueryResult
    {
        public AddListItemResult(): base("Add note.")
        {
            
        }
        public override void OnSelect()
        {
            
        }

        public override EFontAwesomeIcon FontAwesomeIcon => EFontAwesomeIcon.Solid_Plus;
    }

    public class Lists
    {
        public List<Note> Notes { get; set; }
    }

    public class Note
    {
        public string Content { get; set; }
        public bool Deleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
