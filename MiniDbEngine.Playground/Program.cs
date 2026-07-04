using MiniDbEngine.Storage;
using System.Text;

string dbPath = "b_tree_test.db";
if (File.Exists(dbPath)) File.Delete(dbPath);

using (PageManager pm = new PageManager(dbPath))
{
	BTree db = new BTree(pm);

	Console.WriteLine("Inserting 10,000 records...");
	for (int i = 0; i < 10000; i++)
	{
		byte[] key = Encoding.UTF8.GetBytes($"user_{i:D5}");
		byte[] value = Encoding.UTF8.GetBytes($"Data_Payload_{i}");
		db.Put(key, value);
	}

	Console.WriteLine("Fetching record user_07530...");
	byte[] searchKey = Encoding.UTF8.GetBytes("user_07530");

	if (db.Get(searchKey, out byte[] result))
	{
		Console.WriteLine($"Found! Value: {Encoding.UTF8.GetString(result)}");
	}
	else
	{
		Console.WriteLine("Not found!");
	}
}