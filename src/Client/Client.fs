module Client

open Elmish
open Elmish.React
open Elmish.Bridge
open Fable.Core
open Browser
open Fable.FontAwesome
open Fable.FontAwesome.Free
open Fable.React
open Fable.React.Props
open Fetch.Types
open Thoth.Fetch
open Fulma
open Thoth.Json

open ReactDropzoneUploader

open Shared
open Fable.Core.JsInterop

// [<ImportAll("moment")>]
// let moment = jsNative

[<Import("*", from="moment")>]
module moment =
    [<Emit("moment($0).from($1)")>]
    let strFrom (datetime:string, fromdatetime:string) : string = jsNative

    [<Emit("moment($0).format('l HH:mm:ss Z')")>]
    let longFormat (datetime:string) : string = jsNative

[<Import("*", from="markdown-it-checkbox")>]
module markdownitcheckbox =
    let thing = 5

[<Import("*", from="markdown-it")>]
module markdownit =

    [<Emit("new markdownit('commonmark').use(markdownitcheckbox).render($0)")>]
    let render (markdown:string) : string = jsNative

let momentFromPast (datetime:System.DateTime) (fromdatetime:System.DateTime) =
    // datetime should always be in past
    let toDateTime = if datetime < fromdatetime then datetime else fromdatetime
    moment.strFrom (toDateTime.ToString("o"), fromdatetime.ToString("o"))

let momentLongFormat (datetime:System.DateTime) =
    moment.longFormat (datetime.ToString("o"))

[<Emit("window.scrollTo(0, 0)")>]
let scrollToTop _ : unit = jsNative

[<Emit("window.scrollTo(0, document.body.scrollHeight)")>]
let scrollToBottom _ : unit = jsNative

type IUtilities =
    abstract OpenInNewTab : uri:string * title:string -> unit

[<ImportAll("./public/Utils.js")>]
let utilities:IUtilities = jsNative

type Model = {
    CurrentTime: System.DateTime
    Previews: Map<System.Guid, Preview>
    ShowDropzone: bool
}

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events

type Msg =
    | Remote of ClientMsg
    | Tick of System.DateTime
    | ClearPreviews
    | ToggleDropzone

let timer initial = // used to update "2 minutes ago" momentjs message
    let sub dispatch =
        window.setInterval(fun _ ->
            dispatch (Tick System.DateTime.Now)
        , 60000) |> ignore //tick once every minute
    Cmd.ofSub sub

// defines the initial state and initial command (= side-effect) of the application
let init () : Model * Cmd<Msg> =
    { Previews = Map.empty; CurrentTime = System.DateTime.Now; ShowDropzone = false }, Cmd.none

let addPreview model preview currentTime =
    let previews =
        match preview.Content, model.Previews.ContainsKey preview.Id with
        //if client disconnects and reconnects with server, it's possible to get messages repeated.
        | LoadingPreviewContent, true -> model.Previews //never replace already loaded preview with loading preview
        | _ -> model.Previews.Add(preview.Id, preview)
    let maxCount = 100
    let cutBy = 10
    let truncated =
        if previews.Count <= maxCount
        then
            previews
        else
            previews
            |> Map.toList
            |> List.sortByDescending (fun (_, p) -> p.Time)
            |> List.take (maxCount - cutBy)
            |> Map.ofList

    { model with Previews = truncated; CurrentTime = currentTime }

// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match msg with
    | Remote(PreviewMsg preview) ->
        addPreview currentModel preview System.DateTime.Now, Cmd.none
    | Remote(ClearClientPreviews) ->
        { currentModel with Previews = Map.empty }, Cmd.none
    | ClearPreviews ->
        { currentModel with Previews = Map.empty }, Cmd.bridgeSend ServerMsg.ClearPreviews
    | Tick now ->
        { currentModel with CurrentTime = now }, Cmd.none
    | ToggleDropzone ->
        { currentModel with ShowDropzone = not currentModel.ShowDropzone }, Cmd.none

/// Creates a pre with class box
let preBox options children = pre (upcast ClassName "box"::options) children

/// Places the element on the right in the element on the left with no options
let inline (^>) a (b:ReactElement) : ReactElement = a [ ] [ b ]
let inline (^>>) a (b:ReactElement) : ReactElement = a [ b ]
/// Places the string on the right in the element on the left with no options
let inline (^>&) a (b:string) : ReactElement = a [ ] [ str b ]
let inline (^>>&) a (b:string) : ReactElement = a [ str b ]

