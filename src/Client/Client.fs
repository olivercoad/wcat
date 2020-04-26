module Client

open Elmish
open Elmish.React
open Elmish.Bridge
open Fable.Core
open Fable.FontAwesome
open Fable.FontAwesome.Free
open Fable.React
open Fable.React.Props
open Fetch.Types
open Thoth.Fetch
open Fulma
open Thoth.Json

open Shared

// [<ImportAll("moment")>]
// let moment = jsNative

[<Import("*", from="moment")>]
module moment =
    [<Emit("moment($0).fromNow()")>]
    let strFromNow (datetime:string) : string = jsNative

    [<Emit("moment($0).format('l HH:mm:ss Z')")>]
    let longFormat (datetime:string) : string = jsNative

[<Import("*", from="markdown-it-checkbox")>]
module markdownitcheckbox =
    let thing = 5

[<Import("*", from="markdown-it")>]
module markdownit =

    [<Emit("new markdownit('commonmark').use(markdownitcheckbox).render($0)")>]
    let render (markdown:string) : string = jsNative

let momentFromNow (datetime:System.DateTime) =
    moment.strFromNow (datetime.ToString("o"))

let momentLongFormat (datetime:System.DateTime) =
    moment.longFormat (datetime.ToString("o"))

type Model = {
    Previews: Preview list
}

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events

type Msg =
    | Remote of ClientMsg


// defines the initial state and initial command (= side-effect) of the application
let init () : Model * Cmd<Msg> =
    { Previews = [] }, Cmd.none

let addOnlyNewPreviews currentPreviews updatePreviews =
    match currentPreviews with
    | [] ->
        //updatePreviews are given as oldest to newest, but currentPreviews is newest to oldest
        List.rev updatePreviews
    | latestPreview::_ ->
        let newPreviews =
            updatePreviews
            |> List.rev
            |> List.takeWhile (fun p -> p.Id <> latestPreview.Id)
        newPreviews @ currentPreviews



// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match msg with
    | Remote(PreviewMsg previews) ->
        { currentModel with Previews = addOnlyNewPreviews currentModel.Previews previews }, Cmd.none

/// Creates a pre with class box
let preBox options children = pre (upcast ClassName "box"::options) children

/// Places the element on the right in the element on the left with no options
let inline (^>) a (b:ReactElement) : ReactElement = a [ ] [ b ]
/// Places the string on the right in the element on the left with no options
let inline (^>&) a (b:string) : ReactElement = a [ ] [ str b ]

let showPreview preview =
    Column.column
        [
            Column.Width (Screen.All, Column.IsFull)
            Column.CustomClass "preview-item"
        ]
        [
            match preview.Content with
            | ImageSrc src ->
                img [ Src src ]
            | PlainText content ->
                preBox ^>& content

            | Markdown content ->
                Container.container ^> div [ DangerouslySetInnerHTML { __html = (markdownit.render content) } ] [ ]

            | ContentTypeNotImplemented contentType ->
                Message.message [ Message.Color IsDanger ]
                    [
                        Message.header ^>& "Content type not supported"
                        Message.body ^>& contentType
                    ]

            p [ ] [ str (Option.defaultValue "" preview.Filename) ]
            Columns.columns [ Columns.CustomClass "preview-info" ] [
                Column.column [ ] [
                    p [ ] [ str (momentFromNow preview.Time)]
                ]
                Column.column [ Column.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Right) ] ] [
                    p [ ] [ str (momentLongFormat preview.Time) ]
                ]
            ]
            hr [ ]
        ]

let showAllImages model =
    Column.column
        [ Column.Width (Screen.All, Column.IsFull)
        ]
        //   Column.Offset (Screen.All, Column.Is4)]
        (model.Previews |> List.map showPreview)

let view (model : Model) (dispatch : Msg -> unit) =
    div [ ] [
        showAllImages model
        Column.column [ ] [
            Message.message [ Message.Color IsInfo ] [
                Message.header [ ] [ str "Use the wcat cli tool to send files for preview" ]
                Message.body [ ] [
                    p [ ] [ str "The wcat cli tool is a simple bash script" ]
                    a [ Href "/wcat.sh" ] [ str "Download it here" ]
                    p [ ] [ str "In bash, edit wcat to make sure it has the correct address for the server (eg. http://localhost:8085/api/showthis)" ]
                    p [ ] [ str "Make it executable (chmod +x wcat) and place it somewhere in PATH" ]
                    p [ ] [ str "Then use it just like you would use cat: wcat example.jpg" ]
                    p [ ] [ str "Instead of outputting the content inline, it will send the file to the server."]
                    p [ ] [ str "The server will broadcast the content to be displayed here in the browser."]
                ]
            ]
        ]
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
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactBatched "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run