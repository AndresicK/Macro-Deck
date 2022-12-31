using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using MacroDeck.RPC.Enum;
using MacroDeck.RPC.Exceptions;
using SuchByte.MacroDeck.Enums;
using SuchByte.MacroDeck.Interfaces;

namespace SuchByte.MacroDeck.Server.V3.Handlers;

public class RpcWidgetHandler
{
}

public sealed class RpcWidgetPressedHandler : RpcWidgetHandler, IRpcHandler
{
    public string Command => "WidgetPressed";
    public object? Do(object? client, JsonNode? args)
    {
        IRpcHandler.ThrowIfClientNull(client);
        IRpcHandler.ThrowIfClientNotAuthorized(client);

        var widgetEventValid = Enum.TryParse(args["WidgetEvent"]?.ToString(), out WidgetEvents widgetEvent);
        var widgetId = args["Id"]?.ToString();
        if (!widgetEventValid || string.IsNullOrWhiteSpace(widgetId))
        {
            throw new ActionInvalidRequestException();
        }

        var macroDeckClient = client as MacroDeckClient;

        var buttonPressType = widgetEvent switch
        {
            WidgetEvents.PressedShort => ButtonPressType.SHORT,
            WidgetEvents.ReleasedShort => ButtonPressType.SHORT_RELEASE,
            WidgetEvents.PressedLong => ButtonPressType.LONG,
            WidgetEvents.ReleasedLong => ButtonPressType.LONG_RELEASE,
            _ => ButtonPressType.SHORT
        };

        if (macroDeckClient.Folder?.ActionButtons?.FirstOrDefault(x => x.Guid == widgetId) is { } actionButton)
        {
            MacroDeckServer.Instance.Execute(actionButton, macroDeckClient.ClientId, buttonPressType);
        }

        return null;
    }
}