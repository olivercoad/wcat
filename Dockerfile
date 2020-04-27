FROM golang:alpine AS builder
RUN apk update && apk add --no-cache git
WORKDIR /src/wcat
COPY go.mod go.sum ./
COPY /src/wcat ./
RUN mkdir -p /wcat/bin
RUN env GOOS=linux GOARCH=amd64 go build -o /wcat/bin/wcat-linux-386
RUN env GOOS=linux GOARCH=amd64 go build -o /wcat/bin/wcat-linux-arm
RUN env GOOS=windows GOARCH=amd64 go build -o /wcat/bin/wcat-windows-386.exe
RUN env GOOS=darwin GOARCH=386 go build -o /wcat/bin/darwin-386.exe

FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-alpine
COPY --from=builder /wcat/bin /Client/public/clitool
COPY --from=builder /wcat/bin/wcat-linux-386 /bin/wcat
COPY /deploy /
WORKDIR /Server
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Server.dll" ]
