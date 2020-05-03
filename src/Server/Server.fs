open System
open System.IO
open System.Text
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks.V2
open Giraffe
open Saturn
open Shared

open Elmish
open Elmish.Bridge
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.FileProviders

let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let publicPath = Path.GetFullPath "../Client/public"
let wcatBinariesPath = Path.Combine(publicPath, "clitool")

let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

let previewHistory = 60
printfn "Initialising previews queue"
let latestPreviews = Collections.Concurrent.ConcurrentQueue<Preview>()

/// Elmish init function with a channel for sending client messages
/// Returns a new state and commands
let init clientDispatch () =
    for preview in latestPreviews do
        clientDispatch <| PreviewMsg preview
    (), Cmd.none

/// Elmish update function with a channel for sending client messages
/// Returns a new state and commands
let update clientDispatch msg model =
    match msg with
    | ClearPreviews -> latestPreviews.Clear()
    model, Cmd.none

let hub =
    ServerHub<_, ServerMsg, _>()
        .RegisterClient(PreviewMsg)

/// Connect the Elmish functions to an endpoint for websocket connections
let bridge =
    Bridge.mkServer BridgeSocketEndpoint init update
    |> Bridge.withServerHub hub
    |> Bridge.run Giraffe.server


let broadcastPreview preview =
    hub.BroadcastClient (PreviewMsg preview)
    while latestPreviews.Count > previewHistory do
        latestPreviews.TryDequeue () |> ignore
    latestPreviews.Enqueue preview

let readBodyBytes (ctx:HttpContext) = task {
    use bodyStream = ctx.Request.Body
    use memoryStream = new MemoryStream()
    let! _ = bodyStream.CopyToAsync(memoryStream)
    return memoryStream.ToArray()
}

let getImageSrc (ctx:HttpContext) mediaType = task {
    let srcFormat = sprintf "data:%s;charset=utf-8;base64" mediaType
    let! bytes = readBodyBytes ctx
    let imgData = sprintf "%s,%s" srcFormat (Convert.ToBase64String bytes)
    return ImageSrc imgData
}

let getMarkdown (ctx:HttpContext) = task {
    let! body = ctx.ReadBodyFromRequestAsync()
    return Markdown body
}

let getPlainText (ctx:HttpContext) = task {
    let! body = ctx.ReadBodyFromRequestAsync()
    return PlainText body
}

let showthis next (ctx:HttpContext) = task {
    let hasFilename, filenameHeader = ctx.Request.Headers.TryGetValue "filename"
    let filename =
        if hasFilename && filenameHeader.Count >= 1
        then Some (filenameHeader.Item 0)
        else None

    let contentType = Net.Mime.ContentType ctx.Request.ContentType
    let now = DateTime.UtcNow

    printfn "Broadcasting %s as Loading" (Option.defaultValue "" filename)

    let previewLoading = {
        Filename = filename
        Time = now
        Content = LoadingPreviewContent
        Id = Guid.NewGuid ()
    }

    broadcastPreview previewLoading

    //Ideally would catch and broadcast errors if the request dies while processing body

    let! content =
        match contentType.MediaType with
        | "image/jpeg" | "image/png" | "image/svg+xml" ->
            getImageSrc ctx contentType.MediaType
        | "text/markdown" ->
            getMarkdown ctx
        | "text/plain" | "text/csv" ->
            getPlainText ctx
        | _ ->
            ctx.SetStatusCode 415
            Task.FromResult (ContentTypeNotImplemented ctx.Request.ContentType)


    printfn "Previewing %s as %A" (Option.defaultValue "" filename) (content.GetType())
    let preview = { previewLoading with Content = content }

    broadcastPreview preview

    return! json {| ContentType = sprintf "%A" contentType |} next ctx
}

let apiRouter = router {
    not_found_handler (setStatusCode 200 >=> text "api 404")
    post "/showthis" showthis
}

let webApp =
    choose [
        bridge
        router { forward "/api" apiRouter }
    ]

let serveWcatBinaries (app:IApplicationBuilder) = //seperate so as to serve unknown file types
    let options = StaticFileOptions(FileProvider = new PhysicalFileProvider(wcatBinariesPath))
    options.RequestPath <- PathString "/clitool"
    options.ServeUnknownFileTypes <- true
    app.UseStaticFiles options

let app = application {
    url ("http://0.0.0.0:" + port.ToString() + "/")
    use_router webApp
    memory_cache
    use_static publicPath
    use_json_serializer(Thoth.Json.Giraffe.ThothSerializer())
    app_config Giraffe.useWebSockets
    app_config serveWcatBinaries
    use_gzip
}

run app