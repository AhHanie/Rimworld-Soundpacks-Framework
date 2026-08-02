using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soundpacks_Framework.Audio
{
    public sealed class AudioDecodeQueue
    {
        private readonly SemaphoreSlim _gate;

        public AudioDecodeQueue(int maxConcurrency = 0)
        {
            int concurrency = maxConcurrency > 0 ? maxConcurrency : Math.Max(2, Environment.ProcessorCount);
            _gate = new SemaphoreSlim(concurrency, concurrency);
        }

        public Task<AudioDecodeResult> EnqueueDecodeAsync(string path)
        {
            return Task.Run(async () =>
            {
                await _gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    return AudioDecodeService.Decode(path);
                }
                finally
                {
                    _gate.Release();
                }
            });
        }
    }
}
