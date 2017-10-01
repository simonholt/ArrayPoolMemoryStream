using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using BenchmarkDotNet.Running;
using Microsoft.IO;
using System.Buffers;
using System.IO;

namespace Sh.ArrayPoolMemoryStream.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<WriteBenchmarks>();
        }
    }

    [SimpleJob(launchCount: 1, warmupCount: 5, targetCount: 2, invocationCount: 100, id: "QuickJob")]
    [MemoryDiagnoser]
    //[InliningDiagnoser]
    //[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions)]
    public class WriteBenchmarks
    {
        const int writeIterations = 4000;

        [Params(128, 1024)]
        public int BufferSize { get; set; }

        private static ArrayPool<byte> bigArrayPool = ArrayPool<byte>.Create(4 * 1024 * 1024, 4);

        private static RecyclableMemoryStreamManager manager = new RecyclableMemoryStreamManager();

        byte[] buffer = new byte[8 * 1024];    // Must be as bug as max buffersize param

        private void WriteAndConvertToArray(Stream target)
        {
            for (int i = 0; i < writeIterations; i++)
                target.Write(buffer, 0, BufferSize);

            var testArray = bigArrayPool.Rent((int)target.Length);
            try
            {
                target.Seek(0, SeekOrigin.Begin);
                target.Read(testArray, 0, (int)target.Length);
            }
            finally
            {
                bigArrayPool.Return(testArray);
            }

        }

        [Benchmark(Baseline = true)]
        public void WriteUsingDefaultMemoryStream()
        {
            using (var ms = new MemoryStream())
            {
                WriteAndConvertToArray(ms);
            }
        }

        [Benchmark()]
        public void WriteUsingRecyclableMemoryStream()
        {
            using (var ms = manager.GetStream())
            {
                WriteAndConvertToArray(ms);
            }
        }

        [Benchmark()]
        public void WriteUsingArrayPoolMemoryStream()
        {
            using (var ms = new ArrayPoolMemoryStream(bigArrayPool))
            {
                WriteAndConvertToArray(ms);
            }
        }
    }
}
