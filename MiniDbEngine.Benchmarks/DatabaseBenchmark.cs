using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;
using MiniDbEngine.Storage;
using System.Text;

namespace MiniDbEngine.Benchmarks
{
	[MemoryDiagnoser]
	public class DatabaseBenchmark
	{
		private const int TotalRecords = 10000;
		private const string MiniDbPath = "benchmark_mini.db";
		private const string SqlitePath = "benchmark_sqlite.db";

		private PageManager _pm;
		private WalManager _wal;
		private BTree _miniDb;
		private SqliteConnection _sqliteDb;

		[IterationSetup]
		public void Setup()
		{
			if (File.Exists(MiniDbPath)) File.Delete(MiniDbPath);
			if (File.Exists(MiniDbPath + ".wal")) File.Delete(MiniDbPath + ".wal");
			if (File.Exists(SqlitePath)) File.Delete(SqlitePath);

			_pm = new PageManager(MiniDbPath);
			_wal = new WalManager(MiniDbPath);
			_miniDb = new BTree(_pm, _wal);

			_sqliteDb = new SqliteConnection($"Data Source={SqlitePath}");
			_sqliteDb.Open();
			using SqliteCommand cmd = _sqliteDb.CreateCommand();
			cmd.CommandText = "CREATE TABLE KV (Id TEXT PRIMARY KEY, Data TEXT)";
			cmd.ExecuteNonQuery();
		}

		[Benchmark]
		public void CustomEngine_Insert_10K()
		{
			for (int i = 0; i < TotalRecords; i++)
			{
				byte[] key = Encoding.UTF8.GetBytes($"user_{i:D5}");
				byte[] value = Encoding.UTF8.GetBytes($"Data_Payload_{i}");
				_miniDb.Put(key, value);
			}
		}

		[Benchmark]
		public void SQLite_Insert_10K()
		{
			using SqliteTransaction transaction = _sqliteDb.BeginTransaction();
			using SqliteCommand cmd = _sqliteDb.CreateCommand();
			cmd.CommandText = "INSERT INTO KV (Id, Data) VALUES ($id, $data)";

			SqliteParameter idParam = cmd.Parameters.Add("$id", SqliteType.Text);
			SqliteParameter dataParam = cmd.Parameters.Add("$data", SqliteType.Text);

			for (int i = 0; i < TotalRecords; i++)
			{
				idParam.Value = $"user_{i:D5}";
				dataParam.Value = $"Data_Payload_{i}";
				cmd.ExecuteNonQuery();
			}
			transaction.Commit();
		}

		[IterationCleanup]
		public void Cleanup()
		{
			//dispose our custom engine resources
			_pm?.Dispose();
			_wal?.Dispose();

			//dispose SQLite resources
			if (_sqliteDb != null)
			{
				_sqliteDb.Dispose();

				SqliteConnection.ClearAllPools();
			}

			System.Threading.Thread.Sleep(10);
		}
	}
}