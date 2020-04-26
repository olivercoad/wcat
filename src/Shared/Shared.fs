module Shared
open System

let BridgeSocketEndpoint = "/socket/init"

type PreviewContent =
    | ImageSrc of string
    | PlainText of string
    | Markdown of string
    | ContentTypeNotImplemented of string

type Preview = {
    Filename: string option
    Time: DateTime
    Content: PreviewContent
    Id: Guid
}

 /// A type that specifies the messages sent to the server from the client on Elmish.Bridge
/// to learn more, read about at https://github.com/Nhowka/Elmish.Bridge#shared
type ServerMsg =
    | SM1 of string
    | SM2 of int

/// A type that specifies the messages sent to the client from the server on Elmish.Bridge
type ClientMsg =
    | PreviewMsg of Preview list
