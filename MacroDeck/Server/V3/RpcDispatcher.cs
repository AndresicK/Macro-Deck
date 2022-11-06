using MacroDeck.RPC;
using SuchByte.MacroDeck.Interfaces;
using System;

namespace SuchByte.MacroDeck.Server.V3;

public class RpcDispatcher : IObserver<RpcAction>
{
    private readonly IRpcHandlerFactory _rpcHandlerFactory;

    public RpcDispatcher(
        IRpcHandlerFactory rpcHandlerFactory
    )
    {
        _rpcHandlerFactory = rpcHandlerFactory;
    }

    public void OnCompleted()
    {
        throw new NotImplementedException();
    }

    public void OnError(Exception error)
    {
        throw new NotImplementedException();
    }

    public void OnNext(RpcAction action)
    {
        if (!_rpcHandlerFactory.TryGet(action.Request.Method, out var rpcHandler)) return;
        var result = rpcHandler!.Do(action.MacroDeckClient, action.Request.ParamsNode);
        _ = action.ReturnValueCallbackAsync(result);
    }
}