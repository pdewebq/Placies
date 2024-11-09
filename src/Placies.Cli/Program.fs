namespace Placies.Cli

open System
open FsToolkit.ErrorHandling
open Argu
open Placies.Utils
open Placies.Multiformats
open Placies.Cli.MultiformatsSubCommand
open Placies.Cli.IpldSubCommand


[<RequireQualifiedAccess>]
type Args =
    | [<CliPrefix(CliPrefix.None); SubCommand>] Ipld of ParseResults<IpldArgs>
    | [<CliPrefix(CliPrefix.None); SubCommand>] Multiformats of ParseResults<MultiformatsArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Args.Ipld _ -> "Commands with IPLD data"
            | Args.Multiformats _ -> "Commands with Multiformats data"

module Program =

    [<EntryPoint>]
    let main args =
        let multiBaseProvider: IMultiBaseProvider = MultiBaseRegistry.CreateDefault()
        let multiCodecProvider: IMultiCodecProvider = MultiCodecRegistry.CreateDefault()
        let multiHashProvider: IMultiHashProvider = MultiHashRegistry.CreateDefault()

        let argumentParser =
            let errorHandler = ProcessExiter(function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.DarkRed)
            ArgumentParser.Create<Args>(errorHandler=errorHandler)
        let argsParseResults = argumentParser.Parse(args)
        taskResult {
            match argsParseResults.GetSubCommand() with
            | Args.Ipld ipldArgsParseResults ->
                do! ipldArgsParseResults |> IpldArgs.handle multiBaseProvider multiCodecProvider multiHashProvider
            | Args.Multiformats multiformatsArgsParseResults ->
                do! multiformatsArgsParseResults |> MultiformatsArgs.handle multiBaseProvider
        } |> Task.runSynchronously |> Result.getOk
        0
