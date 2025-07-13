FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0.302@sha256:3da7c4198dc77b50aeaf76f262ed0ac2ed324f87fba4b5b0f0bc0b4fbbf2ad93 AS build
ARG TARGETARCH

COPY . /source
WORKDIR /source

SHELL ["/bin/bash", "-o", "pipefail", "-c"]

# renovate: datasource=node-version depName=node
ENV NODE_VERSION=22

RUN apt-get update \
    && apt-get install gnupg --yes \
    && rm --recursive --force /var/lib/apt/lists/*

RUN mkdir --parents /etc/apt/keyrings \
    && curl --silent --show-error --location --retry 5 https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor --output /etc/apt/keyrings/nodesource.gpg \
    && echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_${NODE_VERSION}.x nodistro main" > /etc/apt/sources.list.d/nodesource.list \
    && apt-get update \
    && apt-get install nodejs --yes \
    && rm --recursive --force /var/lib/apt/lists/*

RUN curl --silent --show-error --location --retry 5 https://dot.net/v1/dotnet-install.asc --output dotnet-install.asc \
    && gpg --import dotnet-install.asc \
    && rm dotnet-install.asc

RUN curl --silent --show-error --location --retry 5 https://dot.net/v1/dotnet-install.sh --output dotnet-install.sh \
    && curl --silent --show-error --location --retry 5 https://dot.net/v1/dotnet-install.sig --output dotnet-install.sig \
    && gpg --verify dotnet-install.sig dotnet-install.sh \
    && chmod +x ./dotnet-install.sh \
    && ./dotnet-install.sh --jsonfile ./global.json --install-dir /usr/share/dotnet \
    && rm dotnet-install.sh \
    && rm dotnet-install.sig

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "src/Costellobot/Costellobot.csproj" --arch "${TARGETARCH}" --output /app

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0.7-noble-chiseled-extra@sha256:60a5140f33ce12a20574abe19040c4c82c2defc01386b646f95695056e58d76a AS final

WORKDIR /app
COPY --from=build /app .

EXPOSE 8080

ENTRYPOINT ["./Costellobot"]
