using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using AngryWasp.Logger;
using AngryWasp.Net;
using Common;
using Microsoft.Data.Sqlite;

namespace Node
{
    public static class Database
    {
        private static AsyncLock dbLock = new AsyncLock();
        private static string connectionString;

        public static void Initialize(string walletFileName)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"{walletFileName}.db");
            connectionString = $"Data Source={path};Pooling=True;";

            GetVersion();
            CreateDb();
        }

        private static void GetVersion()
        {
            using (var dbConnection = new SqliteConnection(connectionString))
            {
                dbConnection.Open();
                using var cmd = new SqliteCommand("SELECT SQLITE_VERSION()", dbConnection);
                string version = cmd.ExecuteScalar().ToString();
                Console.WriteLine($"SQLite version: {version}");
            }
        }

        private static void CreateDb()
        {
            using (var dbConnection = new SqliteConnection(connectionString))
            {
                dbConnection.Open();
                using (var cmd = new SqliteCommand("SELECT count(*) FROM sqlite_master WHERE type='table' AND name='blocks';", dbConnection))
                {
                    if ((long)cmd.ExecuteScalar() > 0)
                        return;
                }

                using (var cmd = new SqliteCommand(
                    @"CREATE TABLE blocks (
                        version INTEGER NOT NULL,
                        timestamp TEXT NOT NULL,
                        lastHash TEXT NOT NULL,
                        validator TEXT NOT NULL,
                        hash TEXT PRIMARY KEY,
                        signature TEXT NOT NULL,
                        sponsors TEXT NOT NULL,
                        WITHOUT ROWID
                    );", dbConnection))
                    cmd.ExecuteNonQuery();

                using (var cmd = new SqliteCommand(
                    @"CREATE TABLE transactions (
                        version INTEGER NOT NULL,
                        type INTEGER NOT NULL,
                        nonce INTEGER NOT NULL,
                        fromAddress TEXT NOT NULL,
                        toAddress TEXT NOT NULL,
                        data TEXT NOT NULL,
                        hash TEXT PRIMARY KEY,
                        signature TEXT NOT NULL,
                        blockHash TEXT NOT NULL,
                        txIndex INTEGER NOT NULL,
                        WITHOUT ROWID
                    );", dbConnection))
                    cmd.ExecuteNonQuery();

                using (var cmd = new SqliteCommand(
                    @"CREATE TABLE peerList (
                        host TEXT NOT NULL,
                        port INTEGER NOT NULL,
                        timestamp TEXT NOT NULL,
                        connectionId TEXT PRIMARY KEY,
                        WITHOUT ROWID
                    );", dbConnection))
                    cmd.ExecuteNonQuery();
            }
        }

        public static async Task InsertBlock(Block block)
        {
            using (await dbLock.LockAsync())
            {
                using (var dbConnection = new SqliteConnection(connectionString))
                {
                    dbConnection.Open();

                    using (var cmd = new SqliteCommand($"SELECT count(*) FROM blocks WHERE hash='{block.Hash}';", dbConnection))
                    {
                        if ((long)cmd.ExecuteScalar() > 0)
                            return;
                    }

                    string sponsors = string.Empty;

                    foreach (var s in block.Sponsors)
                        sponsors += $"{s};";

                    using (var cmd = new SqliteCommand($"INSERT INTO blocks (version, timestamp, lastHash, validator, hash, signature, sponsors) VALUES ('{block.Version}', '{block.Timestamp}', '{block.LastHash}', '{block.Validator}', '{block.Hash}', '{block.Signature}', '{sponsors}')", dbConnection))
                        cmd.ExecuteNonQuery();
                }
            }
        }

        public static async Task InsertBlockTransactions(Block block)
        {
            using (await dbLock.LockAsync())
            {
                using (var dbConnection = new SqliteConnection(connectionString))
                {
                    dbConnection.Open();

                    int index = 0;

                    foreach (var tx in block.Transactions)
                    {
                        using (var cmd = new SqliteCommand($"SELECT count(*) FROM transactions WHERE hash='{tx.Hash}';", dbConnection))
                        {
                            if ((long)cmd.ExecuteScalar() > 0)
                                continue;
                        }

                        using (var cmd = new SqliteCommand($"INSERT INTO transactions (version, type, nonce, fromAddress, toAddress, data, hash, signature, blockHash, txIndex) VALUES ('{tx.Version}', '{(int)tx.Type}', '{tx.Nonce}', '{tx.From}', '{tx.To}', '{tx.Data.ToHex()}', '{tx.Hash}', '{tx.Signature}', '{block.Hash}', '{index++}')", dbConnection))
                            cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static long GetBlockCount()
        {
            using (var dbConnection = new SqliteConnection(connectionString))
            {
                dbConnection.Open();

                using (var cmd = new SqliteCommand($"SELECT count(*) FROM blocks;", dbConnection))
                    return (long)cmd.ExecuteScalar();
            }
        }

        public static async Task InsertPeer(Connection connection, ulong timestamp)
        {
            using (await dbLock.LockAsync())
            {
                using (var dbConnection = new SqliteConnection(connectionString))
                {
                    dbConnection.Open();

                    using (var selectCmd = new SqliteCommand($"SELECT count(*) FROM peerList WHERE connectionId='{connection.PeerId.ToByte().ToHex()}';", dbConnection))
                    {
                        if ((long)selectCmd.ExecuteScalar() > 0)
                        {
                            using (var updateCmd = new SqliteCommand($"UPDATE peerList SET timestamp='{timestamp}' WHERE connectionId='{connection.PeerId.ToByte().ToHex()}';", dbConnection))
                            {
                                updateCmd.ExecuteNonQuery();
                                return;
                            }
                        }
                    }

                    using (var cmd = new SqliteCommand($"INSERT INTO peerList (host, port, timestamp, connectionId) VALUES ('{connection.Address}', '{connection.Port}', '{DateTimeHelper.TimestampNow}', '{connection.PeerId.ToByte().ToHex()}')", dbConnection))
                        cmd.ExecuteNonQuery();
                }
            }
        }

        public static List<AngryWasp.Net.Node> SelectMostRecentPeers(int count)
        {
            using (var dbConnection = new SqliteConnection(connectionString))
            {
                dbConnection.Open();

                using (var cmd = new SqliteCommand($"SELECT * FROM peerList ORDER BY timestamp DESC LIMIT {count};", dbConnection))
                {
                    var reader = cmd.ExecuteReader();
                    if (reader == null || !reader.HasRows)
                        return new List<AngryWasp.Net.Node>();

                    List<AngryWasp.Net.Node> nodes = new List<AngryWasp.Net.Node>();

                    while (reader.Read())
                    {
                        var host = reader.GetString(0);
                        var port = (ushort)reader.GetInt32(1);
                        var cId = reader.GetString(3);
                        nodes.Add(new AngryWasp.Net.Node(host, port, cId));
                    }

                    return nodes;
                }
            }
        }

        public static Block SelectBlockByHash(HashKey32 blockHash)
        {
            Log.Instance.WriteInfo($"Fetching block {blockHash}");
            var query = $"SELECT * FROM blocks WHERE hash='{blockHash}'";
            using (var dbConnection = new SqliteConnection(connectionString))
            {
                dbConnection.Open();

                using (var cmd = new SqliteCommand(query, dbConnection))
                {
                    var reader = cmd.ExecuteReader();
                    if (reader == null || !reader.HasRows)
                        return null;

                    while (reader.Read())
                    {
                        var block = new Block();
                        block.Version = (ushort)reader.GetInt32(0);
                        block.Timestamp = ulong.Parse(reader.GetString(1));
                        block.LastHash = reader.GetString(2);
                        block.Validator = reader.GetString(3);
                        block.Hash = reader.GetString(4);
                        block.Signature = reader.GetString(5);
                        block.Transactions = SelectBlockTransactionsByBlockHash(block.Hash);
                        var voters = reader.GetString(6).Split(';', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var v in voters)
                            block.Sponsors.Add(v);

                        return block;
                    }

                    return null;
                }
            }
        }

        public static List<Transaction> SelectBlockTransactionsByBlockHash(HashKey32 blockHash)
        {
            var query = $"SELECT * FROM transactions WHERE blockHash='{blockHash}' ORDER BY txIndex ASC";
            using (var dbConnection = new SqliteConnection(connectionString))
            {
                dbConnection.Open();

                using (var cmd = new SqliteCommand(query, dbConnection))
                {
                    var reader = cmd.ExecuteReader();
                    if (reader == null || !reader.HasRows)
                        return new List<Transaction>();

                    var transactions = new SortedDictionary<int, Transaction>();

                    while (reader.Read())
                    {
                        var tx = new Transaction
                        {
                            Version = (ushort)reader.GetInt32(0),
                            Type = (Transaction_Type)reader.GetByte(1),
                            Nonce = (uint)reader.GetInt64(2),
                            From = reader.GetString(3),
                            To = reader.GetString(4),
                            Data = reader.GetString(5).FromByteHex(),
                            Hash = reader.GetString(6),
                            Signature = reader.GetString(7)
                        };

                        int index = reader.GetInt32(9);

                        transactions.Add(index, tx);
                    }

                    return transactions.Values.ToList();
                }
            }
        }

        public static List<Block> SelectAllBlocks()
        {
            Log.Instance.WriteInfo("Fetching all blocks from database");
            var query = $"SELECT * FROM blocks";
            using (var dbConnection = new SqliteConnection(connectionString))
            {
                dbConnection.Open();

                using (var cmd = new SqliteCommand(query, dbConnection))
                {
                    var reader = cmd.ExecuteReader();
                    if (reader == null || !reader.HasRows)
                        return new List<Block>();

                    var blocks = new List<Block>();

                    while (reader.Read())
                    {
                        var block = new Block();
                        block.Version = (ushort)reader.GetInt32(0);
                        block.Timestamp = ulong.Parse(reader.GetString(1));
                        block.LastHash = reader.GetString(2);
                        block.Validator = reader.GetString(3);
                        block.Hash = reader.GetString(4);
                        block.Signature = reader.GetString(5);
                        block.Transactions = SelectBlockTransactionsByBlockHash(block.Hash);
                        var voters = reader.GetString(6).Split(';', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var v in voters)
                            block.Sponsors.Add(v);

                        blocks.Add(block);
                    }

                    return blocks;
                }
            }
        }
    }
}