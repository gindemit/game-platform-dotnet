namespace GamePlatform.Features.Contracts
{

    public interface IProgressObserver
    {
        void Observe(ProgressEvent value);
    }
}
