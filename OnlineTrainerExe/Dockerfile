FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
ENV DOTNET_ENVIRONMENT=Production
WORKDIR /app
COPY ThirdPartyNotices.txt .
COPY onlinetrainer.sh .
COPY bin/onlinetrainer .
RUN chmod 555 vw-bin/vw-*
ENV PATH="${PATH}:/app"
RUN apt-get update && apt-get install -y gdb zstd
RUN ln -s /usr/lib/x86_64-linux-gnu/`ls -rt /usr/lib/x86_64-linux-gnu | grep "^libzstd.so" | tail -n1` /usr/lib/x86_64-linux-gnu/libzstd.so
ENTRYPOINT ["/bin/bash", "onlinetrainer.sh"]
