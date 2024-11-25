FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
RUN apt-get update && apt-get install -y azure-cli && apt-get clean
ARG TRAINER_SRC_PATH=artifacts/OnlineTrainer
ARG RLSIM_SRC_PATH=artifacts/rl-sim
ARG IMAGE_ENVIRONMENT=Production
ENV DOTNET_ENVIRONMENT=${IMAGE_ENVIRONMENT}
WORKDIR /app
COPY ${TRAINER_SRC_PATH} .
COPY ${RLSIM_SRC_PATH} ./rl-sim
RUN chmod 555 onlinetrainer.sh
RUN chmod 555 vw-bin/vw-*
RUN chmod 555 rl_sim.sh
RUN chmod 555 rl-sim/rl_sim-*
ENV PATH="${PATH}:/app"
RUN apt-get update && apt-get install -y gdb zstd
RUN ln -s /usr/lib/x86_64-linux-gnu/`ls -rt /usr/lib/x86_64-linux-gnu | grep "^libzstd.so" | tail -n1` /usr/lib/x86_64-linux-gnu/libzstd.so
ENTRYPOINT ["/bin/bash", "start-app.sh"]
