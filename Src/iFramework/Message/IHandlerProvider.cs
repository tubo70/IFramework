﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace IFramework.Message
{
    public interface IHandlerProvider
    {
        IMessageHandler GetHandler(Type messageType);
        IList<IMessageHandler> GetHandlers(Type messageType);
    }
}
