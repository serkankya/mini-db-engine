using MiniDbEngine.Storage;

string dbPath = "test.db";

if (File.Exists(dbPath))
	File.Delete(dbPath);

using PageManager pm = new PageManager(dbPath);

Page leaf = pm.AllocatePage(pageType: 1);// 1 = leaf
Console.WriteLine($"New page Id : {leaf.PageId}, Type : {leaf.PageType}");

leaf.PageType = 99;
pm.WritePage(leaf);

Page readLeaf = pm.ReadPage(leaf.PageId);
Console.WriteLine($"Page Id : {readLeaf.PageId}, Type : {readLeaf.PageType}");

Console.WriteLine("test successful!");
