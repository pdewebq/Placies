namespace Placies.Cli.IpldSubCommand

open System
open System.IO
open FsToolkit.ErrorHandling
open Argu
open Placies
open Placies.Utils
open Placies.Ipld
open Placies.Ipld.DagCbor
open Placies.Ipld.DagJson
open Placies.Multiformats

[<RequireQualifiedAccess>]
type IpldRecodeArgs =
    | Input_Codec of codec_name: string
    | Input of path: string
    | Output_Codec of codec_name: string
    | Output of path: string
    | Print_Cid
    | Cid_Version of version: int
    | Cid_MultiHash of multihash_name: string
    | Cid_MultiBase of multibase_name: string
    static member DefaultCidVersion = 1
    static member DefaultCidMultiHash = MultiHashInfos.Sha2_256
    static member DefaultCidMultiBase = MultiBaseInfos.Base32
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | IpldRecodeArgs.Input_Codec _ -> "Input multicodec name"
            | IpldRecodeArgs.Input _ -> "Path of a file with input data, or when is -, read from stdin"
            | IpldRecodeArgs.Output_Codec _ -> "Output multicodec name"
            | IpldRecodeArgs.Output _ -> "Path of a file where output data is written, or when is -, write to stdout"
            | IpldRecodeArgs.Print_Cid -> "Print CID of the output data"
            | IpldRecodeArgs.Cid_Version _ -> $"(when --print-cid) CID version (default %i{IpldRecodeArgs.DefaultCidVersion})"
            | IpldRecodeArgs.Cid_MultiHash _ -> $"(when --print-cid) CID multihash name (default %s{IpldRecodeArgs.DefaultCidMultiHash.Name})"
            | IpldRecodeArgs.Cid_MultiBase _ -> $"(when --print-cid) CID multibase name (default %s{IpldRecodeArgs.DefaultCidMultiBase.Name})"

[<RequireQualifiedAccess>]
type IpldArgs =
    | [<CliPrefix(CliPrefix.None); SubCommand>] Recode of ParseResults<IpldRecodeArgs>
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | IpldArgs.Recode _ -> "Convert IPLD data from a codec to another codec"

[<RequireQualifiedAccess>]
module IpldArgs =

    let getCodecByInfo multiBaseProvider (codecInfo: MultiCodecInfo) : ICodec option =
        match codecInfo with
        | Equals MultiCodecInfos.DagJson -> DagJsonCodec(multiBaseProvider) :> ICodec |> Some
        | Equals MultiCodecInfos.DagCbor -> DagCborCodec() :> ICodec |> Some
        | _ -> None

    let handle
            (multiBaseProvider: IMultiBaseProvider) (multiCodecProvider: IMultiCodecProvider) (multiHashProvider: IMultiHashProvider)
            (ipldArgsParseResults: ParseResults<IpldArgs>)
            =
        taskResult {
            match ipldArgsParseResults.GetSubCommand() with
            | IpldArgs.Recode ipldRecodeArgsParseResults ->
                let inputCodecArg = ipldRecodeArgsParseResults.GetResult(IpldRecodeArgs.Input_Codec)
                let inputArg = ipldRecodeArgsParseResults.GetResult(IpldRecodeArgs.Input)
                let outputCodecArg = ipldRecodeArgsParseResults.GetResult(IpldRecodeArgs.Output_Codec)
                let outputArg = ipldRecodeArgsParseResults.GetResult(IpldRecodeArgs.Output)
                let printCidArg = ipldRecodeArgsParseResults.Contains(IpldRecodeArgs.Print_Cid)

                let! inputCodecInfo = multiCodecProvider.TryGetByName(inputCodecArg) |> Result.requireSome $"Unknown input codec: %s{inputCodecArg}"
                let! outputCodecInfo = multiCodecProvider.TryGetByName(outputCodecArg) |> Result.requireSome $"Unknown output codec: %s{outputCodecArg}"

                let! inputCodec = inputCodecInfo |> getCodecByInfo multiBaseProvider |> Result.requireSome $"Unknown input codec info: %A{inputCodecInfo}"
                let! outputCodec = outputCodecInfo |> getCodecByInfo multiBaseProvider |> Result.requireSome $"Unknown output codec info: %A{outputCodecInfo}"

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

                let! dataModelNode = inputCodec.TryDecodeAsync(inputStream) |> TaskResult.mapError (fun ex -> $"Failed decode input stream:\n{ex}")

                match printCidArg with
                | false ->
                    do! outputCodec.TryEncodeAsync(outputStream, dataModelNode) |> TaskResult.mapError (fun ex -> $"Failed encode to output stream:\n{ex}")
                | true ->
                    let cidVersion = ipldRecodeArgsParseResults.GetResult(IpldRecodeArgs.Cid_Version, defaultValue=IpldRecodeArgs.DefaultCidVersion)
                    let cidMultihashArg = ipldRecodeArgsParseResults.TryGetResult(IpldRecodeArgs.Cid_MultiHash)
                    let cidMultibaseArg = ipldRecodeArgsParseResults.TryGetResult(IpldRecodeArgs.Cid_MultiBase)
                    let! cidMultihashInfo =
                        match cidMultihashArg with
                        | None -> IpldRecodeArgs.DefaultCidMultiHash |> Ok
                        | Some cidMultihashArg -> multiHashProvider.TryGetByName(cidMultihashArg) |> Result.requireSome $"Unknown cid multihash: %s{cidMultihashArg}"
                    let! cidMultibaseInfo =
                        match cidMultibaseArg with
                        | None -> IpldRecodeArgs.DefaultCidMultiBase |> Ok
                        | Some cidMultibaseArg -> multiBaseProvider.TryGetByName(cidMultibaseArg) |> Result.requireSome $"Unknown cid multibase: %s{cidMultibaseArg}"

                    let! cid = outputCodec.TryEncodeWithCidAsync(outputStream, dataModelNode, cidVersion, cidMultihashInfo) |> TaskResult.mapError (fun ex -> $"Failed encode to output stream:\n{ex}")
                    let cidStr = cid |> Cid.toByteArray |> Array.asReadOnlyMemory |> MultiBase.encode cidMultibaseInfo
                    Console.WriteLine(cidStr)
        }
