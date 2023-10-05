namespace Placies.Gateway

open FsToolkit.ErrorHandling
open Ipfs
open Placies

[<RequireQualifiedAccess>]
type IpfsContentRootIpns =
    | Key of libp2pKey: Cid
    | DnsName of dnsName: string

[<RequireQualifiedAccess>]
module IpfsContentRootIpns =

    let unescapeIpnsDnsName (dnsName: string) : string =
        // TODO: Do it without a crutchy intermediate symbol
        dnsName.Replace("--", "$").Replace('-', '.').Replace('$', '-')

    let parseLibp2pKey (input: string) = result {
        let! cidOfLibp2pKey = Result.tryWith (fun () -> Cid.Decode(input)) |> Result.mapError (fun ex -> $"Not CID: {ex}")
        do! (cidOfLibp2pKey.ContentType = "libp2p-key") |> Result.requireTrue "Not libp2p-key"
        return cidOfLibp2pKey
    }

    let parseIpnsName (ipnsName: string) (shouldUnescapeDnsName: bool) : IpfsContentRootIpns =
        let cidOfLibp2pKey = parseLibp2pKey ipnsName
        match cidOfLibp2pKey with
        | Ok cidOfLibp2pKey ->
            IpfsContentRootIpns.Key cidOfLibp2pKey
        | Error _err ->
            let dnsName = if shouldUnescapeDnsName then unescapeIpnsDnsName ipnsName else ipnsName
            IpfsContentRootIpns.DnsName dnsName


type IpfsContentRoot =
    | Ipfs of cid: Cid
    | Ipns of IpfsContentRootIpns

[<RequireQualifiedAccess>]
module IpfsContentRoot =

    let toNamespaceAndValue (contentRoot: IpfsContentRoot) : string * string =
        match contentRoot with
        | Ipfs cid ->
            "ipfs", cid.ToString()
        | Ipns ipnsAddress ->
            let ipnsValue =
                match ipnsAddress with
                | IpfsContentRootIpns.Key libp2PKey -> libp2PKey.ToString()
                | IpfsContentRootIpns.DnsName dnsName -> dnsName
            "ipns", ipnsValue