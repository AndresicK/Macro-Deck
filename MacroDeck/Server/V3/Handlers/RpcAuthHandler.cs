using System.Text.Json.Nodes;
using MacroDeck.RPC.Exceptions;
using SuchByte.MacroDeck.Device;
using SuchByte.MacroDeck.Interfaces;
using SuchByte.MacroDeck.Logging;

namespace SuchByte.MacroDeck.Server.V3.Handlers;


public sealed class RpcAuthHandler : IRpcHandler
{
    public string Command => "Auth";
    public object? Do(object? client, JsonNode? args)
    {
        IRpcHandler.ThrowIfParamsNull(args);
        IRpcHandler.ThrowIfClientNull(client);

        var clientId = args["Id"]?.ToString();
        if (client is not MacroDeckClient macroDeckClient|| clientId is null)
        {
            throw new ActionInvalidAuthException();
        }

        macroDeckClient.ClientId = clientId;

        if (!DeviceManager.RequestConnection(macroDeckClient))
        {
            throw new ActionInvalidAuthException();
        }

        return "AuthOk";
    }
}