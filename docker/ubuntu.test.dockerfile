FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
##################### REMOVE ME #####################
EXPOSE 10000 10001 10002 80 443
######################################################
ENV DOTNET_ENVIRONMENT=Production
WORKDIR /app
COPY ThirdPartyNotices.txt .
COPY onlinetrainer.sh .

##################### REMOVE ME #####################
COPY appsettings.user.json .
######################################################

COPY bin/onlinetrainer .
RUN chmod 555 vw-bin/vw-*
ENV PATH="${PATH}:/app"
RUN apt-get update && apt-get install -y gdb zstd
RUN ln -s /usr/lib/x86_64-linux-gnu/`ls -rt /usr/lib/x86_64-linux-gnu | grep "^libzstd.so" | tail -n1` /usr/lib/x86_64-linux-gnu/libzstd.so

##################### REMOVE ME #####################
RUN apt-get update && apt-get install -y \
    curl \
    libnss3-tools

RUN curl -JLO "https://dl.filippo.io/mkcert/latest?for=linux/amd64" && \
    chmod +x mkcert-v*-linux-amd64 && \
    mv mkcert-v*-linux-amd64 /usr/local/bin/mkcert

RUN mkcert -install && mkcert 127.0.0.1

RUN curl -fsSL https://deb.nodesource.com/setup_18.x | bash - && apt-get install -y nodejs

RUN npm install -g azurite
######################################################

ENTRYPOINT ["/bin/bash", "onlinetrainer.sh"]
