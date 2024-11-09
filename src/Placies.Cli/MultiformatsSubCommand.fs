namespace Placies.Cli.MultiformatsSubCommand

open System
open System.IO
open FsToolkit.ErrorHandling
open Argu
open Placies.Utils
open Placies.Multiformats

[<RequireQualifiedAccess>]
type MultiBaseRebaseArgs =
    | Input of path: string
    | Output_Base of base_name: string
    | Output of path: string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | MultiBaseRebaseArgs.Input _ -> "Path of a file with input data, or when is -, read from stdin"
            | MultiBaseRebaseArgs.Output_Base _ -> "Output multibase name"
            | MultiBaseRebaseArgs.Output _ -> "Path of a file where output data is written, or when is -, write to stdout"

[<RequireQualifiedAccess>]
type MultiBaseArgs =
    | [<CliPrefix(CliPrefix.None); SubCommand>] Rebase of ParseResults<MultiBaseRebaseArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | MultiBaseArgs.Rebase _ -> "Convert a multibase string to another multibase"

[<RequireQualifiedAccess>]
type MultiformatsArgs =
    | [<CliPrefix(CliPrefix.None); SubCommand>] MultiBase of ParseResults<MultiBaseArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | MultiformatsArgs.MultiBase _ -> "Commands on multibases"

[<RequireQualifiedAccess>]
module MultiformatsArgs =

    let handle
            (multiBaseProvider: IMultiBaseProvider)
            (multiformatsArgsParseResults: ParseResults<MultiformatsArgs>)
            =
        taskResult {
            match multiformatsArgsParseResults.GetSubCommand() with
            | MultiformatsArgs.MultiBase multiBaseArgsParseResults ->
                match multiBaseArgsParseResults.GetSubCommand() with
                | MultiBaseArgs.Rebase rebaseArgsParseResults ->
                    let inputArg = rebaseArgsParseResults.GetResult(MultiBaseRebaseArgs.Input)
                    let outputBaseArg = rebaseArgsParseResults.GetResult(MultiBaseRebaseArgs.Output_Base)
                    let outputArg = rebaseArgsParseResults.GetResult(MultiBaseRebaseArgs.Output)

                    let! outputBaseInfo = multiBaseProvider.TryGetByName(outputBaseArg) |> Result.requireSome $"Unknown output base: %s{outputBaseArg}"

                    use inputStream =
                        if inputArg = "-" then
                            Console.OpenStandardInput()
                        else
                            File.OpenRead(inputArg)
                    use outputStream =
                        if outputArg = "-" then
                            Console.OpenStandardOutput()
                        else
                            File.OpenRead(outputArg)

                    use inputStreamReader = new StreamReader(inputStream)
                    let! inputText = inputStreamReader.ReadToEndAsync()
                    let inputText = inputText.Trim()

                    let data = MultiBase.decode multiBaseProvider (inputText.AsMemory())
                    let outputText = MultiBase.encode outputBaseInfo (data.AsMemory().AsReadOnly())

                    use outputStreamWriter = new StreamWriter(outputStream)
                    do! outputStreamWriter.WriteAsync(outputText)
        }
