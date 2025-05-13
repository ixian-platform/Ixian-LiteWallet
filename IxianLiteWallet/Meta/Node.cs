using IXICore;
using IXICore.Inventory;
using IXICore.Meta;
using IXICore.Network;
using IXICore.RegNames;
using IXICore.Utils;
using LW.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using static IXICore.Transaction;

namespace LW.Meta
{
    class Node : IxianNode
    {
        private static Thread mainLoopThread;

        public static bool running = false;

        public static TransactionInclusion tiv = null;

        private bool generatedNewWallet = false;

        public static NetworkClientManagerStatic networkClientManagerStatic = null;

        public Node()
        {
            CoreConfig.simultaneousConnectedNeighbors = 6;
            IxianHandler.init(Config.version, this, Config.networkType, true, Config.checksumLock);
            init();
        }

        // Perform basic initialization of node
        private void init()
        {
            running = true;

            // Load or Generate the wallet
            if (!initWallet())
            {
                running = false;
                IxianLiteWallet.Program.running = false;
                return;
            }

            Logging.consoleOutput = false;

            Console.WriteLine("Connecting to Ixian network...");

            PeerStorage.init("");

            // Init TIV
            tiv = new TransactionInclusion(new LWTransactionInclusionCallbacks());

            mainLoopThread = new Thread(mainLoop);
            mainLoopThread.Name = "Main_Loop_Thread";
            mainLoopThread.Start();
        }


        static void mainLoop()
        {
            while (running)
            {
                try
                {
                    using (MemoryStream mw = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(mw))
                        {
                            writer.WriteIxiVarInt(IxianHandler.getWalletStorage().getPrimaryAddress().addressNoChecksum.Length);
                            writer.Write(IxianHandler.getWalletStorage().getPrimaryAddress().addressNoChecksum);
                            writer.WriteIxiVarInt(Config.maxRelaySectorNodesToRequest);
                            NetworkClientManager.broadcastData(['M', 'H', 'R'], ProtocolMessageCode.getSectorNodes, mw.ToArray(), null);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logging.error("Exception occured in mainLoop: " + e);
                }
                Thread.Sleep(30000);
            }
        }
        private bool initWallet()
        {
            WalletStorage walletStorage = new WalletStorage(Config.walletFile);

            Logging.flush();

            if (!walletStorage.walletExists())
            {
                ConsoleHelpers.displayBackupText();

                // Request a password
                string password = "";
                while (password.Length < 10)
                {
                    Logging.flush();
                    password = ConsoleHelpers.requestNewPassword("Enter a password for your new wallet: ");
                    if (IxianHandler.forceShutdown)
                    {
                        return false;
                    }
                }
                walletStorage.generateWallet(password);
                generatedNewWallet = true;
            }
            else
            {
                ConsoleHelpers.displayBackupText();

                bool success = false;
                while (!success)
                {

                    string password = "";
                    if (password.Length < 10)
                    {
                        Logging.flush();
                        Console.Write("Enter wallet password: ");
                        password = ConsoleHelpers.getPasswordInput();
                        Logging.consoleOutput = true;
                        Logging.verbosity = 31;
                    }
                    if (IxianHandler.forceShutdown)
                    {
                        return false;
                    }
                    if (walletStorage.readWallet(password))
                    {
                        success = true;
                    }
                }
            }


            if (walletStorage.getPrimaryPublicKey() == null)
            {
                return false;
            }

            // Wait for any pending log messages to be written
            Logging.flush();

            Console.WriteLine();
            Console.WriteLine("Your IXIAN addresses are: ");
            Console.ForegroundColor = ConsoleColor.Green;
            foreach (var entry in walletStorage.getMyAddressesBase58())
            {
                Console.WriteLine(entry);
            }
            Console.ResetColor();
            Console.WriteLine();

            if (Config.onlyShowAddresses)
            {
                return false;
            }

            if (walletStorage.viewingWallet)
            {
                Logging.error("Viewing-only wallet {0} cannot be used as the primary wallet.", walletStorage.getPrimaryAddress().ToString());
                return false;
            }

            IxianHandler.addWallet(walletStorage);

            // Prepare the balances list
            List<Address> address_list = IxianHandler.getWalletStorage().getMyAddresses();
            foreach (Address addr in address_list)
            {
                IxianHandler.balances.Add(new Balance(addr, 0));
            }

            return true;
        }

        static public void stop()
        {
            running = false;
            IxianHandler.forceShutdown = true;

            if (mainLoopThread != null)
            {
                mainLoopThread.Interrupt();
                mainLoopThread.Join();
                mainLoopThread = null;
            }

            // Stop TIV
            tiv.stop();

            // Stop the network queue
            NetworkQueue.stop();

            // Stop all network clients
            NetworkClientManager.stop();
            StreamClientManager.stop();
        }

        public void start()
        {
            PresenceList.init(IxianHandler.publicIP, 0, 'C', CoreConfig.clientKeepAliveInterval);

            // Start the network queue
            NetworkQueue.start();

            InventoryCache.init(new InventoryCacheClient(tiv));

            RelaySectors.init(CoreConfig.relaySectorLevels, null);

            // Start the network client manager
            networkClientManagerStatic = new NetworkClientManagerStatic(Config.maxRelaySectorNodesToConnectTo);

            NetworkClientManager.init(networkClientManagerStatic);
            NetworkClientManager.start(2);

            // Start the stream client manager
            StreamClientManager.start();

            // Start TIV
            if (generatedNewWallet || !File.Exists(Config.walletFile))
            {
                generatedNewWallet = false;
                tiv.start("");
            }
            else
            {
                tiv.start("", 0, null, false);
            }
        }

        static public void getBalance(byte[] address)
        {
            ProtocolMessage.setWaitFor(ProtocolMessageCode.balance2, address);

            // Return the balance for the matching address
            using (MemoryStream mw = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(mw))
                {
                    writer.WriteIxiVarInt(address.Length);
                    writer.Write(address);
                    NetworkClientManager.broadcastData(new char[] { 'R' }, ProtocolMessageCode.getBalance2, mw.ToArray(), null);
                }
            }
            ProtocolMessage.wait(30);
        }

