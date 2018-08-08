using System;

namespace NuClear.VStore.S3
{
    public class ObjectElementInvalidTypeException : Exception
    {
        public ObjectElementInvalidTypeException(string message) : base(message)
        {
        }
    }
}