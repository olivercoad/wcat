# wcat - web cat

wcat is a webapp and cli tool which allows you to send *small* files
to your browser to be previewed, such as images or pdfs, while navigating a remote machine with ssh.

It is also possible to drop a file in the browser and download it from the terminal.

It's intended to help bridge the gap between cli and gui by reducing the need to open new terminals just to rsync small files back and forth.

## Setup the server

The easiest way to run the server is using docker:

```bash
docker run -d --name wcat -p "8085:8085" olicoad/wcat:latest
```

## Setup the cli tool

Download the pre-compiled cli tool from the server.
For example, on linux:

```bash
wget http://localhost:8085/clitool/wcat-linux-amd64 -O ~/bin/wcat
chmod +x ~/bin/wcat
```

Make sure that `~/bin` or wherever you downloaded the file to is in `PATH`.

## Usage

Run `wcat` just like you would use `cat` to preview files.

```bash
wcat example.jpg
```

Just like cat, if no files are specified, wcat listens on stdin

```bash
echo "hello world" | wcat
```

To send a file back from the browser, click "Show Dropzone" and drop a file in. Then download the latest dropped file using wcat.

```bash
wcat download # default output-document is ./[filename]
```

### Usage on a remote machine
It is recommended to use cli tool on the same machine that the server is running.
You can use [SSH Port Forwarding](https://www.ssh.com/ssh/tunneling/example) to [access the preview in your browser](http://localhost:8085).

```bash
ssh -L 127.0.0.1:8085:127.0.0.1:8085 example.com
```

Alternatively, if you don't have docker on the remote machine,
you can run the server on your local machine and reverse port forward
so that the cli tool on the remote machine can access the server.

```bash
ssh -R 127.0.0.1:8085:127.0.0.1:8085 example.com
```

### Libraries
The [pywcat](src/Python/README.md) package makes it easy to send previews to wcat from python.

### Example usage

Use [guff](https://github.com/silentbicycle/guff) in svg mode and pipe output into wcat
```bash
wc -l *.c | grep -v total | sort -nr | awk '{print($1)}' | ./guff -s -m line -r | wcat
```

# Development

This project is build on the [SAFE Stack](https://safe-stack.github.io/). It was created using the dotnet [SAFE Template](https://safe-stack.github.io/docs/template-overview/). If you want to learn more about the template why not start with the [quick start](https://safe-stack.github.io/docs/quickstart/) guide?

The cli tool is build with go.

## Install pre-requisites
**Only needed for development**

You'll need to install the following pre-requisites in order to build SAFE applications

* The [.NET Core 3.1 SDK](https://www.microsoft.com/net/download)
* [FAKE 5](https://fake.build/) installed as a [global tool](https://fake.build/fake-gettingstarted.html#Install-FAKE)
* The [Yarn](https://yarnpkg.com/lang/en/docs/install/) package manager (you can also use `npm` but the usage of `yarn` is encouraged).
* [Node LTS](https://nodejs.org/en/download/) installed for the front end components.
* If you're running on OSX or Linux, you'll also need to install [Mono](https://www.mono-project.com/docs/getting-started/install/).

You'll also need to install [go](https://golang.org/) in order to build the cli tool.

## Work with the application

Fake is used to concurrently watch all components of wcat.
It will concurrently run the server and frontend in watch mode and `go install` the cli tool whenever there are changes:

```bash
fake build -t Run
```

## Docker

You can use the included `Dockerfile` to build everything using docker.

```bash
docker build -t wcat .
```

## SAFE Stack Documentation

You will find more documentation about the used F# components at the following places:

* [Saturn](https://saturnframework.org/explanations/overview.html)
* [Fable](https://fable.io/docs/)
* [Elmish](https://elmish.github.io/elmish/)
* [Fulma](https://fulma.github.io/Fulma/)

If you want to know more about the full Azure Stack and all of it's components (including Azure) visit the official [SAFE documentation](https://safe-stack.github.io/docs/).

## Troubleshooting

* **fake not found** - If you fail to execute `fake` from command line after installing it as a global tool, you might need to add it to your `PATH` manually: (e.g. `export PATH="$HOME/.dotnet/tools:$PATH"` on unix) - [related GitHub issue](https://github.com/dotnet/cli/issues/9321)

# Try it with Gitpod
[![Gitpod ready-to-code](https://img.shields.io/badge/Gitpod-ready--to--code-blue?logo=gitpod)](https://gitpod.io/#https://github.com/olivercoad/wcat)

It should automatically install all dependencies and run `dotnet fake build -t run`.

This is the full developer environment including hot reloading client, live reloading server,
and automatically installing the cli tool on code changes.

Just wait a couple minutes for everything to compile and run, open a terminal and try wcat.

```
wcat README.md
```
