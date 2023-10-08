namespace Placies

open System

type ShipyardMultiHash = Ipfs.MultiHash
type ShipyardCid = Ipfs.Cid

[<CustomEquality; CustomComparison>]
[<StructuredFormatDisplay("{DisplayText}")>]
type Cid = {
    ShipyardCid: ShipyardCid
} with
    override this.Equals(obj) =
        match obj with
        | null -> false
        | :? Cid as other -> (this :> IEquatable<Cid>).Equals(other)
        | _ -> false
    override this.GetHashCode() =
        this.ShipyardCid.GetHashCode()
    interface IEquatable<Cid> with
        member this.Equals(other) =
            this.ShipyardCid.Equals(other.ShipyardCid)

    interface IComparable<Cid> with
        member this.CompareTo(other) =
            this.ShipyardCid.Encode().CompareTo(other.ShipyardCid.Encode())
    interface IComparable with
        member this.CompareTo(obj) =
            match obj with
            | :? Cid as other -> (this :> IComparable<Cid>).CompareTo(other)
            | _ -> invalidArg (nameof obj) $"Type is not {typeof<Cid>}: {obj.GetType()}"

    override this.ToString() =
        this.ShipyardCid.ToString()
    member this.DisplayText = this.ToString()

[<RequireQualifiedAccess>]
module Cid =

    let create (encoding: string) (version: int) (contentType: string) (multiHash: ShipyardMultiHash) : Cid =
        let shipyardCid = ShipyardCid(
            Encoding = encoding,
            Version = version,
            ContentType = contentType,
            Hash = multiHash
        )
        { ShipyardCid = shipyardCid }

    let encode (cid: Cid) : string =
        cid.ShipyardCid.Encode()

    let decode (input: string) : Cid =
        let shipyardCid = ShipyardCid.Decode(input)
        { ShipyardCid = shipyardCid }

    let tryDecode (input: string) : ResultExn<Cid> =
        Result.tryWith (fun () -> decode input)
