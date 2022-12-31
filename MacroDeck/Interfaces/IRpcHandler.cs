using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using MacroDeck.RPC.Exceptions;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Server;

namespace SuchByte.MacroDeck.Interfaces;

public interface IRpcHandler
{
    string Command { get; }

    object? Do(object? client, JsonNode? args);


    [DoesNotReturn]
    protected static void ThrowIfParamsNull(JsonNode? args)
    {
        if (args is null)
        {
            throw new ActionInvalidRequestException("params");
        }
    }

    [DoesNotReturn]
    protected static void ThrowIfClientNull(object? client)
    {
        if (client is null or not MacroDeckClient)
        {
            throw new ActionInvalidRequestException("id");
        }
    }

    [DoesNotReturn]
    protected static void ThrowIfClientNotAuthorized(object? client)
    {
        if (client is MacroDeckClient { IsAuthorized: false })
        {
            MacroDeckLogger.Trace($"Authorized: {((MacroDeckClient)client).IsAuthorized}");
            throw new NotAuthorizedException();
        }
    }

}