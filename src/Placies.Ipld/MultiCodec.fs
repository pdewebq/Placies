namespace Placies.Multiformats

open System.Collections.Generic
open Placies

type MultiCodecInfo = {
    Name: string
    Code: int
}

[<RequireQualifiedAccess>]
module MultiCodecInfos =

    let Cidv1 = { Name = "cidv1"; Code = 0x1 }

    let Sha2_256 = { Name = "sha2-256"; Code = 0x12 }

    let Varsig = { Name = "varsig"; Code = 0x34 }
    let Dns = { Name = "dns"; Code = 0x35 }

    let DagPb = { Name = "dag-pb"; Code = 0x70 }
    let DagCbor = { Name = "dag-cbor"; Code = 0x71 }
    let Libp2pKey = { Name = "libp2p-key"; Code = 0x72 }

    let Ipfs = { Name = "ipfs"; Code = 0xe3 }
    let Ipns = { Name = "ipns"; Code = 0xe5 }

    let DagJson = { Name = "dag-json"; Code = 0x0129 }

    let RsaPub = { Name = "rsa-pub"; Code = 0x1205 }


type IMultiCodecProvider =
    abstract TryGetByCode: code: int -> MultiCodecInfo option
    abstract TryGetByName: name: string -> MultiCodecInfo option

type MultiCodecRegistry() =
    let registryOfName = Dictionary<string, MultiCodecInfo>()
    let registryOfCode = Dictionary<int, MultiCodecInfo>()

    member _.Register(codecInfo: MultiCodecInfo): bool =
        registryOfCode.TryAdd(codecInfo.Code, codecInfo)
        && registryOfName.TryAdd(codecInfo.Name, codecInfo)

    interface IMultiCodecProvider with
        member _.TryGetByCode(code: int): MultiCodecInfo option =
            registryOfCode.TryGetValue(code) |> Option.ofTryByref
        member _.TryGetByName(name: string): MultiCodecInfo option =
            registryOfName.TryGetValue(name) |> Option.ofTryByref

    static member CreateDefault(): MultiCodecRegistry =
        let registry = MultiCodecRegistry()
        registry.Register(MultiCodecInfos.Cidv1) |> ignore
        registry.Register(MultiCodecInfos.Sha2_256) |> ignore
        registry.Register(MultiCodecInfos.Varsig) |> ignore
        registry.Register(MultiCodecInfos.Dns) |> ignore
        registry.Register(MultiCodecInfos.DagPb) |> ignore
        registry.Register(MultiCodecInfos.DagCbor) |> ignore
        registry.Register(MultiCodecInfos.Libp2pKey) |> ignore
        registry.Register(MultiCodecInfos.Ipfs) |> ignore
        registry.Register(MultiCodecInfos.Ipns) |> ignore
        registry.Register(MultiCodecInfos.DagJson) |> ignore
        registry.Register(MultiCodecInfos.RsaPub) |> ignore
        registry
