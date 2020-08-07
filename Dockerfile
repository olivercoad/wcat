FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as fakebuild


# Add keys and sources lists
RUN curl -sL https://deb.nodesource.com/setup_14.x | bash
RUN curl -sS https://dl.yarnpkg.com/debian/pubkey.gpg | apt-key add -
RUN echo "deb https://dl.yarnpkg.com/debian/ stable main" \
    | tee /etc/apt/sources.list.d/yarn.list

# Install node, yarn
RUN apt-get update && apt-get install -y nodejs yarn \
        # Clean up
        && apt-get autoremove -y \
        && apt-get clean -y \
        && rm -rf /var/lib/apt/lists/*

WORKDIR /workspace
COPY .config .config
RUN dotnet tool restore
COPY paket.dependencies paket.lock ./
RUN dotnet paket restore

COPY build.fsx yarn.lock RELEASE_NOTES.md ./
RUN dotnet fake build -t InstallClient
COPY . .
RUN dotnet fake build -t Bundle


FROM golang:alpine AS gobuilder
RUN apk update && apk add --no-cache git
WORKDIR /src/wcat
COPY go.mod go.sum ./
COPY /src/wcat ./
COPY --from=fakebuild /workspace/src/wcat/version.go .
RUN echo "Listing files"; ls -lAh
RUN mkdir -p /wcat/bin
# RUN env CGO_ENABLED=0 GOOS=linux GOARCH=386 go build -o /wcat/bin/wcat-linux-386
RUN env CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -o /wcat/bin/wcat-linux-amd64
RUN env CGO_ENABLED=0 GOOS=linux GOARCH=arm go build -o /wcat/bin/wcat-linux-arm
RUN env CGO_ENABLED=0 GOOS=linux GOARCH=arm64 go build -o /wcat/bin/wcat-linux-arm64
RUN env CGO_ENABLED=0 GOOS=windows GOARCH=386 go build -o /wcat/bin/wcat-windows-386.exe
# RUN env CGO_ENABLED=0 GOOS=windows GOARCH=amd64 go build -o /wcat/bin/wcat-windows-amd64.exe
# RUN env CGO_ENABLED=0 GOOS=darwin GOARCH=386 go build -o /wcat/bin/wcat-darwin-38
RUN env CGO_ENABLED=0 GOOS=darwin GOARCH=amd64 go build -o /wcat/bin/wcat-darwin-amd64


FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-alpine as server
COPY --from=gobuilder /wcat/bin /Client/public/clitool
COPY --from=gobuilder /wcat/bin/wcat-linux-amd64 /bin/wcat
COPY --from=fakebuild /workspace/deploy /
WORKDIR /Server
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Server.dll" ]