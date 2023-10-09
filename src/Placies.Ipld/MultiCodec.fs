namespace Placies.Multiformats

open System.Collections.Generic
open Placies

type MultiCodecInfo = {
    Name: string
    Code: int
}

[<RequireQualifiedAccess>]
module MultiCodecInfos =

    let Sha2_256 = { Name = "sha2-256"; Code = 0x12 }

    let DagPb = { Name = "dag-pb"; Code = 0x70 }
    let DagCbor = { Name = "dag-cbor"; Code = 0x71 }

    let DagJson = { Name = "dag-json"; Code = 0x0129 }


type IMultiCodecProvider =
    abstract TryGetByCode: code: int -> MultiCodecInfo option
    abstract TryGetByName: name: string -> MultiCodecInfo option

type MultiCodecRegistry() =
    let registryOfName = Dictionary<string, MultiCodecInfo>()
    let registryOfCode = Dictionary<int, MultiCodecInfo>()
    member _.Register(codecInfo: MultiCodecInfo): bool =
        registryOfCode.TryAdd(codecInfo.Code, codecInfo) |> ignore
        registryOfName.TryAdd(codecInfo.Name, codecInfo)
    interface IMultiCodecProvider with
        member _.TryGetByCode(code: int): MultiCodecInfo option =
            registryOfCode.TryGetValue(code) |> Option.ofTryByref
        member _.TryGetByName(name: string): MultiCodecInfo option =
            registryOfName.TryGetValue(name) |> Option.ofTryByref
