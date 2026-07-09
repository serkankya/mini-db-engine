using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using MiniDbEngine.Storage;

namespace MiniDbEngine.Benchmarks
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Starting database showdown: MiniDbEngine vs SQLite");
			Summary summary = BenchmarkRunner.Run<DatabaseBenchmark>();
		}
	}
}