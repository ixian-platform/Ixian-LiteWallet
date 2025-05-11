using IXICore;
using IXICore.Meta;
using System;
using System.Linq;

namespace LW.Meta
{
    internal class LWTransactionInclusionCallbacks : TransactionInclusionCallbacks
    {
        public void receivedTransactionInclusionVerificationResponse(byte[] txid, bool verified)
        {
            string status = "NOT VERIFIED";
            if (verified)
            {
                status = "VERIFIED";
                PendingTransactions.remove(txid);
            }
            Console.WriteLine("Transaction {0} is {1}\n", Transaction.getTxIdString(txid), status);
        }

        public void receivedBlockHeader(Block block_header, bool verified)
        {
            foreach (Balance balance in IxianHandler.balances)
            {
                if (balance.blockChecksum != null && balance.blockChecksum.SequenceEqual(block_header.blockChecksum))
                {
                    balance.verified = true;
                }
            }

            if (block_header.blockNum >= IxianHandler.getHighestKnownNetworkBlockHeight())
            {
                IxianHandler.status = NodeStatus.ready;
                Node.setNetworkBlock(block_header.blockNum, block_header.blockChecksum, block_header.version);
            }
            Node.processPendingTransactions();
        }
    }
}
