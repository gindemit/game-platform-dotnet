using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace GamePlatform.Serialization.MessagePack
{

    public sealed class QualificationCodecException : FormatException
    {
        public QualificationCodecException(string message) : base(message) { }
        public QualificationCodecException(string message, Exception inner) : base(message, inner) { }
    }
}
