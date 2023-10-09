namespace Placies.Multiformats

open System
open System.IO
open Ipfs
open Placies

type ShipyardMultiHash = Ipfs.MultiHash

type MultiHash = {
    HashFunctionCode: int
    Digest: byte array
} with
    member this.DigestSize: int = this.Digest.Length

[<RequireQualifiedAccess>]
module MultiHash =

    let ofStream (stream: Stream) : MultiHash =
        let hashFuncCode = stream.ReadVarint32()
        let digestSize = stream.ReadVarint32()
        let digest = Array.zeroCreate digestSize
        stream.Read(digest.AsSpan()) |> ignore
        { HashFunctionCode = hashFuncCode
          Digest = digest }

    let ofBytes (bytes: byte array) : MultiHash =
        use stream = new MemoryStream(bytes)
        ofStream stream

    let ofShipyardMultiHash (shipyardMultiHash: ShipyardMultiHash) : MultiHash =
        { HashFunctionCode = shipyardMultiHash.Algorithm.Code
          Digest = shipyardMultiHash.Digest }

    let parseBase58String (input: string) : Result<MultiHash, exn> =
        Result.tryWith ^fun () ->
            ShipyardMultiHash(input) |> ofShipyardMultiHash

    let writeToStream (stream: Stream) (multiHash: MultiHash) : unit =
        stream.WriteVarint(multiHash.HashFunctionCode)
        stream.WriteVarint(multiHash.DigestSize)
        stream.Write(multiHash.Digest)

    let toBase58String (multiHash: MultiHash) : string =
        use stream = new MemoryStream()
        multiHash |> writeToStream stream
        stream.ToArray().ToBase58()

    let computeFromBytes (bytes: byte array) (algorithmName: string) : MultiHash =
        ShipyardMultiHash.ComputeHash(bytes, algorithmName) |> ofShipyardMultiHash

    let computeFromStream (stream: Stream) (algorithmName: string) : MultiHash =
        ShipyardMultiHash.ComputeHash(stream, algorithmName) |> ofShipyardMultiHash
