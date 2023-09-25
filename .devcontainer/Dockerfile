ARG VARIANT="8.0"
FROM mcr.microsoft.com/vscode/devcontainers/dotnet:latest@sha256:6e5d9440418393a00b05d306bf45bbab97d4bb9771f2b5d52f5f2304e393cc2f

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
