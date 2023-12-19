namespace Placies.Multiformats

open System
open System.Collections.Generic
open System.Security.Cryptography
open Placies.Utils


type IdentityHashAlgorithm() =
    inherit HashAlgorithm()
    let mutable memory: Memory<byte> = Unchecked.defaultof<_>
    override this.HashCore(array, ibStart, cbSize) =
        memory <- array.AsMemory(ibStart, cbSize)
    override this.HashFinal() =
        memory.ToArray()
    override this.Initialize() =
        ()


[<RequireQualifiedAccess>]
module MultiHashInfos =

    let Identity = { CodecInfo = MultiCodecInfos.Identity; HashAlgorithm = fun () -> new IdentityHashAlgorithm() }

    let Sha2_256 = { CodecInfo = MultiCodecInfos.Sha2_256; HashAlgorithm = fun () -> SHA256.Create() }


type MultiHashRegistry() =
    let registryOfName = Dictionary<string, MultiHashInfo>()
    let registryOfCode = Dictionary<int, MultiHashInfo>()

    member this.Register(info: MultiHashInfo): bool =
        registryOfCode.TryAdd(info.Code, info)
        && registryOfName.TryAdd(info.Name, info)

    interface IMultiHashProvider with
        member this.TryGetByCode(code) =
            registryOfCode.TryGetValue(code) |> Option.ofTryByref
        member this.TryGetByName(name) =
            registryOfName.TryGetValue(name) |> Option.ofTryByref

    static member CreateDefault(): MultiHashRegistry =
        let registry = MultiHashRegistry()
        registry.Register(MultiHashInfos.Identity) |> ignore
        registry.Register(MultiHashInfos.Sha2_256) |> ignore
        registry
