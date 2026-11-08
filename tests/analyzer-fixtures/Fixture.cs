using System;
using System.Threading.Tasks;

namespace AnalyzerFixture
{
    public static class PolicyFixture
    {
#if BAD_RETHROW
        public static void Rethrow()
        {
            try
            {
                throw new InvalidOperationException();
            }
            catch (Exception error)
            {
                throw error;
            }
        }
#elif BAD_VALUETASK
        public static int ReadTwice()
        {
            var value = ObtainAsync();
            return value.Result + value.Result;
        }

        private static async ValueTask<int> ObtainAsync()
        {
            await Task.Yield();
            return 42;
        }
#else
        public static async Task<int> ConsumeOnceAsync(ValueTask<int> value)
        {
            return await value.ConfigureAwait(false);
        }
#endif
    }
}