let withLineBreaks (content:string) =
    content.Replace("\r", "\n")

let openInNewTabBtn src title text =
    let winopen src _ =
        // window.``open``(src) |> ignore
        utilities.OpenInNewTab (src, Option.defaultValue "wcat preview" title)
        ()

    // let src = "https://checkface.ml"

    Column.column [ ] [
        Button.button
            [ Button.IsLink; Button.OnClick (winopen src) ]
            [ str text ]
    ]

let downloadButton filename (content: byte array) =
    let blob = Blob.Create [| box content |]
    let href = URL.createObjectURL blob
    Column.column [ ] [
        Button.a
            [ Button.IsLink; Button.Props [ Download filename; Href href ] ]
            [ str ("Download file") ]
    ]

let showPreview currentTime preview =
    Column.column
        [
            Column.Width (Screen.All, Column.IsFull)
            Column.CustomClass "preview-item"
            Column.Props [ Key (string preview.Id) ]
        ]
        [
            match preview.Content with
            | ImageSrc src ->
                Box.box' ^> img [ Src src ]

            | PlainText content ->
                preBox ^>& (withLineBreaks content)

            | Markdown content ->
                Box.box' ^> Content.content ^> div [ DangerouslySetInnerHTML { __html = (markdownit.render content) } ] [ ]

            | AudioSrc src ->
                audio [ Src src; Controls true; ] [ p ^>& (Option.defaultValue "Audio" preview.Filename) ]

            | IframeSrc src ->
                iframe [ Src src ] [ ]
                openInNewTabBtn src preview.Filename "Open in new tab"

            | ContentTypeNotImplemented (fileExtension, contentType, content) ->
                Message.message [ Message.Color IsDanger ]
                    [
                        let ext =
                            match fileExtension with
                            | Some fext -> sprintf " (%s)" fext
                            | None -> ""
                        Message.header ^>& "Content type not supported for preview"
                        Message.body [ ] [
                            p ^> b ^>& contentType + ext
                            p [ ] [
                                a [ Href (sprintf "%s/issues" projectGithubLink ) ] [ str "Open an issue" ]
                                str " on github if you would like this content type to be supported"
                            ]
                            p [ ] [
                                str "Use "
                                code [ ] [ str "--justfile" ]
                                str " if you don't want a preview"
                            ]
                        ]
                    ]
                downloadButton preview.Filename content

            | JustFile content ->
                div [ ] [ p [ ] [ str "Just the file (no preview)"] ]
                downloadButton preview.Filename content

            | LoadingPreviewContent ->
                Icon.icon ^> Fa.i [ Fa.Solid.Spinner; Fa.Spin ] [ ]

            | RequestBodyTooLarge ->
                Message.message [ Message.Color IsDanger ]
                    [
                        Message.header ^>& "The file is too large to preview"
                        Message.body [ ] [
                            p ^>& sprintf "The maximum file size for preview is %s MB" ((MaxBodySize/1000_000)?toLocaleString())
                        ]
                    ]

            p [ ] [ str (Option.defaultValue "" preview.Filename) ]
            Columns.columns [ Columns.CustomClass "preview-info" ] [
                Column.column [ ] [
                    p [ ] [ str (momentFromPast preview.Time currentTime)]
                ]
                Column.column [ Column.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Right) ] ] [
                    p [ ] [ str (momentLongFormat preview.Time) ]
                ]
            ]
            hr [ ]
        ]

let showAllImages model =
    if model.Previews.IsEmpty
    then
        div [ Hidden true ] [ ]
    else
        let previews =
            model.Previews
            |> Map.toList
            |> List.unzip
            |> snd
            |> List.sortByDescending (fun p -> p.Time)
            |> List.map (showPreview model.CurrentTime)

        Column.column
            [ Column.Width (Screen.All, Column.IsFull) ]
            previews

let helpMessage =
    Message.message [ Message.Color IsInfo ] [
        Message.header [ ] [ str "Use the wcat cli tool to send files for preview" ]
        Message.body [ ] [
            p ^>& "Download the wcat cli tool"
            p ^>& "Remember to make it executable (chmod +x wcat) and place it somewhere in PATH if necessary"
            br [ ]
            p ^>& "Then use it just like you would use cat: wcat example.jpg"
            br [ ]
            p ^>& "Instead of outputting the content inline, it will send the file to the server."
            p ^>& "The server will broadcast the content to be displayed here in the browser."
        ]
    ]

