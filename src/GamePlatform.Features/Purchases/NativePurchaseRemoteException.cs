using System;
namespace GamePlatform.Features.Purchases
{
    public sealed class NativePurchaseRemoteException : Exception
    {
        public NativePurchaseRemoteException(int statusCode) : base("Native purchase service returned an error.") { StatusCode = statusCode; }
        public int StatusCode { get; }
    }
}
