# Deploy a Learning Loop

The deploy folder contains bicep scripts for deploying a sample Loop. These scripts can be used to deploy a self-contained loop enviornment or can be inculded in a more customized configuration.

# Prerequisites

- Powershell
  - https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.4

- Azure CLI
  - https://learn.microsoft.com/en-us/cli/azure/install-azure-cli

- Docker Engine
  - https://docs.docker.com/engine/install/

# Sample Deployment Script

The deploy-sample.ps1 script sets up a resource group to deploy the Learning Loop image (see `get-help ./deploy/scripts/deploy-sample.ps1`). The scipt executes in three phases.

- Phase 1 (optional)
Phase 1 deploys a resource group environment where the Loop resources will be deployed. If using an Azure container repository, the repository will be created here. Since Managed Identity is used by the Loop to access storage and Event Hub resources, a managed identity will also be created. If these resources exist, this phase can be skipped using -skipSetupEnvironment.

- Phase 2 (optional)
Phase 2 pushes the specified tarred Docker image to either an Azure container registry or a Docker Hub repository. If the image is already in a repository, this phase can be skipped using -loadAndPushDockerImage $false.

- Phase 3 (optional)
Phase 3 deploys the Loop container, the storage account, and the Event Hub. This phase may be skipped if -noDeploy is specified. In this case, a `parameters.bicepparam` file will be generated with the specified parameters; this file can be used to deploy the Loop using az directly.

# Sample Deployment using an Azure ACR

The sample deployment will set up a resource group for your environment, create an Azure ACR, push the Docker image to the ACR, and deploy the Learning Loop with storage and Event Hubs.

Deploy the Docker image from your build or get the Docker image from the GitHub build (currently located [here](https://github.com/microsoft/learning-loop/actions/runs/11109130866/artifacts/1996359925))

## Sample Deploy (Linux)

```sh
cd deploy
sudo systemctl start docker.service
sudo pwsh
az login --use-device-code
./scripts/deploy-sample.sh -dockerImageTar ../learning-loop-ubuntu-latest.tar -imageRegistryCredType ManagedIdentity
```

## Sample Deploy (Windows / Linux - WSL)

1) Start Docker-Desktop
2) Start a Powershell terminal

```sh
cd deploy
az login --use-device-code
./scripts/deploy-sample.sh -dockerImageTar ../learning-loop-ubuntu-latest.tar -imageRegistryCredType ManagedIdentity
```

# Next Steps

- [Send events to the Learning Loop (run rl_sim_cpp)](RL_SIM.md)
