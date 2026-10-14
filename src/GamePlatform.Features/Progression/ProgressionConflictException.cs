using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;
using GamePlatform.Features.Contracts;

namespace GamePlatform.Features.Progression
{

    public sealed class ProgressionConflictException : InvalidOperationException
    {
        public ProgressionConflictException(string message) : base(message) { }
    }
}
