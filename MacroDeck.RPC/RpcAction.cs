namespace MacroDeck.RPC;

public class RpcAction
{
    public delegate Task ReturnValueAsyncDelegate(object? result);
    public readonly ReturnValueAsyncDelegate ReturnValueCallbackAsync;
    public readonly Request Request;
    public readonly object? MacroDeckClient;

    public RpcAction(object macroDeckClient, Request request, ReturnValueAsyncDelegate returnValueCallbackAsync)
    {
        MacroDeckClient = macroDeckClient;
        Request = request;
        ReturnValueCallbackAsync = returnValueCallbackAsync;
    }
}