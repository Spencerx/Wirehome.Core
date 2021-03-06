﻿using System.Collections.Generic;
using Wirehome.Core.Repository;

namespace Wirehome.Core.Automations.Configuration
{
    public class AutomationLogicConfiguration
    {
        public RepositoryEntityUid Uid { get; set; }

        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
    }
}
