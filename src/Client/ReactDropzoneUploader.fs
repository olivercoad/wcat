module ReactDropzoneUploader

open Fable.Core
open Browser.Types
open Fable.React
open Fable.Core.JsInterop
open Fable.React.Props

[<StringEnum>]
type StatusValue =
  | [<CompiledName("rejected_file_type")>] RejectedFileType
  | [<CompiledName("rejected_max_files")>] RejectedMaxFiles
  | Preparing
  | [<CompiledName("error_file_size")>] ErrorFileSize
  | [<CompiledName("error_validation")>] ErrorValidation
  | Ready
  | Started
  | [<CompiledName("getting_upload_params")>] GettingUploadParams
  | [<CompiledName("error_upload_params")>] ErrorUploadParams
  | Uploading
  | [<CompiledName("exception_upload")>] ExceptionUpload
  | Aborted
  | Restarted
  | Removed
  | [<CompiledName("error_upload")>] ErrorUpload
  | [<CompiledName("headers_received")>] HeadersReceived
  | Done

[<StringEnum>]
type MethodValue =
  | DELETE
  | GET
  | HEAD
  | OPTIONS
  | PATCH
  | POST
  | PUT

type IMeta = {
    ``id`` : string
    status : StatusValue
    ``type``: string // MIME type, example: `image/*`
    name: string
    uploadedDate: string // ISO string
    percent: float
    size: int // bytes
    lastModifiedDate: string // ISO string
    previewUrl: string option // from URL.createObjectURL
    duration: float option // seconds
    width: int option
    height: int option
    videoWidth: int option
    videoHeight: int option
    validationError: obj option
}

type IFileWithMeta = {
    file : File
    meta : IMeta
    cancel : unit -> unit
    restart : unit -> unit
    remove : unit -> unit
    // xhr : XMLHttpRequest option
}

type IExtra = {
  active: bool
  reject: bool
  dragged: DataTransferItem[]
  accept: string
  multiple: bool
  minSizeBytes: int
  maxSizeBytes: int
  maxFiles: int
}

type IUploadParams =
  abstract url: string with get, set
  abstract method: MethodValue option with get, set
  abstract body: File with get, set
  abstract fields: string list with get, set
  abstract headers: obj with get, set



type CustomizationFunction<'a> = IFileWithMeta list -> IExtra -> 'a

type DropzoneProps =
    | OnChangeStatus of (IFileWithMeta -> StatusValue -> IFileWithMeta array -> IFileWithMeta option)
    | GetUploadParams of (IFileWithMeta -> JS.Promise<IUploadParams>)
    | OnSubmit of (IFileWithMeta array -> IFileWithMeta array -> unit)
    | Accept of string
    | Multiple of bool
    | MinSizeBytes of int
    | MaxSizeBytes of int
    | MaxFiles of int
    | Validate of (IFileWithMeta -> bool)
    | AutoUpload of bool
    | Timeout of int
    | InitialFiles of File list
    | Disabled of CustomizationFunction<bool>
    | CanCancel of CustomizationFunction<bool>
    | CanRemove of CustomizationFunction<bool>
    | CanRestart of CustomizationFunction<bool>
    | InputContent of CustomizationFunction<ReactElement>
    | InputWithFilesContent of CustomizationFunction<ReactElement>
    | SubmitButtonDisabled of CustomizationFunction<bool>
    | SubmitButtonContent of CustomizationFunction<ReactElement>



let inline dropzone (props : DropzoneProps list) (elems : ReactElement list) : ReactElement =
    importSideEffects "react-dropzone-uploader/dist/styles.css"
    ofImport "default" "react-dropzone-uploader" (keyValueList CaseRules.LowerFirst props) elems
