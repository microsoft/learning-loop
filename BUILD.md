# Build Learning Loop

Building Learning Loop is the same for all supported .NET platforms.

## Prerequisites

- [.NET 8.0](https://dotnet.microsoft.com/download) (required)
- [CMake](https://cmake.org/download/)
- [pkg-config](https://linux.die.net/man/1/pkg-config) (for Linux)
  - For Ubuntu/Debian (`sudo apt-get install pkg-config`)
- [Ninja Build](https://ninja-build.org/)
- C++ Compiler (clang++ or g++)

## Steps

Clone the repository

```sh
git clone https://github.com/microsoft/learning-loop.git
cd learning-loop/src
```

TODO: CMake instructions

TODO: instructions on the command line (windows/linux)

Build the assemblies. The build will invoke the Vowpal Wabbit binary parser build needed by the OnlineTrainer.

```sh
mkdir artifacts
dotnet build ./learning-loop.sln -c Release
```

## Next Steps

- [Build the Docker image](DOCKER.md)
- [Deploy the OnlineTrainer](DEPLOY.md)