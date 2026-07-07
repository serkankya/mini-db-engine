using MiniDbEngine.Storage;
using System.Text;

string dbPath = "b_tree_test.db";
if (File.Exists(dbPath)) File.Delete(dbPath);
if (File.Exists(dbPath + ".wal")) File.Delete(dbPath + ".wal");

using (PageManager pm = new PageManager(dbPath))
using (WalManager wal = new WalManager(dbPath))
{
	BTree db = new BTree(pm, wal);

	Console.WriteLine("Inserting records...");
	for (int i = 1000; i <= 1020; i++)
	{
		byte[] key = Encoding.UTF8.GetBytes($"user_{i}");
		byte[] value = Encoding.UTF8.GetBytes($"Payload_{i}");
		db.Put(key, value);
	}

	Console.WriteLine("\n--- Deleting user_1010 ---");
	db.Delete(Encoding.UTF8.GetBytes("user_1010"));

	Console.WriteLine("\n--- Range Scan (user_1008 to user_1012) ---");
	byte[] startKey = Encoding.UTF8.GetBytes("user_1008");
	byte[] endKey = Encoding.UTF8.GetBytes("user_1012");

	foreach (KeyValuePair<byte[], byte[]> record in db.Scan(startKey, endKey))
	{
		string k = Encoding.UTF8.GetString(record.Key);
		string v = Encoding.UTF8.GetString(record.Value);
		Console.WriteLine($"Found: {k} -> {v}");
	}
}