# Deploy a Learning Loop

The deploy folder contains Bicep scripts for deploying a sample Loop. These scripts can be used to deploy a self-contained loop environment or can be included in a more customized configuration.

## Prerequisites

Azure CLI, Docker Engine, and Git should be available on your system. Linux requires the jq command line tool. All of these prerequisites should be availble via your package manager; see the below links for the details of each.

### Linux

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Docker Engine](https://docs.docker.com/engine/install/)
- [Git](https://git-scm.com/downloads)
- [jq](https://jqlang.github.io/jq/download/)

### Windows

- [PowerShell](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.4)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Docker Engine](https://docs.docker.com/engine/install/)
- [Git](https://git-scm.com/downloads)

## Quick Start

Use the deploy-sample script to set up a new resource group environment and deploy a sample loop. A successful deployment requires the ability to create and manage the following resources in your Azure Subscription

- Create Resource Groups
- Ability to assign roles within the Resource Group
- Create an Azure Container Registry (ACR)
- Create Managed Identities
- Create Storage Accounts
- Create EventHubs
- Create KeyVaults
- Create Azure Container Groups and Container Instances
- Create Application Insights

**Note: At this time `Quick Start` requires the ability to download the Learning-Loop Docker artifact (see step 1)**

1) Download the learning-loop Docker image artifact from the [latest successful build](https://github.com/microsoft/learning-loop/actions?query=is%3Asuccess). Click on the link to the latest successful build, then click on the Artifacts link located in the header of the page (or scroll to the bottom of the page). Select the link labeled `docker-image-ubuntu-latest`. Note the file path of the downloaded artifact zip file for use in step 6.

    If you are unable to access the artifacts, you will need to [build the project](BUILD.md) and [the Docker image](DOCKER.md); come back to this step when the image is built.

    Note: In the future, the Docker image will be accessible from a public repository and this step will be obsolete (hang in there)

    <img src="images/learning-loop-artifacts.png" alt="Learning-Loop Artifacts" width="50%"/>

2) Clone the [learning-loop](https://github.com/microsoft/learning-loop) GitHub repository

    ```bash
    git clone https://github.com/microsoft/learning-loop.git
    cd learning-loop/deploy
    ```

3) Log in to Azure

    ```bash
    az login --use-device-code
    ```

4) Start the Docker Engine

    #### Linux (not Windows-WSL2)

    ```bash
    sudo systemctl start docker.service
    ```

    #### Windows (and Linux/WSL2)

    Launch the Docker Desktop application

6) Run the deploy-sample script substituting DOCKER-IMAGE-FILE-PATH with the Docker image artifact obtained from step 1.

    #### Linux Script

    ```bash
    chmod +x ./scripts/deploy-sample.sh
    ./scripts/deploy-sample.sh --dockerImageFile DOCKER-IMAGE-FILE-PATH
    ```

    #### Windows Script

    ```bash
    ./scripts/deploy-sample.ps1 -dockerImageFile DOCKER-IMAGE-FILE-PATH
    ```

## Next Steps

- [Send events to the Learning Loop (run rl_sim_cpp)](RL_SIM.md)

## Sample Deployment Script Details

The deploy-sample script sets up a resource group to deploy the Learning Loop image (see `get-help ./deploy/scripts/deploy-sample.ps1` or `./deploy/scripts/deploy-sample.sh --help`). The script executes in three phases.

- **Phase 1**: deploys a resource group environment where the Loop resources will be deployed. If using an Azure Container Registry, the repository will be created here. Since Managed Identity is used by the Loop to access storage and Event Hub resources, a Managed Identity will also be created.

- **Phase 2**: pushes the specified tar'd Docker image to either an Azure Container Registry or a Docker Hub repository.

- **Phase 3**: deploys the Loop container, the storage account, and the Event Hub.

A full deployment using the deploy-sample script will deploy all required resources and generate two files.

- `<loopName>.bicep` - contains the parameters for deploying the application environment, including the Learning Loop container, Event Hub, and the Storage Account. This parameters file is used with `main.bicep` and can be re-run independently of the script.
- `<loopName>.config.json` - contains the JSON config parameters for use with `rl_sim_cpp`

## Customize a deployment

See the Bicep scripts and [README](deploy/README.md) in the project deploy folder for deployment details.
