﻿using System.Collections.Generic;
using Wirehome.Core.Repository;

namespace Wirehome.Core.Components.Configuration
{
    public class ComponentAdapterConfiguration
    {
        public RepositoryEntityUid Uid { get; set; }

        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
    }
}
