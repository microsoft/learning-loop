FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
ARG SRC_PATH=bin/onlinetrainer
ARG IMAGE_ENVIRONMENT=Production
ENV DOTNET_ENVIRONMENT=${IMAGE_ENVIRONMENT}
WORKDIR /app
COPY ${SRC_PATH} .
RUN chmod 555 vw-bin/vw-*
ENV PATH="${PATH}:/app"
RUN apt-get update && apt-get install -y gdb zstd
RUN ln -s /usr/lib/x86_64-linux-gnu/`ls -rt /usr/lib/x86_64-linux-gnu | grep "^libzstd.so" | tail -n1` /usr/lib/x86_64-linux-gnu/libzstd.so
ENTRYPOINT ["/bin/bash", "onlinetrainer.sh"]
