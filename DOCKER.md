# Build a Docker Image

This document describes how to build the Docker image using Docker commands. These steps assume you have built and published the binaries to the artifacts folder.

Note: the image can be created using the steps described in building the learning-loop using the `docker` CMake target.

## Prerequisites

- [PowerShell](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.4)
- [Docker Engine](https://docs.docker.com/engine/install/)

## Prepare the Binaries

Prepare the OnlineTrainer binaries for the Docker image.

```sh
cd src
dotnet publish --no-build OnlineTrainerExe/OnlineTrainerExe.csproj -o artifacts/OnlineTrainer
```

## Ensure the Docker Engine is Running

  Ensure the Docker Engine is running using the following command.

  ```sh
  docker info
  ```

If Docker is not running, start the Docker Engine on your platform.

- https://docs.docker.com/desktop/

  ### Windows/Mac

  Start Docker Desktop

  ### Ubuntu (Debian)

  Start the Docker daemon

  ```sh
  sudo systemctl start docker.service
  ```

## Windows WSL Notes

If running Docker under Windows WSL, be sure to turn on Docker Desktop for WSL 2.

- https://docs.docker.com/desktop/wsl/

## Build the Image

Building the image is the same for each platform.

Tag the image as needed; the example below uses learning-loop:latest.

```sh
docker build -t learning-loop:latest --build-arg SRC_PATH=./artifacts/OnlineTrainer -f ../docker/ubuntu.dockerfile .
```

**Note: a Windows Core image Docker file is available. Replace the Docker file with `../docker/windows-core.dockerfile`. Be sure to switch your Docker Engine to use Windows containers.

## Save the Image

Save the image to a tar file for direct deployment using the sample script. The Docker image is based on Ubuntu.

```sh
docker save learning-loop:latest -o learning-loop-ubuntu-latest.tar
```

## Next Steps

- [Deploy the Loop](DEPLOY.md)