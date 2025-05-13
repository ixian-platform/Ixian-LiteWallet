using IxianLiteWallet;
using IXICore;
using IXICore.Inventory;
using IXICore.Meta;
using IXICore.Network;
using IXICore.Network.Messages;
using IXICore.Utils;
using LW.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace LW.Network
{
    public class ProtocolMessage
    {
        public static ProtocolMessageCode waitingFor = 0;
        public static byte[] waitForAddress = null;
        public static bool blocked = false;

        public static void setWaitFor(ProtocolMessageCode value, byte[] addr)
        {
            waitingFor = value;
            waitForAddress = addr;
            blocked = true;
        }

        public static void wait(int timeout_seconds)
        {
            DateTime start = DateTime.UtcNow;
            while(blocked)
            {
                if((DateTime.UtcNow - start).TotalSeconds > timeout_seconds)
                {
                    Logging.warn("Timeout occured while waiting for " + waitingFor);
                    break;
                }
                Thread.Sleep(250);
            }
        }

        // Unified protocol message parsing
        public static void parseProtocolMessage(ProtocolMessageCode code, byte[] data, RemoteEndpoint endpoint)
        {
            if (endpoint == null)
            {
                Logging.error("Endpoint was null. parseProtocolMessage");
                return;
            }
            try
            {
                switch (code)
                {
                    case ProtocolMessageCode.hello:
                        using (MemoryStream m = new MemoryStream(data))
                        {
                            using (BinaryReader reader = new BinaryReader(m))
                            {
                                CoreProtocolMessage.processHelloMessageV6(endpoint, reader);
                            }
                        }
                        break;


                    case ProtocolMessageCode.helloData:
                        using (MemoryStream m = new MemoryStream(data))
                        {
                            using (BinaryReader reader = new BinaryReader(m))
                            {
                                if (CoreProtocolMessage.processHelloMessageV6(endpoint, reader))
                                {
                                    char node_type = endpoint.presenceAddress.type;

                                    ulong last_block_num = reader.ReadIxiVarUInt();

                                    int bcLen = (int)reader.ReadIxiVarUInt();
                                    byte[] block_checksum = reader.ReadBytes(bcLen);

                                    endpoint.blockHeight = last_block_num;

                                    int block_version = (int)reader.ReadIxiVarUInt();

                                    // Process the hello data
                                    endpoint.helloReceived = true;
                                    NetworkClientManager.recalculateLocalTimeDifference();

                                    if (endpoint.presenceAddress.type == 'R')
                                    {
                                        string[] connected_servers = StreamClientManager.getConnectedClients(true);
                                        if (connected_servers.Count() == 1 || !connected_servers.Contains(StreamClientManager.primaryS2Address))
                                        {
                                            // TODO set the primary s2 host more efficiently, perhaps allow for multiple s2 primary hosts
                                            StreamClientManager.primaryS2Address = endpoint.getFullAddress(true);
                                            // TODO TODO do not set if directly connectable
                                            IxianHandler.publicIP = endpoint.address;
                                            IxianHandler.publicPort = endpoint.incomingPort;
                                            PresenceList.forceSendKeepAlive = true;
                                            Logging.info("Forcing KA from networkprotocol");
                                        }
                                    }

                                    if (endpoint.presenceAddress.type == 'M' || endpoint.presenceAddress.type == 'H')
                                    {
                                        // Get random presences
                                        endpoint.sendData(ProtocolMessageCode.getRandomPresences, new byte[1] { (byte)'R' });

                                        CoreProtocolMessage.subscribeToEvents(endpoint);
                                    }
                                }
                            }
                        }
                        break;

                    case ProtocolMessageCode.balance2:
                        {
                            using (MemoryStream m = new MemoryStream(data))
                            {
                                using (BinaryReader reader = new BinaryReader(m))
                                {
                                    int address_length = (int)reader.ReadIxiVarUInt();
                                    Address address = new Address(reader.ReadBytes(address_length));

                                    int balance_bytes_len = (int)reader.ReadIxiVarUInt();
                                    byte[] balance_bytes = reader.ReadBytes(balance_bytes_len);

                                    // Retrieve the latest balance
                                    IxiNumber ixi_balance = new IxiNumber(new BigInteger(balance_bytes));

                                    // Retrieve the blockheight for the balance
                                    ulong block_height = reader.ReadIxiVarUInt();

                                    foreach (Balance balance in IxianHandler.balances)
                                    {
                                        if (address.addressNoChecksum.SequenceEqual(balance.address.addressNoChecksum))
                                        {


                                            if (block_height > balance.blockHeight && (balance.balance != ixi_balance || balance.blockHeight == 0))
                                            {
                                                byte[] block_checksum = reader.ReadBytes((int)reader.ReadIxiVarUInt());

                                                balance.address = address;
                                                balance.balance = ixi_balance;
                                                balance.blockHeight = block_height;
                                                balance.blockChecksum = block_checksum;
                                                balance.verified = false;
                                            }

                                            balance.lastUpdate = Clock.getTimestamp();

                                            if (waitingFor == code && waitForAddress != null && waitForAddress.SequenceEqual(address.addressNoChecksum))
                                            {
                                                blocked = false;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;

                    case ProtocolMessageCode.updatePresence:
                        handleUpdatePresence(data, endpoint);
                        break;

                    case ProtocolMessageCode.keepAlivePresence:
                        handleKeepAlivePresence(data, endpoint);
                        break;

                    case ProtocolMessageCode.blockHeaders3:
                        {
                            // Forward the block headers to the TIV handler
                            Node.tiv.receivedBlockHeaders3(data, endpoint);
                        }
                        break;

                    case ProtocolMessageCode.pitData2:
                        {
                            Node.tiv.receivedPIT2(data, endpoint);
                        }
                        break;

                    case ProtocolMessageCode.transactionData2:
                        handleTransactionData(data, endpoint);
                        break;

                    case ProtocolMessageCode.bye:
                        CoreProtocolMessage.processBye(data, endpoint);
                        break;

                    case ProtocolMessageCode.inventory2:
                        handleInventory2(data, endpoint);
                        break;

                    case ProtocolMessageCode.sectorNodes:
                        handleSectorNodes(data, endpoint);
                        break;

                    case ProtocolMessageCode.nameRecord:
                        handleNameRecord(data, endpoint);
                        break;

                    case ProtocolMessageCode.keepAlivesChunk:
                        handleKeepAlivesChunk(data, endpoint);
                        break;

                    case ProtocolMessageCode.rejected:
                        handleRejected(data, endpoint);
                        break;

                    default:
                        Logging.warn("Unknown protocol message: {0}, from {1} ({2})", code, endpoint.getFullAddress(), endpoint.serverWalletAddress);
                        break;

                }
            }
            catch (Exception e)
            {
                Logging.error("Error parsing network message. Details: {0}", e);
            }

        }

        private static void handleTransactionData(byte[] data, RemoteEndpoint endpoint)
        {
            Transaction tx = new Transaction(data, true, true);

            if (endpoint.presenceAddress.type == 'M'
                || endpoint.presenceAddress.type == 'H'
                || endpoint.presenceAddress.type == 'R')
            {
                PendingTransactions.increaseReceivedCount(tx.id, endpoint.presence.wallet);
            }

            if (Node.tiv.receivedNewTransaction(tx))
            {
                if (!Program.commands.stressRunning)
                {
                    Console.WriteLine("Received new transaction {0}", tx.getTxIdString());
                }
            }
        }

        public static void handleKeepAlivesChunk(byte[] data, RemoteEndpoint endpoint)
        {
            using (MemoryStream m = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(m))
                {
                    int ka_count = (int)reader.ReadIxiVarUInt();

                    int max_ka_per_chunk = CoreConfig.maximumKeepAlivesPerChunk;
                    if (ka_count > max_ka_per_chunk)
                    {
                        ka_count = max_ka_per_chunk;
                    }

                    for (int i = 0; i < ka_count; i++)
                    {
                        if (m.Position == m.Length)
                        {
                            break;
                        }

                        int ka_len = (int)reader.ReadIxiVarUInt();
                        byte[] ka_bytes = reader.ReadBytes(ka_len);

                        handleKeepAlivePresence(ka_bytes, endpoint);
                    }
                }
            }
        }

        private static void handleUpdatePresence(byte[] data, RemoteEndpoint endpoint)
        {
            // Parse the data and update entries in the presence list
            Presence updatedPresence = PresenceList.updateFromBytes(data, 0);
        }

        private static void handleKeepAlivePresence(byte[] data, RemoteEndpoint endpoint)
        {
            byte[] hash = CryptoManager.lib.sha3_512sqTrunc(data);

            InventoryCache.Instance.setProcessedFlag(InventoryItemTypes.keepAlive, hash, true);

            Address address = null;
            long last_seen = 0;
            byte[] device_id = null;
            char node_type;
            bool updated = PresenceList.receiveKeepAlive(data, out address, out last_seen, out device_id, out node_type, endpoint);
        }


        static void handleNameRecord(byte[] data, RemoteEndpoint endpoint)
        {
            int offset = 0;

            var nameAndOffset = data.ReadIxiBytes(offset);
            offset += nameAndOffset.bytesRead;
            byte[] name = nameAndOffset.bytes;

            var recordCountAndOffset = data.GetIxiVarUInt(offset);
            offset += recordCountAndOffset.bytesRead;
            int recordCount = (int)recordCountAndOffset.num;

            for (int i = 0; i < recordCount; i++)
            {
                var recordAndOffset = data.ReadIxiBytes(offset);
                offset += recordAndOffset.bytesRead;
            }
        }


        static void handleSectorNodes(byte[] data, RemoteEndpoint endpoint)
        {
            int offset = 0;

            var prefixAndOffset = data.ReadIxiBytes(offset);
            offset += prefixAndOffset.bytesRead;
            byte[] prefix = prefixAndOffset.bytes;

            var nodeCountAndOffset = data.GetIxiVarUInt(offset);
            offset += nodeCountAndOffset.bytesRead;
            int nodeCount = (int)nodeCountAndOffset.num;

            for (int i = 0; i < nodeCount; i++)
            {
                var kaBytesAndOffset = data.ReadIxiBytes(offset);
                offset += kaBytesAndOffset.bytesRead;

                Presence p = PresenceList.updateFromBytes(kaBytesAndOffset.bytes, IxianHandler.getMinSignerPowDifficulty(IxianHandler.getLastBlockHeight() + 1, IxianHandler.getLastBlockVersion(), Clock.getNetworkTimestamp()));
                if (p != null)
                {
                    RelaySectors.Instance.addRelayNode(p.wallet);
                }
            }

            if (IxianHandler.isMyAddress(new Address(prefix)))
            {
                List<Peer> peers = new();
                var relays = RelaySectors.Instance.getSectorNodes(prefix, Config.maxRelaySectorNodesToRequest);
                foreach (var relay in relays)
                {
                    var p = PresenceList.getPresenceByAddress(relay);
                    if (p == null)
                    {
                        continue;
                    }
                    var pa = p.addresses.First();
                    peers.Add(new(pa.address, relay, pa.lastSeenTime, 0, 0, 0));

                    PeerStorage.addPeerToPeerList(pa.address, p.wallet, pa.lastSeenTime, 0, 0, 0);
                }
                Node.networkClientManagerStatic.setClientsToConnectTo(peers);
            }
        }

        static void handleRejected(byte[] data, RemoteEndpoint endpoint)
        {
            try
            {
                Rejected rej = new Rejected(data);
                switch (rej.code)
                {
                    case RejectedCode.TxInvalid:
                    case RejectedCode.TxInsufficientFee:
                    case RejectedCode.TxDust:
                        Logging.error("Transaction {0} was rejected with code: {1}", Crypto.hashToString(rej.data), rej.code);
                        Console.WriteLine("Transaction {0} was rejected with code: {1}", Crypto.hashToString(rej.data), rej.code);
                        break;

                    case RejectedCode.TxDuplicate:
                        Logging.warn("Transaction {0} already sent.", Crypto.hashToString(rej.data), rej.code);
                        // All good
                        PendingTransactions.increaseReceivedCount(rej.data, endpoint.serverWalletAddress);
                        break;

                    default:
                        Logging.error("Received 'rejected' message with unknown code {0} {1}", rej.code, Crypto.hashToString(rej.data));
                        break;
                }
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Exception occured while processing 'rejected' message with code {0} {1}", data[0], Crypto.hashToString(data)), e);
            }
        }


        static void handleInventory2(byte[] data, RemoteEndpoint endpoint)
        {
            using (MemoryStream m = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(m))
                {
                    ulong item_count = reader.ReadIxiVarUInt();
                    if (item_count > (ulong)CoreConfig.maxInventoryItems)
                    {
                        Logging.warn("Received {0} inventory items, max items is {1}", item_count, CoreConfig.maxInventoryItems);
                        item_count = (ulong)CoreConfig.maxInventoryItems;
                    }

                    ulong last_accepted_block_height = IxianHandler.getLastBlockHeight();

                    ulong network_block_height = IxianHandler.getHighestKnownNetworkBlockHeight();

                    Dictionary<ulong, List<InventoryItemSignature>> sig_lists = new Dictionary<ulong, List<InventoryItemSignature>>();
                    List<InventoryItemKeepAlive> ka_list = new List<InventoryItemKeepAlive>();
                    List<byte[]> tx_list = new List<byte[]>();
                    for (ulong i = 0; i < item_count; i++)
                    {
                        ulong len = reader.ReadIxiVarUInt();
                        byte[] item_bytes = reader.ReadBytes((int)len);
                        InventoryItem item = InventoryCache.decodeInventoryItem(item_bytes);
                        if (item.type == InventoryItemTypes.transaction)
                        {
                            PendingTransactions.increaseReceivedCount(item.hash, endpoint.presence.wallet);
                        }
                        PendingInventoryItem pii = InventoryCache.Instance.add(item, endpoint);

                        // First update endpoint blockheights
                        switch (item.type)
                        {
                            case InventoryItemTypes.block:
                                var iib = ((InventoryItemBlock)item);
                                if (iib.blockNum > endpoint.blockHeight)
                                {
                                    endpoint.blockHeight = iib.blockNum;
                                }
                                break;
                        }

                        if (!pii.processed && pii.lastRequested == 0)
                        {
                            // first time we're seeing this inventory item
                            switch (item.type)
                            {
                                case InventoryItemTypes.keepAlive:
                                    var iika = (InventoryItemKeepAlive)item;
                                    if (PresenceList.getPresenceByAddress(iika.address) != null)
                                    {
                                        ka_list.Add(iika);
                                        pii.lastRequested = Clock.getTimestamp();
                                    }
                                    else
                                    {
                                        InventoryCache.Instance.processInventoryItem(pii);
                                    }
                                    break;

                                case InventoryItemTypes.transaction:
                                    tx_list.Add(item.hash);
                                    pii.lastRequested = Clock.getTimestamp();
                                    break;

                                case InventoryItemTypes.block:
                                    var iib = ((InventoryItemBlock)item);
                                    if (iib.blockNum <= last_accepted_block_height)
                                    {
                                        InventoryCache.Instance.setProcessedFlag(iib.type, iib.hash, true);
                                        continue;
                                    }

                                    var netBlockNum = CoreProtocolMessage.determineHighestNetworkBlockNum();
                                    if (iib.blockNum > netBlockNum)
                                    {
                                        continue;
                                    }

                                    requestNextBlock(iib.blockNum, iib.hash, endpoint);
                                    break;

                                default:
                                    Logging.warn("Unhandled inventory item {0}", item.type);
                                    break;
                            }
                        }
                    }

                    CoreProtocolMessage.broadcastGetKeepAlives(ka_list, endpoint);

                    CoreProtocolMessage.broadcastGetTransactions(tx_list, 0, endpoint);
                }
            }
        }

        static void requestNextBlock(ulong blockNum, byte[] blockHash, RemoteEndpoint endpoint)
        {
            InventoryItemBlock iib = new InventoryItemBlock(blockHash, blockNum);
            PendingInventoryItem pii = InventoryCache.Instance.add(iib, endpoint);
            if (!pii.processed
                && pii.lastRequested == 0)
            {
                pii.lastRequested = Clock.getTimestamp();
                InventoryCache.Instance.processInventoryItem(pii);
            }
        }
    }
}
