using System.Dynamic;

namespace MacroDeck.RPC.Models;

public class PageDto
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string BackgroundBase64 { get; set; }
    public string BackgroundColorHex { get; set;}
    public int Rows { get; set; }
    public int Columns { get; set; }
    public List<WidgetDto> Widgets { get; set; } = new();

}