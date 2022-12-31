namespace MacroDeck.RPC.Models;

public class WidgetDto
{
    public string Id { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; }
    public int ColumnSpan { get; set; }
    public string WidgetType => "ActionButton"; // Only ActionButton is supported on Macro Deck 2
    public ActionButtonDto WidgetData { get; set; } = new();
    

}