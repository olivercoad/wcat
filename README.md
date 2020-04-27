# wcat

wcat is a webapp and cli tool which allows you to send images
to your browser while navigating a remote machine with ssh.

## Setup

```bash
docker run -d --name wcat -p "8085:8085" olicoad/wcat:latest
```

Download the cli tool.

```bash
wget http://localhost:8085/clitool/wcat-linux-386 -O ~/bin/wcat
chmod +x ~/bin/wcat
```

Make sure that `~/bin` or wherever you downloaded the file to is in `PATH`.

## Usage

It is recommended to use cli tool on the same machine that the server is running.
If that is a remote machine, you can use [SSH Port Forwarding](https://www.ssh.com/ssh/tunneling/example) to [access the preview in your browser](http://localhost:8085).

```bash
ssh -L 127.0.0.1:8085:127.0.0.1:8085 example.com
```

Run `wcat` just like you would use `cat` to preview files.

```bash
wcat example.jpg
```

# SAFE Stack

This project is build on the [SAFE Stack](https://safe-stack.github.io/). It was created using the dotnet [SAFE Template](https://safe-stack.github.io/docs/template-overview/). If you want to learn more about the template why not start with the [quick start](https://safe-stack.github.io/docs/quickstart/) guide?

## Install pre-requisites

You'll need to install the following pre-requisites in order to build SAFE applications

* The [.NET Core SDK](https://www.microsoft.com/net/download)
* [FAKE 5](https://fake.build/) installed as a [global tool](https://fake.build/fake-gettingstarted.html#Install-FAKE)
* The [Yarn](https://yarnpkg.com/lang/en/docs/install/) package manager (you can also use `npm` but the usage of `yarn` is encouraged).
* [Node LTS](https://nodejs.org/en/download/) installed for the front end components.
* If you're running on OSX or Linux, you'll also need to install [Mono](https://www.mono-project.com/docs/getting-started/install/).

## Work with the application

To concurrently run the server and the client components in watch mode use the following command:

```bash
fake build -t Run
```


You can use the included `Dockerfile` and `build.fsx` script to deploy your application as Docker container. You can find more regarding this topic in the [official template documentation](https://safe-stack.github.io/docs/template-docker/).


## SAFE Stack Documentation

You will find more documentation about the used F# components at the following places:

* [Saturn](https://saturnframework.org/docs/)
* [Fable](https://fable.io/docs/)
* [Elmish](https://elmish.github.io/elmish/)
* [Fulma](https://fulma.github.io/Fulma/)

If you want to know more about the full Azure Stack and all of it's components (including Azure) visit the official [SAFE documentation](https://safe-stack.github.io/docs/).

## Troubleshooting

* **fake not found** - If you fail to execute `fake` from command line after installing it as a global tool, you might need to add it to your `PATH` manually: (e.g. `export PATH="$HOME/.dotnet/tools:$PATH"` on unix) - [related GitHub issue](https://github.com/dotnet/cli/issues/9321)
