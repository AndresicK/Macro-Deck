using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using MacroDeck.RPC.Models;
using SuchByte.MacroDeck.Folders;
using SuchByte.MacroDeck.Icons;
using SuchByte.MacroDeck.Profiles;
using SuchByte.MacroDeck.Utils;

namespace SuchByte.MacroDeck.Parsers;

public static class PageParserExtensions
{
    public static PageDto ToPageDto(this MacroDeckFolder folder) =>
        PageParser.FolderToPageDto(folder);
}

public class PageParser
{
    public static PageDto FolderToPageDto(MacroDeckFolder folder)
    {
        var profile = ProfileManager.FindProfileByFolder(folder);
        var widgets = new ConcurrentBag<WidgetDto>();
        Parallel.ForEach(folder?.ActionButtons ?? Enumerable.Empty<ActionButton.ActionButton>(), actionButton =>
        {
            widgets.Add(actionButton.ToWidgetDto());
        });

        var pageDto = new PageDto()
        {
            BackgroundBase64 = string.Empty,
            BackgroundColorHex = "#252525",
            Columns = profile?.Columns ?? 5,
            Rows = profile?.Rows ?? 3,
            Id = folder!.FolderId,
            DisplayName = folder!.DisplayName,
            Widgets = widgets.ToList()
        };

        return pageDto;
    }
}