﻿using Wirehome.Core.Exceptions;

namespace Wirehome.Core.Automations.Exceptions
{
    public class AutomationNotFoundException : WirehomeException
    {
        public AutomationNotFoundException(string uid) :
            base($"Automation with UID '{uid}' not found.")
        {
        }
    }
}