        static public void generateNewAddress()
        {
            Address base_address = IxianHandler.getWalletStorage().getPrimaryAddress();
            Address new_address = IxianHandler.getWalletStorage().generateNewAddress(base_address, null);
            if (new_address != null)
            {
                IxianHandler.balances.Add(new Balance(new_address, 0));
                Console.WriteLine("New address generated: {0}", new_address.ToString());
            }
            else
            {
                Console.WriteLine("Error occurred while generating a new address");
            }
        }

        static public void sendTransaction(Address address, IxiNumber amount)
        {
            // TODO add support for sending funds from multiple addreses automatically based on remaining balance
            Balance address_balance = IxianHandler.balances.First();
            var from = address_balance.address;
            sendTransactionFrom(from, address, amount);
        }

        static public void sendTransactionFrom(Address fromAddress, Address toAddress, IxiNumber amount)
        {
            getBalance(fromAddress.addressNoChecksum);

            IxiNumber fee = ConsensusConfig.forceTransactionPrice;
            SortedDictionary<Address, ToEntry> to_list = new(new AddressComparer());
            Balance address_balance = IxianHandler.balances.FirstOrDefault(addr => addr.address.addressNoChecksum.SequenceEqual(fromAddress.addressNoChecksum));
            Address pubKey = new(IxianHandler.getWalletStorage().getPrimaryPublicKey());

            if (!IxianHandler.getWalletStorage().isMyAddress(fromAddress))
            {
                Console.WriteLine("From address is not my address.\n");
                return;
            }

            SortedDictionary<byte[], IxiNumber> from_list = new(new ByteArrayComparer())
            {
                { IxianHandler.getWalletStorage().getAddress(fromAddress).nonce, amount }
            };

            to_list.AddOrReplace(toAddress, new ToEntry(Transaction.getExpectedVersion(IxianHandler.getLastBlockVersion()), amount));

            List<Address> relayNodeAddresses = NetworkClientManager.getRandomConnectedClientAddresses(2);
            IxiNumber relayFee = 0;
            foreach (Address relayNodeAddress in relayNodeAddresses)
            {
                var tmpFee = fee > ConsensusConfig.transactionDustLimit ? fee : ConsensusConfig.transactionDustLimit;
                to_list.AddOrReplace(relayNodeAddress, new ToEntry(Transaction.getExpectedVersion(IxianHandler.getLastBlockVersion()), tmpFee));
                relayFee += tmpFee;
            }


            // Prepare transaction to calculate fee
            Transaction transaction = new((int)Transaction.Type.Normal, fee, to_list, from_list, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());

            relayFee = 0;
            foreach (Address relayNodeAddress in relayNodeAddresses)
            {
                var tmpFee = transaction.fee > ConsensusConfig.transactionDustLimit ? transaction.fee : ConsensusConfig.transactionDustLimit;
                to_list[relayNodeAddress].amount = tmpFee;
                relayFee += tmpFee;
            }

            byte[] first_address = from_list.Keys.First();
            from_list[first_address] = from_list[first_address] + relayFee + transaction.fee;
            IxiNumber wal_bal = IxianHandler.getWalletBalance(new Address(transaction.pubKey.addressNoChecksum, first_address));
            if (from_list[first_address] > wal_bal)
            {
                IxiNumber maxAmount = wal_bal - transaction.fee;

                if (maxAmount < 0)
                    maxAmount = 0;

                Console.WriteLine("Insufficient funds to cover amount and transaction fee.\nMaximum amount you can send is {0} IXI.\n", maxAmount);
                return;
            }
            // Prepare transaction with updated "from" amount to cover fee
            transaction = new((int)Transaction.Type.Normal, fee, to_list, from_list, pubKey, IxianHandler.getHighestKnownNetworkBlockHeight());

            // Send the transaction
            if (IxianHandler.addTransaction(transaction, relayNodeAddresses, true))
            {
                Console.WriteLine("Sending transaction, txid: {0}\n", transaction.getTxIdString());
            }
            else
            {
                Console.WriteLine("Could not send transaction\n");
            }
        }

