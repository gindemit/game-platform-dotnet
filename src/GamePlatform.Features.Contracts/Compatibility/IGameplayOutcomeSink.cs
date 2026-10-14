using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts
{

    public interface IGameplayOutcomeSink
    {
        void Report(GameplayOutcome outcome);
    }
}
