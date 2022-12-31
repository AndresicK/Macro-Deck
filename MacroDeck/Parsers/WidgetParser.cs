using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MacroDeck.RPC.Models;
using SuchByte.MacroDeck.Icons;
using SuchByte.MacroDeck.Utils;

namespace SuchByte.MacroDeck.Parsers;

public static class WidgetParserExtensions
{
    public static WidgetDto ToWidgetDto(this ActionButton.ActionButton actionButton) =>
        WidgetParser.ActionButtonToWidgetDto(actionButton);
}

public class WidgetParser
{
    public static WidgetDto ActionButtonToWidgetDto(ActionButton.ActionButton actionButton)
    {
        var iconBase64 = "";
        var labelBase64 = "";
        var backgroundColorHex = actionButton.State
            ? actionButton.BackColorOn.ToHex()
            : actionButton.BackColorOff.ToHex();

        if (!actionButton.State)
        {
            if (!string.IsNullOrWhiteSpace(actionButton.IconOff))
            {
                var icon = IconManager.GetIconByString(actionButton.IconOff);
                if (icon != null)
                {
                    iconBase64 = icon.IconBase64;
                }
            }
            if (!string.IsNullOrWhiteSpace(actionButton.LabelOff.LabelText))
            {
                labelBase64 = actionButton.LabelOff.LabelBase64 ?? "";
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(actionButton.IconOn))
            {
                var icon = IconManager.GetIconByString(actionButton.IconOn);
                if (icon != null)
                {
                    iconBase64 = icon.IconBase64;
                }
            }
            if (!string.IsNullOrWhiteSpace(actionButton.LabelOn.LabelText))
            {
                labelBase64 = actionButton.LabelOn.LabelBase64 ?? "";
            }
        }

        var widgetData = new ActionButtonDto()
        {
            State = actionButton.State,
            LabelBase64 = labelBase64,
            BackgroundBase64Hex = iconBase64,
            BackgroundColorHex = backgroundColorHex
        };

        var widget = new WidgetDto()
        {
            Column = actionButton.Position_X,
            ColumnSpan = 1,
            Row = actionButton.Position_Y,
            RowSpan = 1,
            Id = actionButton.Guid,
            WidgetData = widgetData,
        };
        return widget;
    }

}