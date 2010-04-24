﻿using System;

namespace Guanima.Redis.Commands.Generic
{
    [Serializable]
    public sealed class EchoCommand : RedisCommand
    {
        public EchoCommand(RedisValue value)
            : base(value)
        {
        }
    }
}
