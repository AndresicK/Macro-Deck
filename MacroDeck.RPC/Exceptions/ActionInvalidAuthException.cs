using MacroDeck.RPC.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacroDeck.RPC.Exceptions;

public class ActionInvalidAuthException : ActionException
{
    public ActionInvalidAuthException()
        : base(ErrorCode.Generic, "Auth failed due to wrong password or device blacklisted")
    {

    }
}