let downloadButtons =
    let buttons =
        [ "Windows", "wcat-windows-386.exe", "wcat.exe";
          "Darwin64", "wcat-darwin-amd64", "wcat";
          "Linux64", "wcat-linux-amd64", "wcat" ;
          "Arm", "wcat-linux-arm", "wcat";
          "Arm64", "wcat-linux-arm75", "wcat";
          "Bash", "wcat.sh", "wcat.sh" ]
        |> List.map (fun (os, file, downloadfile) ->
            let href = (sprintf "/clitool/%s" file)
            Button.a
                [ Button.IsLink; Button.Props [ Download downloadfile; Href href ] ]
                [ str os ])

    Button.list [ Button.List.IsCentered; Button.List.AreMedium; Button.List.Modifiers [  ] ] buttons

let navbar dispatch =
    let navItem onclick children =
        Navbar.Item.a
            [ Navbar.Item.Props [ OnClick onclick ] ]
            children

    Navbar.navbar [ Navbar.IsFixedBottom; ]
    ^>> Navbar.menu [ ] [
        Navbar.Start.div [ ] [
            Navbar.Item.a [ Navbar.Item.Props [ Href projectGithubLink ] ] ^>>& Version.app
        ]
        Navbar.End.div [ ] [
            navItem (fun _ -> dispatch ToggleDropzone) ^>>& "Show dropzone"
            navItem (fun _ -> dispatch ClearPreviews) ^>>& "Clear Previews"
            navItem scrollToBottom [ p ^>& "Go to Bottom"; Icon.icon ^> Fa.i [ Fa.Solid.ChevronDown ] [ ] ]
            navItem scrollToTop [ p ^>& "Go to Top"; Icon.icon ^> Fa.i [ Fa.Solid.ChevronUp ] [ ] ]
        ]
    ]

let showDropzone model dispatch =
    Modal.modal [ Modal.IsActive model.ShowDropzone ] [
        Modal.background [ Props [ OnClick (fun _ -> dispatch ToggleDropzone) ] ] [ ]
        Modal.Card.card [ ] [
            Modal.Card.head [ ] [
                Modal.Card.title [ ] [ str "Send files back to the terminal" ]
                Delete.delete [ Delete.OnClick (fun _ -> dispatch ToggleDropzone) ] [ ]
            ]
            Modal.Card.body [ ] [
                dropzone [
                    MaxFiles 1
                    Multiple false
                    MaxSizeBytes MaxBodySize
                    InputContent (fun _ _ -> str "Drop a File")
                    OnChangeStatus (
                        fun file status allFiles ->
                            printfn "File is: %s" (string file)
                            printfn "Status is: %s" (string status)
                            printfn "AllFiles is: %s" (string allFiles)
                            match status with
                            | Done ->
                                file.remove()
                                None
                            | _ ->
                                None
                    )

                    GetUploadParams (fun file -> promise {
                        let uploadParams = jsOptions<IUploadParams>(fun p ->
                            p.url <- "/api/uploadfile"
                            p.method <- Some POST
                            p.headers <-
                                {|
                                    filename = file.meta.name

                                |}
                            p.body <- file.file
                        )

                        return uploadParams
                    })
                ] [ ]
            ]
            Modal.Card.foot [ ] [
                Button.button [ Button.Color IsSuccess; Button.OnClick (fun _ -> dispatch ToggleDropzone) ] [ str "Done" ]
            ]
        ]
    ]

let view (model : Model) (dispatch : Msg -> unit) =
    div [ ] [
        navbar dispatch

        showAllImages model

        Hero.hero [ Hero.IsFullheightWithNavbar ] [
            Hero.head [ ]
            ^>> Column.column [ Column.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Right) ] ]
            ^>> a [ Href projectGithubLink; Class "anchor-with-icon" ]
            ^>> Fa.i [ Fa.Brand.Github; Fa.Size Fa.Fa4x ] [ ]

            Hero.body
            ^> Column.column [ ] [
                Heading.h1 [ Heading.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered)] ] ^>>& "wcat"
                helpMessage
                downloadButtons
            ]
        ]

        showDropzone model dispatch
    ]

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Program.mkProgram init update view
|> Program.withBridgeConfig
    (
        Bridge.endpoint BridgeSocketEndpoint
        |> Bridge.withMapping Remote
    )
|> Program.withSubscription timer
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactBatched "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run