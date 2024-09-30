# Build Learning Loop

Building Learning Loop is the same for all supported .NET platforms.

# Prerequisites

- .NET 8.0 (required)
  - https://dotnet.microsoft.com/download

- CMake
  - https://cmake.org/download/

- pkg-config (Linux)
  - https://linux.die.net/man/1/pkg-config
  - Ubuntu/Debian install (`sudo apt-get install pkg-config`)

- Ninja build (required for building binaries)
  - https://ninja-build.org/

- C++ Compiler (clang++ or g++ for building binaries)

# Steps

Clone the repository
```sh
git clone https://github.com/microsoft/learning-loop.git
cd learning-loop
git submodule update --init --recursive
```

Build the assemblies. The build will invoke the Vowpal Wabbit binary parser build needed by the OnlineTrainer.
```sh
dotnet build ./learning-loop.sln -c Release
```

# Next Steps

- [Build the Docker image](DOCKER.md)
- [Deploy the OnlineTrainer](DEPLOY.md)