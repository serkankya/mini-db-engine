# dotnet-kv-store

A minimalist, B-Tree based key-value storage engine written from scratch in C# (.NET 10).

I started this project to get a deeper understanding of how database storage engines actually work under the hood (disk I/O, page management, B-Trees, etc.) instead of just relying on SQLite or LiteDB. 

## Current Status
- **Disk I/O:** Custom fixed-size (4KB) page management using raw `FileStream`.
- **Endianness:** Cross-platform safe binary formatting using `BinaryPrimitives`.
- **Persistence:** Basic tests are in place to ensure data survives after closing the stream.

## What's Next?
- Slotted Page architecture (storing actual key-value pairs inside the pages).
- On-disk B-Tree implementation (insert, split, range scans).
- Write-Ahead Log (WAL) for crash recovery.

## Running Tests
You can run the xUnit tests to verify page persistence:
```bash
dotnet test