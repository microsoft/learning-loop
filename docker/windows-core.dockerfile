FROM mcr.microsoft.com/dotnet/runtime:8.0.11-windowsservercore-ltsc2022 AS base
RUN powershell -Command " \
    Invoke-WebRequest -Uri https://aka.ms/installazurecliwindows -OutFile AzureCLI.msi; \
    Start-Process msiexec.exe -ArgumentList '/i', 'AzureCLI.msi', '/quiet', '/qn', '/norestart' -NoNewWindow -Wait; \
    Remove-Item -Force AzureCLI.msi"

RUN az --version

ARG TRAINER_SRC_PATH=artifacts/OnlineTrainer
ARG RLSIM_SRC_PATH=artifacts/rl-sim
ARG IMAGE_ENVIRONMENT=Production
ENV DOTNET_ENVIRONMENT=${IMAGE_ENVIRONMENT}
WORKDIR /app
COPY ${TRAINER_SRC_PATH} .
COPY ${RLSIM_SRC_PATH} rl-sim
ENTRYPOINT ["powershell", "-NoLogo", "-File", "start-app.ps1"]