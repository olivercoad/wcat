FROM golang:alpine AS builder
RUN apk update && apk add --no-cache git
WORKDIR /src/wcat
COPY go.mod go.sum ./
COPY /src/wcat ./
RUN mkdir -p /wcat/bin
# RUN env CGO_ENABLED=0 GOOS=linux GOARCH=386 go build -o /wcat/bin/wcat-linux-386
RUN env CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -o /wcat/bin/wcat-linux-amd64
RUN env CGO_ENABLED=0 GOOS=linux GOARCH=arm go build -o /wcat/bin/wcat-linux-arm
RUN env CGO_ENABLED=0 GOOS=linux GOARCH=arm64 go build -o /wcat/bin/wcat-linux-arm64
RUN env CGO_ENABLED=0 GOOS=windows GOARCH=386 go build -o /wcat/bin/wcat-windows-386.exe
# RUN env CGO_ENABLED=0 GOOS=windows GOARCH=amd64 go build -o /wcat/bin/wcat-windows-amd64.exe
# RUN env CGO_ENABLED=0 GOOS=darwin GOARCH=386 go build -o /wcat/bin/wcat-darwin-38
RUN env CGO_ENABLED=0 GOOS=darwin GOARCH=amd64 go build -o /wcat/bin/wcat-darwin-amd64

FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-alpine
COPY --from=builder /wcat/bin /Client/public/clitool
COPY --from=builder /wcat/bin/wcat-linux-amd64 /bin/wcat
COPY /deploy /
WORKDIR /Server
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Server.dll" ]
