namespace IpfsSigningGatewayProxy

open FsToolkit.ErrorHandling
open Ipfs

[<RequireQualifiedAccess>]
type SigningIpnsAddress =
    | Key of libp2pKey: Cid
    | DnsName of dnsName: string

[<RequireQualifiedAccess>]
module SigningIpnsAddress =

    let escapeIpnsDnsName (dnsName: string) : string =
        dnsName.Replace("-", "--").Replace('.', '-')

    let parseLibp2pKey (input: string) = result {
        let! cidOfLibp2pKey = Result.tryWith (fun () -> Cid.Decode(input)) |> Result.mapError (fun ex -> $"Not CID: {ex}")
        do! (cidOfLibp2pKey.ContentType = "libp2p-key") |> Result.requireTrue "Not libp2p-key"
        return cidOfLibp2pKey
    }

    let parseIpnsName (ipnsName: string) (shouldEscapeDnsName: bool) : SigningIpnsAddress =
        let cidOfLibp2pKey = parseLibp2pKey ipnsName
        match cidOfLibp2pKey with
        | Ok cidOfLibp2pKey ->
            SigningIpnsAddress.Key cidOfLibp2pKey
        | Error _err ->
            let dnsName = if shouldEscapeDnsName then escapeIpnsDnsName ipnsName else ipnsName
            SigningIpnsAddress.DnsName dnsName


[<RequireQualifiedAccess>]
type SigningAddress =
    | Ipfs of cid: Cid
    | Ipns of SigningIpnsAddress
