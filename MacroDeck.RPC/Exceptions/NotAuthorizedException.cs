using MacroDeck.RPC.Enum;

namespace MacroDeck.RPC.Exceptions;

public class NotAuthorizedException : ActionException
{
    public NotAuthorizedException()
        : base(ErrorCode.Generic, "Client is not authorized")
    {

    }
}