        public override ulong getLastBlockHeight()
        {
            if(tiv.getLastBlockHeader() == null)
            {
                return 0;
            }
            return tiv.getLastBlockHeader().blockNum;
        }

        public override bool isAcceptingConnections()
        {
            return false;
        }

        public override ulong getHighestKnownNetworkBlockHeight()
        {
            ulong bh = getLastBlockHeight();
            ulong netBlockNum = CoreProtocolMessage.determineHighestNetworkBlockNum();
            if (bh < netBlockNum)
            {
                bh = netBlockNum;
            }

            return bh;
        }

        public override int getLastBlockVersion()
        {
            if (tiv.getLastBlockHeader() == null 
                || tiv.getLastBlockHeader().version < Block.maxVersion)
            {
                // TODO Omega force to v10 after upgrade
                return Block.maxVersion - 1;
            }
            return tiv.getLastBlockHeader().version;
        }

        public override bool addTransaction(Transaction tx, List<Address> relayNodeAddresses, bool force_broadcast)
        {
            foreach (var address in relayNodeAddresses)
            {
                NetworkClientManager.sendToClient(address, ProtocolMessageCode.transactionData2, tx.getBytes(true, true), null);
            }
            //CoreProtocolMessage.broadcastProtocolMessage(new char[] { 'R' }, ProtocolMessageCode.transactionData2, tx.getBytes(true, true), null);
            PendingTransactions.addPendingLocalTransaction(tx, relayNodeAddresses);
            return true;
        }

        public override Block getLastBlock()
        {
            return tiv.getLastBlockHeader();
        }

        public override Wallet getWallet(Address id)
        {
            foreach (Balance balance in IxianHandler.balances)
            {
                if (id.addressNoChecksum.SequenceEqual(balance.address.addressNoChecksum))
                    return new Wallet(id, balance.balance);
            }
            return new Wallet(id, 0);
        }

        public override IxiNumber getWalletBalance(Address id)
        {
            foreach (Balance balance in IxianHandler.balances)
            {
                if (id.addressNoChecksum.SequenceEqual(balance.address.addressNoChecksum))
                    return balance.balance;
            }
            return 0;
        }

        public override void shutdown()
        {
            IxianHandler.forceShutdown = true;
        }

        public override void parseProtocolMessage(ProtocolMessageCode code, byte[] data, RemoteEndpoint endpoint)
        {
            ProtocolMessage.parseProtocolMessage(code, data, endpoint);
        }

        public static void processPendingTransactions()
        {
            // TODO TODO improve to include failed transactions
            ulong last_block_height = IxianHandler.getLastBlockHeight();
            lock (PendingTransactions.pendingTransactions)
            {
                long cur_time = Clock.getTimestamp();
                List<PendingTransaction> tmp_pending_transactions = new(PendingTransactions.pendingTransactions);
                int idx = 0;
                foreach (var entry in tmp_pending_transactions)
                {
                    Transaction t = entry.transaction;
                    long tx_time = entry.addedTimestamp;

                    if (t.applied != 0)
                    {
                        PendingTransactions.pendingTransactions.RemoveAll(x => x.transaction.id.SequenceEqual(t.id));
                        continue;
                    }

                    // if transaction expired, remove it from pending transactions
                    if (last_block_height > ConsensusConfig.getRedactedWindowSize() && t.blockHeight < last_block_height - ConsensusConfig.getRedactedWindowSize())
                    {
                        Console.WriteLine("Error sending the transaction {0}", t.getTxIdString());
                        PendingTransactions.pendingTransactions.RemoveAll(x => x.transaction.id.SequenceEqual(t.id));
                        continue;
                    }

                    if (cur_time - tx_time > 40) // if the transaction is pending for over 40 seconds, resend
                    {
                        foreach (var address in entry.relayNodeAddresses)
                        {
                            NetworkClientManager.sendToClient(address, ProtocolMessageCode.transactionData2, t.getBytes(true, true), null);
                        }
                        entry.addedTimestamp = cur_time;
                    }

                    if (entry.confirmedNodeList.Count() > 1) // already received 3+ feedback
                    {
                        continue;
                    }

                    if (cur_time - tx_time > 20) // if the transaction is pending for over 20 seconds, send inquiry
                    {
                        CoreProtocolMessage.broadcastGetTransaction(t.id, 0);
                    }

                    idx++;
                }
            }
        }

        public override Block getBlockHeader(ulong blockNum)
        {
            return BlockHeaderStorage.getBlockHeader(blockNum);
        }

        public override IxiNumber getMinSignerPowDifficulty(ulong blockNum, int curBlockVersion, long curBlockTimestamp)
        {
            // TODO TODO implement this properly
            return ConsensusConfig.minBlockSignerPowDifficulty;
        }

        public override byte[] getBlockHash(ulong blockNum)
        {
            Block b = getBlockHeader(blockNum);
            if (b == null)
            {
                return null;
            }

            return b.blockChecksum;
        }

        public override RegisteredNameRecord getRegName(byte[] name, bool useAbsoluteId)
        {
            throw new NotImplementedException();
        }
    }
}
