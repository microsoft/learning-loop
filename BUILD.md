# Build Learning Loop

Building Learning Loop is the same for all supported .NET platforms.

## Prerequisites

- [.NET 8.0](https://dotnet.microsoft.com/download) (required)
- [CMake](https://cmake.org/download/)
- [Ninja Build](https://ninja-build.org/)
- C++ Compiler (clang++ or g++)

## Steps

1) Clone the repository

    ```sh
    git clone https://github.com/microsoft/learning-loop.git
    cd learning-loop/src
    ```

2) Configure

    ### Windows

      Replace \<TARGET-VCPKG-PATH\> with the path where you want to install VCPKG for building the reinforcement-learning port.

      ```sh
      cmake --preset=vs2017 -DVCPKG_ROOT="<TARGET-VCPKG-PATH>"
      ```

    ### Linux/MacOS

      Replace \<TARGET-VCPKG-PATH\> with the path where you want to install VCPKG for building the reinforcement-learning port.

      ```sh
      cmake --preset=ninja -DVCPKG_ROOT="<TARGET-VCPKG-PATH>"
      ```

3) Build

    Build one of the following targets.

    - `learning-loop`: builds projects in the learning-loop solution
    - `onlinetrainer`: build the OnlineTrainerExe project and publish to artifacts
    - `common`: builds the Common project and publish to artifacts
    - `nuget`: builds the Common project and creates a nuget package; the package will be written to artifacts/nuget
    - `docker`: builds the learning-loop docker image
    - `build-deploy`: builds the docker image and deploys to an Azure environment (requires az login --use-device-code prior to running)
    - `examples`: builds the ConsoleJoiner example and publishes to artifacts/examples/ConsoleJoiner

    Replace \<THE-TARGET\> from the above target list.

    ```sh
    cmake --build --preset=release --target=<THE-TARGET>
    ```

## Next Steps

- [Build the Docker image](DOCKER.md)
- [Deploy the OnlineTrainer](DEPLOY.md)