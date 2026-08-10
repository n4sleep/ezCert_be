# ezCert API container for AWS App Runner.
#
# NOTE: the .NET base images (mcr.microsoft.com) and apk/apt package repos are
# unreachable behind the corporate proxy, so the app is published SELF-CONTAINED
# (linux-x64, InvariantGlobalization) on the host and this image only ships the
# publish output on a minimal glibc base from Docker Hub.
#
# Publish (host): dotnet publish src/EzCert.Api/EzCert.Api.csproj -c Release \
#   -r linux-x64 --self-contained true -p:InvariantGlobalization=true -o <out>
# Build:          docker build --build-arg PUBLISH_DIR=<out> -t ezcert-api .
FROM debian:bookworm-slim AS runtime
ARG PUBLISH_DIR=./publish
WORKDIR /app
COPY ${PUBLISH_DIR}/ .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["./EzCert.Api"]
