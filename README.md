***

# Ixian Lite Wallet has been deprecated and will not be updated. Please use QuIXI as a replacement:
* [QuIXI](https://github.com/ixian-platform/QuIXI): Quick integration toolkit for Ixian. Connects apps, IoT devices, AI Agents, and services to the Ixian Platform via REST APIs and message queues (MQTT/RabbitMQ). Provides developers with a simple bridge into secure Ixian communication and Client Discovery.

***

# Ixian Lite Wallet
Ixian Lite Wallet is a lightweight console executable which allows using an Ixian wallet without running a full Ixian-DLT Node.

## Running
Download the latest binary release or you can compile the code yourself.

### Windows
Double-click on the IxianLiteWallet.exe to start the wallet.

### Linux
Install the dotnet runtime following the guide for your Linux distribution. Go to the [Microsoft dotnet page](https://learn.microsoft.com/en-us/dotnet/core/install/linux) and follow the steps for your Linux distribution.

Open a terminal and navigate to the IxianLiteWallet folder, then type
```
./IxianLiteWallet
```
to start the wallet.

### macOS
Install the dotnet runtime following the official guide. Go to the [Microsoft dotnet page](https://learn.microsoft.com/en-us/dotnet/core/install/macos) and follow the steps.

Open a terminal and navigate to the IxianLiteWallet folder, then type
```
./IxianLiteWallet
```
to start the wallet.


## Building
### Windows
Visual Studio 2022 (Community Edition is fine) with .NET Desktop Development Workload and Desktop Development with C++ Workload

Several NuGetPackages are downloaded automatically during the build process.

### Linux
Install the dotnet SDK following the guide for your Linux distribution. Go to the [Microsoft dotnet page](https://learn.microsoft.com/en-us/dotnet/core/install/linux) and follow the steps for your Linux distribution.

For Debian based distributions such as Ubuntu, type
```
sudo apt install gcc git make unzip
```
or if you have a Redhat based distribution, type
```
sudo yum install gcc git make unzip
```

Next you'll need to build the IxianLiteWallet solution. You can do this by typing the following commands in the terminal:
```
git clone https://github.com/ProjectIxian/Ixian-Core.git
git clone https://github.com/ProjectIxian/Ixian-LiteWallet.git
cd Ixian-LiteWallet/IxianLiteWallet
sh rebuild.sh
```
The IxianLiteWallet will be compiled and placed in the IxianLiteWallet/bin/Release/ folder.

### macOS
Install the dotnet SDK following the guide for your Linux distribution. Go to the [Microsoft dotnet page](https://learn.microsoft.com/en-us/dotnet/core/install/macos) and follow the steps.

```
brew install gcc git make unzip
```

Next you'll need to build the IxianLiteWallet solution. You can do this by typing the following commands in the terminal:
```
git clone https://github.com/ProjectIxian/Ixian-Core.git
git clone https://github.com/ProjectIxian/Ixian-LiteWallet.git
cd Ixian-LiteWallet/IxianLiteWallet
sh rebuild.sh
```
The IxianLiteWallet will be compiled and placed in the IxianLiteWallet/bin/Release/ folder.


## Development branches

There are two main development branches:
* **master**: This branch is used to build the binaries for the latest stable release of Ixian Lite Wallet. It should change slowly and be quite well-tested. This is also the default branch for anyone who wishes to build their Ixian software from source.
* **development**: This is the main development branch. The branch might not always be kept bug-free, if an extensive new feature is being worked on. If you are simply looking to build a current binary yourself, please use one of the release tags which will be associated with the master branch.

## Documentation

You can find more documentation on how to build, APIs and other documents on [Ixian Documentation Pages](https://docs.ixian.io).

## Get in touch / Contributing

If you feel like you can contribute to the project, or have questions or comments, you can get in touch with the team through Discord: https://discord.gg/pdJNVhv

## Pull requests

If you would like to send an improvement or bugfix to this repository, but without permanently joining the team, follow these approximate steps:

1. Fork this repository
2. Create a branch (preferably with a name that describes the change)
3. Create commits (the commit messages should contain some information on what and why was changed)
4. Create a pull request to this repository for review and inclusion.

## About Ixian

Ixian DLT is a revolutionary blockchain that brings several innovative advantages, such as processing a high volume of micro-transactions quickly while consuming a low amount of processing power, disk space and energy.

**Homepage**: https://www.ixian.io

**Discord**: https://discord.gg/pdJNVhv

**Bitcointalk**: https://bitcointalk.org/index.php?topic=4631942.0

**Documentation**: https://docs.ixian.io

**GitHub**: https://www.github.com/ProjectIxian
