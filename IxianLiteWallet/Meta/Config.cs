using Fclp;
using IxianLiteWallet;
using IXICore.Meta;
using IXICore.Network;
using System;
using System.Collections.Generic;
using System.Text;

namespace LW.Meta
{
    class Config
    {
        public static string walletFile = "ixian.wal";
        public static bool onlyShowAddresses = false;

        public static readonly string version = "xlwc-0.9.4"; // LiteWallet version

        public static NetworkType networkType = NetworkType.main;

        public static byte[] checksumLock = null;

        public static int maxRelaySectorNodesToConnectTo = 3;

        public static int maxConnectedStreamingNodes = 6;
        private static string outputHelp()
        {
            Program.noStart = true;

            Console.WriteLine("Starts a new instance of IxianLiteWallet");
            Console.WriteLine("");
            Console.WriteLine(" IxianLiteWallet.exe [-h] [-v] [-w ixian.wal] [-t] [-n seed1.ixian.io:10234] [--checksumLock Ixian]");
            Console.WriteLine("");
            Console.WriteLine("    -h\t\t\t\t Displays this help");
            Console.WriteLine("    -v\t\t\t\t Displays version");
            Console.WriteLine("    -w\t\t\t\t Specify name of the wallet file");
            Console.WriteLine("    -t\t\t\t\t Starts node in testnet mode");
            Console.WriteLine("    -n\t\t\t\t Specify which seed node to use");
            Console.WriteLine("    --checksumLock\t\t Sets the checksum lock for seeding checksums - useful for custom networks.");
            Console.WriteLine("    --networkType\t\t mainnet, testnet or regtest.");

            return "";
        }
        private static string outputVersion()
        {
            Program.noStart = true;

            // Do nothing since version is the first thing displayed

            return "";
        }

        private static NetworkType parseNetworkTypeValue(string value)
        {
            NetworkType netType;
            value = value.ToLower();
            switch (value)
            {
                case "mainnet":
                    netType = NetworkType.main;
                    break;
                case "testnet":
                    netType = NetworkType.test;
                    break;
                case "regtest":
                    netType = NetworkType.reg;
                    break;
                default:
                    throw new Exception(string.Format("Unknown network type '{0}'. Possible values are 'mainnet', 'testnet', 'regtest'", value));
            }
            return netType;
        }


        public static void init(string[] args)
        {
            string seedNode = "";

            var cmd_parser = new FluentCommandLineParser();
            
            cmd_parser.SetupHelp("h", "help").Callback(text => outputHelp());
            cmd_parser.Setup<bool>('v', "version").Callback(text => outputVersion());
            cmd_parser.Setup<string>('w', "wallet").Callback(value => walletFile = value).Required();
            cmd_parser.Setup<bool>('t', "testnet").Callback(value => networkType = NetworkType.test).Required();
            cmd_parser.Setup<string>("networkType").Callback(value => networkType = parseNetworkTypeValue(value)).Required();
            cmd_parser.Setup<string>('n', "node").Callback(value => seedNode = value).Required();
            cmd_parser.Setup<string>("checksumLock").Callback(value => checksumLock = Encoding.UTF8.GetBytes(value)).Required();

            cmd_parser.Parse(args);

            if (seedNode != "")
            {
                switch (networkType)
                {
                    case NetworkType.main:
                        NetworkUtils.seedNodes = new List<string[]>
                            {
                                new string[2] { seedNode, null }
                            };
                        break;

                    case NetworkType.test:
                    case NetworkType.reg:
                        NetworkUtils.seedTestNetNodes = new List<string[]>
                            {
                                new string[2] { seedNode, null }
                            };
                        break;
                }
            }

            if (Program.noStart)
            {
                return;
            }
        }
    }
}
