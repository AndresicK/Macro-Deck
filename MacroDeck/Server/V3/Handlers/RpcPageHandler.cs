using System.Text.Json.Nodes;
using SuchByte.MacroDeck.Interfaces;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Parsers;

namespace SuchByte.MacroDeck.Server.V3.Handlers;

public class RpcPageHandler
{
}


public sealed class RpcGetPageHandler : RpcPageHandler, IRpcHandler
{
    public string Command => "GetPage";
    public object? Do(object? client, JsonNode? args)
    {
        IRpcHandler.ThrowIfClientNull(client);
        //IRpcHandler.ThrowIfClientNotAuthorized(client);

        var macroDeckClient = client as MacroDeckClient;

        MacroDeckLogger.Trace($"Handler authorized: {macroDeckClient.IsAuthorized}");

        return macroDeckClient.Folder.ToPageDto();
    }
}